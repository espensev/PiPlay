using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows.Threading;
using PiPlay.Services;

namespace PiPlay.Tests;

/// <summary>
/// Acknowledged single-instance hand-off (REQ-APP-01, ADR-0009, spec 11): the running instance
/// decides before the sender stops waiting, a withdrawn dispatch never runs later, and a same-ID
/// retry never applies a link twice.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Logic)]
public class SingleInstanceHandoffTests
{
    private const string Link = "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLx";
    private static readonly TimeSpan ShortDispatch = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan TestWait = TimeSpan.FromSeconds(10);

    // --- Timing contract ---

    [Fact]
    public void Dispatch_bound_plus_margin_ends_before_the_sender_stops_waiting()
    {
        Assert.True(
            SingleInstanceHandoffPolicy.DispatchTimeout + SingleInstanceHandoffPolicy.AckMargin
                < SingleInstanceHandoffPolicy.AckTimeout,
            "The running instance must decide and reply while the sender still waits; otherwise " +
            "a retry can apply the link twice and a late apply can follow 'did not respond'.");
    }

    [Fact]
    public void Handoff_deadlines_match_the_accepted_coordination_contract()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(2500), SingleInstanceHandoffPolicy.DispatchTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(3500), SingleInstanceHandoffPolicy.AckTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(500), SingleInstanceHandoffPolicy.AckMargin);
        Assert.Equal(TimeSpan.FromSeconds(2), SingleInstanceHandoffPolicy.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), SingleInstanceHandoffPolicy.RequestTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), SingleInstanceHandoffPolicy.ReplyTimeout);
        Assert.Equal(2, SingleInstanceHandoffPolicy.MaxAttempts);
    }

    // --- Wire format ---

    [Theory]
    [InlineData(Link + "|with-separator")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42\r\nsecond line")]
    [InlineData("https://www.youtube.com/results?search_query=bl%C3%A5b%C3%A6r pai")]
    [InlineData("")]
    public void Request_line_round_trips_the_url_on_one_line(string url)
    {
        var request = HandoffRequest.Create(url);

        var line = SingleInstanceHandoffPolicy.FormatRequest(request);

        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.True(SingleInstanceHandoffPolicy.TryParseRequest(line, out var parsed));
        Assert.Equal(request, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("v2|0123456789abcdef0123456789abcdef|x")]
    [InlineData("v1|not-a-request-id|x")]
    [InlineData("v1|0123456789abcdef0123456789abcdef")]
    [InlineData("v1|0123456789abcdef0123456789abcdef|x|y")]
    public void Malformed_request_lines_are_rejected(string? line) =>
        Assert.False(SingleInstanceHandoffPolicy.TryParseRequest(line, out _));

    [Theory]
    [InlineData("applied", nameof(HandoffOutcome.Applied))]
    [InlineData("not-applied", nameof(HandoffOutcome.NotApplied))]
    [InlineData(null, nameof(HandoffOutcome.NoAcknowledgement))]
    [InlineData("", nameof(HandoffOutcome.NoAcknowledgement))]
    [InlineData("APPLIED", nameof(HandoffOutcome.NoAcknowledgement))]
    public void Reply_parsing_treats_anything_unexpected_as_no_acknowledgement(string? line, string expected) =>
        Assert.Equal(Enum.Parse<HandoffOutcome>(expected), SingleInstanceHandoffPolicy.ParseReply(line));

    // --- Running instance: start-or-withdraw decision and apply-once ledger ---

    [Fact]
    public async Task Dispatch_the_ui_thread_has_not_started_is_withdrawn_and_never_applies_later()
    {
        var applied = new List<string>();
        ManualDispatch? posted = null;

        var outcome = await SingleInstanceHandoffPolicy.DispatchAsync(
            HandoffRequest.Create(Link), new HandoffRequestLedger(), applied.Add, UnexpectedFailure,
            callback => posted = new ManualDispatch(callback), ShortDispatch, CancellationToken.None);

        Assert.Equal(HandoffOutcome.NotApplied, outcome);
        Assert.False(posted!.Start(), "The UI thread freeing up must not run a withdrawn hand-off.");
        Assert.Empty(applied);
    }

    [Fact]
    public async Task Dispatch_started_before_the_bound_is_acknowledged_without_waiting_for_it_to_finish()
    {
        var applied = new List<string>();

        var outcome = await SingleInstanceHandoffPolicy.DispatchAsync(
            HandoffRequest.Create(Link), new HandoffRequestLedger(), applied.Add, UnexpectedFailure,
            callback =>
            {
                var dispatch = new ManualDispatch(callback);
                dispatch.Start(); // started, but still running at the bound
                return dispatch;
            },
            ShortDispatch, CancellationToken.None);

        Assert.Equal(HandoffOutcome.Applied, outcome);
        Assert.Equal([Link], applied);
    }

    [Fact]
    public async Task Same_id_retry_after_an_applied_handoff_is_acknowledged_without_applying_again()
    {
        var ledger = new HandoffRequestLedger();
        var applied = new List<string>();
        var request = HandoffRequest.Create(Link);

        var first = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, applied.Add, UnexpectedFailure, RunNow, ShortDispatch, CancellationToken.None);
        var retry = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, applied.Add, UnexpectedFailure, RunNow, ShortDispatch, CancellationToken.None);

        Assert.Equal(HandoffOutcome.Applied, first);
        Assert.Equal(HandoffOutcome.Applied, retry);
        Assert.Equal([Link], applied);
    }

    [Fact]
    public async Task Same_id_retry_after_a_withdrawn_handoff_applies_it_once()
    {
        var ledger = new HandoffRequestLedger();
        var applied = new List<string>();
        var request = HandoffRequest.Create(Link);

        var first = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, applied.Add, UnexpectedFailure, callback => new ManualDispatch(callback),
            ShortDispatch, CancellationToken.None);
        var retry = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, applied.Add, UnexpectedFailure, RunNow, ShortDispatch, CancellationToken.None);

        Assert.Equal(HandoffOutcome.NotApplied, first);
        Assert.Equal(HandoffOutcome.Applied, retry);
        Assert.Equal([Link], applied);
    }

    [Fact]
    public async Task Separate_launches_of_the_same_link_each_apply()
    {
        var ledger = new HandoffRequestLedger();
        var applied = new List<string>();

        foreach (var request in new[] { HandoffRequest.Create(Link), HandoffRequest.Create(Link) })
        {
            Assert.Equal(HandoffOutcome.Applied, await SingleInstanceHandoffPolicy.DispatchAsync(
                request, ledger, applied.Add, UnexpectedFailure, RunNow, ShortDispatch, CancellationToken.None));
        }

        Assert.Equal([Link, Link], applied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handoff_that_throws_on_the_ui_thread_is_reported_and_acknowledged_so_it_is_not_retried(
        bool cancelledInside)
    {
        var ledger = new HandoffRequestLedger();
        var request = HandoffRequest.Create(Link);
        var failures = new List<Exception>();
        Exception thrown = cancelledInside
            ? new OperationCanceledException("navigation cancelled")
            : new InvalidOperationException("navigation failed");
        var attempts = 0;
        void Apply(string _)
        {
            attempts++;
            throw thrown;
        }

        var first = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, Apply, failures.Add, RunNow, ShortDispatch, CancellationToken.None);
        var retry = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, Apply, failures.Add, RunNow, ShortDispatch, CancellationToken.None);

        Assert.Equal(HandoffOutcome.Applied, first);
        Assert.Equal(HandoffOutcome.Applied, retry);
        Assert.Equal(1, attempts);
        Assert.Same(thrown, Assert.Single(failures));
    }

    [Fact]
    public async Task Started_dispatch_that_faults_is_still_acknowledged()
    {
        var outcome = await SingleInstanceHandoffPolicy.DispatchAsync(
            HandoffRequest.Create(Link), new HandoffRequestLedger(), _ => { }, UnexpectedFailure,
            callback =>
            {
                var dispatch = new ManualDispatch(callback);
                dispatch.Start();
                dispatch.Fault(new InvalidOperationException("failure callback threw"));
                return dispatch;
            },
            ShortDispatch, CancellationToken.None);

        Assert.Equal(HandoffOutcome.Applied, outcome);
    }

    [Fact]
    public async Task Exit_while_a_dispatch_waits_withdraws_it()
    {
        var applied = new List<string>();
        ManualDispatch? posted = null;
        using var exiting = new CancellationTokenSource();

        var dispatching = SingleInstanceHandoffPolicy.DispatchAsync(
            HandoffRequest.Create(Link), new HandoffRequestLedger(), applied.Add, UnexpectedFailure,
            callback => posted = new ManualDispatch(callback), TestWait, exiting.Token);
        exiting.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatching);
        Assert.False(posted!.Start(), "A hand-off withdrawn at exit must not run.");
        Assert.Empty(applied);
    }

    [Fact]
    public async Task Handoff_withdrawn_by_dispatcher_shutdown_reports_not_applied()
    {
        var applied = new List<string>();

        var outcome = await SingleInstanceHandoffPolicy.DispatchAsync(
            HandoffRequest.Create(Link), new HandoffRequestLedger(), applied.Add, UnexpectedFailure,
            callback =>
            {
                var dispatch = new ManualDispatch(callback);
                dispatch.CancelByShutdown();
                return dispatch;
            },
            TestWait, CancellationToken.None);

        Assert.Equal(HandoffOutcome.NotApplied, outcome);
        Assert.Empty(applied);
    }

    [Fact]
    public void Ledger_forgets_the_oldest_request_ids_beyond_its_capacity()
    {
        var ledger = new HandoffRequestLedger();
        var ids = Enumerable.Range(0, HandoffRequestLedger.Capacity + 1)
            .Select(_ => Guid.NewGuid().ToString("N"))
            .ToArray();

        Assert.All(ids, id => Assert.True(ledger.TryBegin(id)));

        Assert.False(ledger.TryBegin(ids[1]));
        Assert.True(ledger.TryBegin(ids[0]));
    }

    // --- Sender: one same-ID retry ---

    [Fact]
    public async Task Sender_retries_once_with_the_same_request_id_and_stops_at_the_first_acknowledgement()
    {
        var request = HandoffRequest.Create(Link);
        var sent = new List<HandoffRequest>();
        var failures = new List<(int Attempt, HandoffOutcome Outcome)>();

        var outcome = await SingleInstanceHandoffPolicy.SendAsync(
            request,
            (attempt, _) =>
            {
                sent.Add(attempt);
                return Task.FromResult(sent.Count == 1 ? HandoffOutcome.NoAcknowledgement : HandoffOutcome.Applied);
            },
            (attempt, failure) => failures.Add((attempt, failure)),
            CancellationToken.None);

        Assert.Equal(HandoffOutcome.Applied, outcome);
        Assert.Equal([request, request], sent);
        Assert.Equal([(1, HandoffOutcome.NoAcknowledgement)], failures);
    }

    [Fact]
    public async Task Sender_gives_up_after_the_retry_and_reports_the_last_failure()
    {
        var results = new Queue<HandoffOutcome>([HandoffOutcome.Unreachable, HandoffOutcome.NotApplied]);
        var attempts = 0;

        var outcome = await SingleInstanceHandoffPolicy.SendAsync(
            HandoffRequest.Create(Link),
            (_, _) =>
            {
                attempts++;
                return Task.FromResult(results.Dequeue());
            },
            (_, _) => { },
            CancellationToken.None);

        Assert.Equal(HandoffOutcome.NotApplied, outcome);
        Assert.Equal(SingleInstanceHandoffPolicy.MaxAttempts, attempts);
    }

    // --- Real named-pipe round trips ---

    [Fact]
    public async Task Pipe_round_trip_applies_the_link_and_acknowledges_it()
    {
        var pipe = NewPipeName();
        var ledger = new HandoffRequestLedger();
        var applied = new ConcurrentQueue<string>();
        var request = HandoffRequest.Create(Link + "|with-separator");

        var server = SingleInstancePipeTransport.ServeOneAsync(
            pipe,
            (received, ct) => SingleInstanceHandoffPolicy.DispatchAsync(
                received, ledger, applied.Enqueue, UnexpectedFailure, RunNow, TestWait, ct),
            CancellationToken.None);
        var outcome = await SingleInstancePipeTransport.SendOnceAsync(
            pipe, request, TestWait, TestWait, TestWait, CancellationToken.None);

        Assert.Equal(HandoffOutcome.Applied, outcome);
        Assert.Equal(HandoffOutcome.Applied, await server.WaitAsync(TestWait));
        Assert.Equal([request.Url], applied);
    }

    [Fact]
    public async Task Late_apply_after_the_sender_stopped_waiting_is_not_repeated_by_its_retry()
    {
        // The double-apply race: the first attempt is applied only after the sender's wait ended.
        // The retry carries the same request ID, so the ledger acknowledges it without a second apply.
        var pipe = NewPipeName();
        var ledger = new HandoffRequestLedger();
        var applied = new ConcurrentQueue<string>();
        var senderGaveUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstServed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connections = 0;

        async Task<HandoffOutcome> HandleAsync(HandoffRequest received, CancellationToken ct)
        {
            if (Interlocked.Increment(ref connections) == 1)
                await senderGaveUp.Task.WaitAsync(TestWait, ct);
            return await SingleInstanceHandoffPolicy.DispatchAsync(
                received, ledger, applied.Enqueue, UnexpectedFailure, RunNow, TestWait, ct);
        }

        var server = ServeTwoAsync(pipe, HandleAsync, firstServed);
        var request = HandoffRequest.Create(Link);

        var outcome = await SingleInstanceHandoffPolicy.SendAsync(
            request,
            async (attempt, ct) =>
            {
                if (senderGaveUp.Task.IsCompleted)
                    await firstServed.Task.WaitAsync(TestWait, ct);
                return await SingleInstancePipeTransport.SendOnceAsync(
                    pipe, attempt, TestWait, TestWait, TimeSpan.FromMilliseconds(200), ct);
            },
            (_, _) => senderGaveUp.TrySetResult(),
            CancellationToken.None);

        var (first, second) = await server.WaitAsync(TestWait);
        Assert.Equal(HandoffOutcome.Applied, outcome);
        Assert.Equal(HandoffOutcome.Applied, first);
        Assert.Equal(HandoffOutcome.Applied, second);
        Assert.Equal([request.Url], applied);
    }

    [Fact]
    public async Task Handoff_the_ui_thread_never_starts_is_withdrawn_and_stays_unapplied_after_the_sender_gives_up()
    {
        // The UI thread stays blocked through both attempts. Each dispatch is withdrawn at its
        // bound while the sender still waits, so the sender's "did not respond" is final.
        var pipe = NewPipeName();
        var ledger = new HandoffRequestLedger();
        var applied = new ConcurrentQueue<string>();
        var posted = new ConcurrentQueue<ManualDispatch>();
        var firstServed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;

        Task<HandoffOutcome> HandleAsync(HandoffRequest received, CancellationToken ct) =>
            SingleInstanceHandoffPolicy.DispatchAsync(
                received, ledger, applied.Enqueue, UnexpectedFailure,
                callback =>
                {
                    var dispatch = new ManualDispatch(callback);
                    posted.Enqueue(dispatch);
                    return dispatch;
                },
                ShortDispatch, ct);

        var server = ServeTwoAsync(pipe, HandleAsync, firstServed);

        var outcome = await SingleInstanceHandoffPolicy.SendAsync(
            HandoffRequest.Create(Link),
            async (attempt, ct) =>
            {
                if (attempts++ > 0)
                    await firstServed.Task.WaitAsync(TestWait, ct);
                return await SingleInstancePipeTransport.SendOnceAsync(pipe, attempt, TestWait, TestWait, TestWait, ct);
            },
            (_, _) => { },
            CancellationToken.None);

        var (first, second) = await server.WaitAsync(TestWait);
        Assert.Equal(HandoffOutcome.NotApplied, outcome);
        Assert.Equal(HandoffOutcome.NotApplied, first);
        Assert.Equal(HandoffOutcome.NotApplied, second);
        Assert.Equal(2, posted.Count);
        Assert.All(posted, dispatch => Assert.False(dispatch.Start()));
        Assert.Empty(applied);
    }

    [Fact]
    public async Task Malformed_request_is_dropped_without_a_reply()
    {
        var pipe = NewPipeName();
        var handled = false;

        var server = SingleInstancePipeTransport.ServeOneAsync(
            pipe,
            (_, _) =>
            {
                handled = true;
                return Task.FromResult(HandoffOutcome.Applied);
            },
            CancellationToken.None);

        using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(TestWait, CancellationToken.None);
        await client.WriteAsync(Encoding.UTF8.GetBytes("https://youtu.be/dQw4w9WgXcQ\n"));
        await client.FlushAsync();

        Assert.Null(await server.WaitAsync(TestWait));
        Assert.Equal(0, await client.ReadAsync(new byte[16]).AsTask().WaitAsync(TestWait));
        Assert.False(handled);
    }

    [Fact]
    public async Task Server_closes_its_end_only_after_the_sender_took_the_reply_and_closed()
    {
        // A pipe instance lives until its last handle closes. Closing after the sender keeps the
        // next one-instance server from finding the pipe "busy".
        var pipe = NewPipeName();
        var server = SingleInstancePipeTransport.ServeOneAsync(
            pipe, (_, _) => Task.FromResult(HandoffOutcome.Applied), TestWait, CancellationToken.None);

        var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await client.ConnectAsync(TestWait, CancellationToken.None);
            await client.WriteAsync(RequestLine(HandoffRequest.Create(Link)));
            using var reader = new StreamReader(client, Encoding.UTF8, false, 64, leaveOpen: true);
            Assert.Equal(
                SingleInstanceHandoffPolicy.AppliedReply,
                await reader.ReadLineAsync().WaitAsync(TestWait));

            await Task.Delay(200);
            Assert.False(server.IsCompleted, "The server must not close its end before the sender.");
        }
        finally
        {
            client.Dispose();
        }

        Assert.Equal(HandoffOutcome.Applied, await server.WaitAsync(TestWait));
        using var next = new NamedPipeServerStream(
            pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    }

    [Fact]
    public async Task Sender_that_never_takes_its_reply_cannot_hold_the_server()
    {
        var pipe = NewPipeName();
        var server = SingleInstancePipeTransport.ServeOneAsync(
            pipe, (_, _) => Task.FromResult(HandoffOutcome.Applied),
            TimeSpan.FromMilliseconds(200), CancellationToken.None);

        using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(TestWait, CancellationToken.None);
        await client.WriteAsync(RequestLine(HandoffRequest.Create(Link)));

        // The client neither reads the reply nor closes; the server still moves on.
        Assert.Equal(HandoffOutcome.Applied, await server.WaitAsync(TestWait));
    }

    [Fact]
    public async Task Running_instance_that_never_takes_the_request_cannot_hold_the_sender()
    {
        var pipe = NewPipeName();
        using var server = new NamedPipeServerStream(
            pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connected = server.WaitForConnectionAsync();

        var outcome = await SingleInstancePipeTransport.SendOnceAsync(
            pipe, HandoffRequest.Create(Link), TestWait,
            TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200),
            CancellationToken.None).WaitAsync(TestWait);

        Assert.Equal(HandoffOutcome.NoAcknowledgement, outcome);
        await connected.WaitAsync(TestWait);
    }

    [Fact]
    public async Task Sender_that_finds_no_running_instance_reports_it_unreachable()
    {
        var outcome = await SingleInstancePipeTransport.SendOnceAsync(
            NewPipeName(), HandoffRequest.Create(Link),
            TimeSpan.FromMilliseconds(100), TestWait, TestWait,
            CancellationToken.None).WaitAsync(TestWait);

        Assert.Equal(HandoffOutcome.Unreachable, outcome);
    }

    private static string NewPipeName() => $"PiPlay.Tests.Handoff.{Guid.NewGuid():N}";

    private static byte[] RequestLine(HandoffRequest request) =>
        Encoding.UTF8.GetBytes(SingleInstanceHandoffPolicy.FormatRequest(request) + "\n");

    private static void UnexpectedFailure(Exception ex) =>
        Assert.Fail($"The hand-off was not expected to fail: {ex}");

    private static Task<(HandoffOutcome? First, HandoffOutcome? Second)> ServeTwoAsync(
        string pipe,
        Func<HandoffRequest, CancellationToken, Task<HandoffOutcome>> handleAsync,
        TaskCompletionSource firstServed) =>
        Task.Run(async () =>
        {
            var first = await SingleInstancePipeTransport.ServeOneAsync(pipe, handleAsync, CancellationToken.None);
            // The first pipe instance is closed; the retry now rendezvous with the second.
            firstServed.TrySetResult();
            var second = await SingleInstancePipeTransport.ServeOneAsync(pipe, handleAsync, CancellationToken.None);
            return (first, second);
        });

    private static IHandoffDispatch RunNow(Action callback)
    {
        var dispatch = new ManualDispatch(callback);
        dispatch.Start();
        dispatch.Finish();
        return dispatch;
    }

    /// <summary>A UI-thread queue entry the test starts, finishes, or withdraws by hand.</summary>
    private sealed class ManualDispatch(Action callback) : IHandoffDispatch
    {
        private const int Queued = 0, Started = 1, Withdrawn = 2;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _state;

        public Task Completion => _completion.Task;

        public bool TryAbort()
        {
            if (Interlocked.CompareExchange(ref _state, Withdrawn, Queued) != Queued) return false;
            _completion.TrySetCanceled();
            return true;
        }

        /// <summary>The UI thread dequeues the entry; a withdrawn one never runs.</summary>
        public bool Start()
        {
            if (Interlocked.CompareExchange(ref _state, Started, Queued) != Queued) return false;
            callback();
            return true;
        }

        public void Finish() => _completion.TrySetResult();

        public void Fault(Exception ex) => _completion.TrySetException(ex);

        public void CancelByShutdown()
        {
            if (Interlocked.CompareExchange(ref _state, Withdrawn, Queued) == Queued)
                _completion.TrySetCanceled();
        }
    }
}

/// <summary>
/// The same contract against a real WPF dispatcher whose UI thread is stalled, so
/// <see cref="DispatcherOperation.Abort"/> is what keeps a withdrawn hand-off from running later.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Wpf)]
public class SingleInstanceHandoffDispatcherTests
{
    private const string Link = "https://youtu.be/dQw4w9WgXcQ";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handoff_that_throws_on_the_ui_thread_reaches_the_failure_callback_there(bool cancelledInside)
    {
        // A posted dispatcher operation keeps its exception in its task: nothing but the
        // failure callback would see it, and WPF would report a cancellation as an abort.
        using var ui = new DedicatedDispatcher();
        var failures = new ConcurrentQueue<(Exception Error, bool OnUiThread)>();
        Exception thrown = cancelledInside
            ? new OperationCanceledException("navigation cancelled")
            : new InvalidOperationException("navigation failed");

        var outcome = await SingleInstanceHandoffPolicy.DispatchAsync(
            HandoffRequest.Create(Link), new HandoffRequestLedger(),
            _ => throw thrown,
            ex => failures.Enqueue((ex, ui.Dispatcher.CheckAccess())),
            callback => DispatcherHandoffDispatch.Post(ui.Dispatcher, callback),
            TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(HandoffOutcome.Applied, outcome);
        var (error, onUiThread) = Assert.Single(failures);
        Assert.Same(thrown, error);
        Assert.True(onUiThread);
    }

    [Fact]
    public async Task Stalled_ui_thread_withdraws_the_handoff_and_its_same_id_retries_apply_it_once()
    {
        using var ui = new DedicatedDispatcher();
        var ledger = new HandoffRequestLedger();
        var applied = new List<string>(); // touched only on the UI thread
        var request = HandoffRequest.Create(Link);
        IHandoffDispatch Post(Action callback) => DispatcherHandoffDispatch.Post(ui.Dispatcher, callback);

        using var stallEntered = new ManualResetEventSlim();
        using var releaseStall = new ManualResetEventSlim();
        var stall = ui.Dispatcher.InvokeAsync(() =>
        {
            stallEntered.Set();
            releaseStall.Wait();
        });
        Assert.True(stallEntered.Wait(TimeSpan.FromSeconds(10)));

        var first = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, applied.Add, UnexpectedFailure, Post, TimeSpan.FromMilliseconds(100), CancellationToken.None);

        releaseStall.Set();
        await stall.Task;
        // Drain below Send priority: had the withdrawn hand-off stayed queued, it would run first.
        var appliedAfterStall = await ui.Dispatcher.InvokeAsync(() => applied.Count, DispatcherPriority.Normal).Task;

        var retry = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, applied.Add, UnexpectedFailure, Post, TimeSpan.FromSeconds(10), CancellationToken.None);
        var lateRetry = await SingleInstanceHandoffPolicy.DispatchAsync(
            request, ledger, applied.Add, UnexpectedFailure, Post, TimeSpan.FromSeconds(10), CancellationToken.None);
        var finalApplied = await ui.Dispatcher.InvokeAsync(() => applied.ToArray()).Task;

        Assert.Equal(HandoffOutcome.NotApplied, first);
        Assert.Equal(0, appliedAfterStall);
        Assert.Equal(HandoffOutcome.Applied, retry);
        Assert.Equal(HandoffOutcome.Applied, lateRetry);
        Assert.Equal([Link], finalApplied);
    }

    private static void UnexpectedFailure(Exception ex) =>
        Assert.Fail($"The hand-off was not expected to fail: {ex}");

    /// <summary>A private UI thread the test may block without stalling the shared WPF test thread.</summary>
    private sealed class DedicatedDispatcher : IDisposable
    {
        private readonly Thread _thread;

        public DedicatedDispatcher()
        {
            Dispatcher? dispatcher = null;
            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                ready.Set();
                System.Windows.Threading.Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "PiPlay-Handoff-Test-UI",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            if (!ready.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The hand-off test UI thread did not start.");
            Dispatcher = dispatcher!;
        }

        public Dispatcher Dispatcher { get; }

        public void Dispose()
        {
            Dispatcher.InvokeShutdown();
            _thread.Join(TimeSpan.FromSeconds(10));
        }
    }
}
