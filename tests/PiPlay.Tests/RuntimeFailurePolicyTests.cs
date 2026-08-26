using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using PiPlay.Models;
using PiPlay.Services;

namespace PiPlay.Tests;

[Trait(TestCategories.Key, TestCategories.Logic)]
public class RuntimeFailurePolicyTests
{
    [Fact]
    public void Consecutive_failure_gate_logs_once_and_reports_suppressed_repeats_on_recovery()
    {
        var gate = new ConsecutiveFailureGate();

        Assert.True(gate.RecordFailure());
        for (var i = 0; i < 99; i++)
            Assert.False(gate.RecordFailure());

        Assert.Equal(99, gate.RecordSuccess());
        Assert.Null(gate.RecordSuccess());
        Assert.True(gate.RecordFailure());
        Assert.Equal(0, gate.RecordSuccess());
        Assert.Null(gate.RecordSuccess());
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", false)]
    [InlineData("", false)]
    public async Task Source_suppression_requires_an_explicit_true_script_result(
        string scriptResult,
        bool expected)
    {
        string? capturedScript = null;

        var acknowledged = await YouTubeDomBridge.SuppressPlaybackAsync(script =>
        {
            capturedScript = script;
            return Task.FromResult(scriptResult);
        });

        Assert.Equal(expected, acknowledged);
        Assert.Contains("v.muted", capturedScript, StringComparison.Ordinal);
        Assert.Contains("v.pause", capturedScript, StringComparison.Ordinal);
        Assert.Contains("return v.muted === true && v.paused === true", capturedScript,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_suppression_reports_false_when_the_script_executor_throws()
    {
        var acknowledged = await YouTubeDomBridge.SuppressPlaybackAsync(
            _ => Task.FromException<string>(new InvalidOperationException("renderer unavailable")));

        Assert.False(acknowledged);
    }

    [Fact]
    public async Task Source_suppression_reports_false_when_the_script_executor_never_completes()
    {
        var neverCompletes = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var acknowledged = await YouTubeDomBridge.SuppressPlaybackAsync(
                _ => neverCompletes.Task,
                TimeSpan.FromMilliseconds(25))
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(acknowledged);
    }

    [Fact]
    public void Runtime_deadlines_match_the_accepted_coordination_contract()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), YouTubeDomBridge.ExecutionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), SingleInstancePipePolicy.ClientReadTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), DispatcherFaultPolicy.RepeatDialogWindow);
    }

    [Fact]
    public async Task Runtime_deadline_keeps_a_non_cancellable_operation_single_flight()
    {
        using var gate = new SemaphoreSlim(1, 1);
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var first = AsyncOperationDeadline.RunSingleFlightAsync(
            gate,
            () =>
            {
                firstStarted.TrySetResult();
                return releaseFirst.Task;
            },
            TimeSpan.FromMilliseconds(25));

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<TimeoutException>(() => first);

        var duplicateStarts = 0;
        await Assert.ThrowsAsync<TimeoutException>(() =>
            AsyncOperationDeadline.RunSingleFlightAsync(
                gate,
                () =>
                {
                    duplicateStarts++;
                    return Task.FromResult("duplicate");
                },
                TimeSpan.FromMilliseconds(25)));
        Assert.Equal(0, duplicateStarts);

        releaseFirst.SetResult("first");
        var recovered = await AsyncOperationDeadline.RunSingleFlightAsync(
            gate,
            () => Task.FromResult("recovered"),
            TimeSpan.FromSeconds(2));

        Assert.Equal("recovered", recovered);
    }

    [Fact]
    public async Task Popout_launch_precondition_stops_code_after_unacknowledged_suppression()
    {
        var launchContinued = false;

        async Task AttemptLaunchAsync()
        {
            await PopoutLaunchPolicy.RequireAcknowledgedSourceSuppressionAsync(
                () => Task.FromResult(false));
            launchContinued = true;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(AttemptLaunchAsync);
        Assert.False(launchContinued);
    }

    [Fact]
    public async Task Popout_launch_precondition_allows_code_after_acknowledged_suppression()
    {
        var launchContinued = false;

        await PopoutLaunchPolicy.RequireAcknowledgedSourceSuppressionAsync(
            () => Task.FromResult(true));
        launchContinued = true;

        Assert.True(launchContinued);
    }

    // Launch gate (spec 13.1 / 22.1): a playlist page is a launchable popout target — the popout
    // opens the playlist and starts its first playable item. Only a target with nothing playable
    // at all keeps the "open a video first" prompt.
    [Fact]
    public void Popout_launch_gate_accepts_video_and_playlist_only_targets()
    {
        Assert.True(PopoutLaunchPolicy.IsLaunchableTarget(new YouTubeTarget { VideoId = "dQw4w9WgXcQ" }));
        Assert.True(PopoutLaunchPolicy.IsLaunchableTarget(
            new YouTubeTarget { PlaylistId = "PL0123456789", IsPlaylistOnly = true }));
        Assert.False(PopoutLaunchPolicy.IsLaunchableTarget(null));
        Assert.False(PopoutLaunchPolicy.IsLaunchableTarget(new YouTubeTarget()));
    }

    // A playlist page has no guaranteed <video> element, so "no video found" is a legitimate
    // suppression outcome there, not a failed ownership transfer — the acknowledged-suppression
    // contract (fail-closed abort) applies only when the source is on a concrete video. That holds
    // even after the launch resolves the page's first playable item onto the target: the id names
    // what the POPOUT will start, not anything audible on the source's browse page.
    [Fact]
    public void Only_video_targets_require_acknowledged_suppression()
    {
        Assert.True(PopoutLaunchPolicy.RequiresAcknowledgedSuppression(new YouTubeTarget { VideoId = "dQw4w9WgXcQ" }));
        Assert.False(PopoutLaunchPolicy.RequiresAcknowledgedSuppression(
            new YouTubeTarget { PlaylistId = "PL0123456789", IsPlaylistOnly = true }));
        Assert.False(PopoutLaunchPolicy.RequiresAcknowledgedSuppression(new YouTubeTarget
        {
            VideoId = "dQw4w9WgXcQ",
            PlaylistId = "PL0123456789",
            IsPlaylistOnly = true,
        }));
    }

    [Fact]
    public void Browser_clear_coordinator_keeps_one_incomplete_task()
    {
        var coordinator = new BrowserDataClearCoordinator();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;

        Assert.True(coordinator.TryStart(() =>
        {
            starts++;
            return completion.Task;
        }, out var first));

        Assert.False(coordinator.TryStart(() =>
        {
            starts++;
            return Task.CompletedTask;
        }, out var duplicate));

        Assert.Same(first, duplicate);
        Assert.Equal(1, starts);
        Assert.True(coordinator.IsRunning);
    }

    [Fact]
    public async Task Browser_clear_coordinator_releases_after_late_success_and_allows_one_new_start()
    {
        var coordinator = new BrowserDataClearCoordinator();
        var firstCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;

        Assert.True(coordinator.TryStart(() =>
        {
            starts++;
            return firstCompletion.Task;
        }, out var first));

        firstCompletion.SetResult();
        await first;
        Assert.False(coordinator.IsRunning);

        Assert.True(coordinator.TryStart(() =>
        {
            starts++;
            return Task.CompletedTask;
        }, out var second));
        await second;

        Assert.Equal(2, starts);
    }

    [Fact]
    public async Task Browser_clear_coordinator_observes_late_fault_and_releases_the_gate()
    {
        var coordinator = new BrowserDataClearCoordinator();
        var firstCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(coordinator.TryStart(() => firstCompletion.Task, out var first));
        firstCompletion.SetException(new InvalidOperationException("late clear failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        Assert.False(coordinator.IsRunning);
        Assert.True(coordinator.TryStart(() => Task.CompletedTask, out _));
    }

    [Fact]
    public void Browser_clear_coordinator_does_not_latch_a_synchronously_throwing_factory()
    {
        var coordinator = new BrowserDataClearCoordinator();

        Assert.Throws<InvalidOperationException>(() =>
            coordinator.TryStart(
                () => throw new InvalidOperationException("synchronous factory failure"), out _));

        Assert.False(coordinator.IsRunning);
        Assert.True(coordinator.TryStart(() => Task.CompletedTask, out var retry));
        Assert.True(retry.IsCompletedSuccessfully);
    }

    [Fact]
    public void Browser_clear_coordinator_does_not_retain_an_already_completed_task()
    {
        var coordinator = new BrowserDataClearCoordinator();
        static Task NewCompletedTask()
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            completion.SetResult();
            return completion.Task;
        }

        Assert.True(coordinator.TryStart(NewCompletedTask, out var first));
        Assert.True(first.IsCompletedSuccessfully);
        Assert.False(coordinator.IsRunning);
        Assert.True(coordinator.TryStart(NewCompletedTask, out var second));
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Browser_clear_coordinator_coalesces_concurrent_starts()
    {
        var coordinator = new BrowserDataClearCoordinator();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = new bool[16];
        var operations = new Task[16];
        var factoryCalls = 0;

        Parallel.For(0, starts.Length, i =>
        {
            starts[i] = coordinator.TryStart(() =>
            {
                Interlocked.Increment(ref factoryCalls);
                return completion.Task;
            }, out operations[i]);
        });

        Assert.Equal(1, starts.Count(started => started));
        Assert.Equal(1, factoryCalls);
        Assert.All(operations, operation => Assert.Same(completion.Task, operation));
        completion.SetResult();
    }

    [Fact]
    public void Browser_clear_wait_policy_distinguishes_timeout_from_completion_race()
    {
        var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = Task.CompletedTask;

        Assert.True(BrowserDataClearCoordinator.DidForegroundWaitExpire(operation.Task, timeout));

        // The timer may have won WhenAny while the operation completed before the UI continuation
        // resumed. That is a terminal operation, not a background timeout.
        operation.SetResult();
        Assert.False(BrowserDataClearCoordinator.DidForegroundWaitExpire(operation.Task, timeout));
        Assert.False(BrowserDataClearCoordinator.DidForegroundWaitExpire(operation.Task, operation.Task));
    }

    [Fact]
    public async Task Browser_clear_wait_policy_routes_an_operation_timeout_fault_as_failure()
    {
        var operation = Task.FromException(new TimeoutException("operation fault"));

        Assert.False(BrowserDataClearCoordinator.DidForegroundWaitExpire(
            operation, Task.CompletedTask));
        await Assert.ThrowsAsync<TimeoutException>(() => operation);
    }

    [Fact]
    public void Pipe_identity_is_stable_within_a_session_and_distinct_across_sessions()
    {
        var first = SingleInstancePipePolicy.BuildPipeName("Stable", sessionId: 12);

        Assert.Equal(first, SingleInstancePipePolicy.BuildPipeName("Stable", sessionId: 12));
        Assert.NotEqual(first, SingleInstancePipePolicy.BuildPipeName("Stable", sessionId: 13));
        Assert.NotEqual(first, SingleInstancePipePolicy.BuildPipeName("Default", sessionId: 12));
    }

    [Fact]
    public void Pipe_retry_delay_is_exponential_and_capped()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(250), SingleInstancePipePolicy.RetryDelay(1));
        Assert.Equal(TimeSpan.FromMilliseconds(500), SingleInstancePipePolicy.RetryDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(16), SingleInstancePipePolicy.RetryDelay(7));
        Assert.Equal(TimeSpan.FromSeconds(30), SingleInstancePipePolicy.RetryDelay(8));
        Assert.Equal(TimeSpan.FromSeconds(30), SingleInstancePipePolicy.RetryDelay(100));
    }

    [Fact]
    public async Task Pipe_payload_read_times_out_and_cancels_a_silent_client()
    {
        var readCancelled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<string> ReadAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return string.Empty;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                readCancelled.TrySetResult();
                throw;
            }
        }

        await Assert.ThrowsAsync<TimeoutException>(() =>
            SingleInstancePipePolicy.ReadClientPayloadAsync(
                ReadAsync,
                TimeSpan.FromMilliseconds(25),
                CancellationToken.None));
        await readCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Pipe_loop_does_not_retry_until_the_injected_delay_finishes()
    {
        using var cancellation = new CancellationTokenSource();
        var delayEntered = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;

        var run = SingleInstancePipePolicy.RunAsync(
            attemptAsync: _ =>
            {
                attempts++;
                return Task.FromException(new IOException("pipe unavailable"));
            },
            delayAsync: async (delay, token) =>
            {
                delayEntered.TrySetResult(delay);
                await releaseDelay.Task.WaitAsync(token);
            },
            onFirstFailure: _ => { },
            onRecovery: _ => { },
            cancellation.Token);

        Assert.Equal(TimeSpan.FromMilliseconds(250),
            await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, attempts);

        cancellation.Cancel();
        releaseDelay.TrySetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Pipe_loop_resets_backoff_after_a_successful_handoff()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var recoveries = new List<int>();

        await SingleInstancePipePolicy.RunAsync(
            attemptAsync: _ =>
            {
                attempts++;
                return attempts switch
                {
                    1 => Task.FromException(new IOException("first episode")),
                    2 => Task.CompletedTask,
                    _ => Task.FromException(new IOException("second episode")),
                };
            },
            delayAsync: (delay, _) =>
            {
                delays.Add(delay);
                if (delays.Count == 2) cancellation.Cancel();
                return Task.CompletedTask;
            },
            onFirstFailure: _ => { },
            onRecovery: recoveries.Add,
            cancellation.Token);

        Assert.Equal([TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250)], delays);
        Assert.Equal([1], recoveries);
    }

    [Fact]
    public async Task Pipe_loop_treats_cancellation_as_shutdown_not_failure()
    {
        using var cancellation = new CancellationTokenSource();
        var failures = 0;
        var run = SingleInstancePipePolicy.RunAsync(
            attemptAsync: token => Task.Delay(Timeout.InfiniteTimeSpan, token),
            delayAsync: Task.Delay,
            onFirstFailure: _ => failures++,
            onRecovery: _ => { },
            cancellation.Token);

        cancellation.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, failures);
    }

    [Fact]
    public async Task Pipe_loop_treats_an_unrelated_operation_cancellation_as_a_retryable_failure()
    {
        using var cancellation = new CancellationTokenSource();
        var failures = 0;
        var delays = 0;

        await SingleInstancePipePolicy.RunAsync(
            attemptAsync: _ => Task.FromException(new OperationCanceledException("not app shutdown")),
            delayAsync: (_, _) =>
            {
                delays++;
                cancellation.Cancel();
                return Task.CompletedTask;
            },
            onFirstFailure: _ => failures++,
            onRecovery: _ => { },
            cancellation.Token);

        Assert.Equal(1, failures);
        Assert.Equal(1, delays);
    }

    // --- Dispatcher fault policy (spec 15.4 / Q-6) ---

    // Two throw sites so a signature can be shown to track the site, not the message text.
    private static Exception RenderFault(string message)
    {
        try { throw new InvalidOperationException(message); }
        catch (InvalidOperationException ex) { return ex; }
    }

    private static Exception TimerFault(string message)
    {
        try { throw new InvalidOperationException(message); }
        catch (InvalidOperationException ex) { return ex; }
    }

    [Fact]
    public void Dispatcher_fault_keeps_the_app_alive_and_reports_it_once()
    {
        var policy = new DispatcherFaultPolicy(() => TimeSpan.Zero);

        var decision = policy.Evaluate(RenderFault("first"), isShuttingDown: false);

        Assert.True(decision.Handled);
        Assert.True(decision.ShowDialog);
        Assert.NotEmpty(decision.Signature);
    }

    [Fact]
    public void Dispatcher_fault_behind_an_open_dialog_is_swallowed_without_a_second_dialog()
    {
        var policy = new DispatcherFaultPolicy(() => TimeSpan.Zero);
        Assert.True(policy.Evaluate(RenderFault("first"), isShuttingDown: false).ShowDialog);

        // The dispatcher keeps pumping behind a modal, so an unrelated fault re-enters the handler.
        var reentrant = policy.Evaluate(TimerFault("while the dialog is up"), isShuttingDown: false);

        Assert.True(reentrant.Handled);
        Assert.False(reentrant.ShowDialog);
    }

    [Fact]
    public void Dispatcher_fault_repeat_is_silent_inside_the_window_and_reported_after_it()
    {
        var now = TimeSpan.Zero;
        var policy = new DispatcherFaultPolicy(() => now);

        Assert.True(policy.Evaluate(RenderFault("tick"), isShuttingDown: false).ShowDialog);
        policy.DialogClosed();

        now += DispatcherFaultPolicy.RepeatDialogWindow - TimeSpan.FromMilliseconds(1);
        var suppressed = policy.Evaluate(RenderFault("tick"), isShuttingDown: false);
        Assert.True(suppressed.Handled);
        Assert.False(suppressed.ShowDialog);

        now += TimeSpan.FromMilliseconds(2);
        Assert.True(policy.Evaluate(RenderFault("tick"), isShuttingDown: false).ShowDialog);
    }

    [Fact]
    public void Dispatcher_fault_with_a_different_signature_gets_its_own_dialog()
    {
        var policy = new DispatcherFaultPolicy(() => TimeSpan.Zero);

        Assert.True(policy.Evaluate(RenderFault("render"), isShuttingDown: false).ShowDialog);
        policy.DialogClosed();

        // Same instant, different throw site: a new problem is worth telling the user about.
        Assert.True(policy.Evaluate(TimerFault("timer"), isShuttingDown: false).ShowDialog);
    }

    [Fact]
    public void Dispatcher_fault_signature_tracks_the_throw_site_not_the_message()
    {
        var policy = new DispatcherFaultPolicy(() => TimeSpan.Zero);

        var first = policy.Evaluate(RenderFault("frame 41 failed"), isShuttingDown: false);
        policy.DialogClosed();
        var sameSite = policy.Evaluate(RenderFault("frame 42 failed"), isShuttingDown: false);

        // Varying detail in the message must not defeat suppression of one repeating fault.
        Assert.Equal(first.Signature, sameSite.Signature);
        Assert.False(sameSite.ShowDialog);

        // Identical message text from a different site is a different fault.
        Assert.NotEqual(
            first.Signature,
            policy.Evaluate(TimerFault("frame 41 failed"), isShuttingDown: false).Signature);
    }

    [Fact]
    public void Dispatcher_fault_coalesced_behind_a_dialog_does_not_take_over_the_suppression_slot()
    {
        var now = TimeSpan.Zero;
        var policy = new DispatcherFaultPolicy(() => now);

        Assert.True(policy.Evaluate(RenderFault("storm"), isShuttingDown: false).ShowDialog);
        Assert.False(policy.Evaluate(TimerFault("passer-by"), isShuttingDown: false).ShowDialog);
        policy.DialogClosed();

        // The passer-by must not have displaced "storm"; otherwise the storm resumes on dismissal.
        Assert.False(policy.Evaluate(RenderFault("storm"), isShuttingDown: false).ShowDialog);
    }

    [Fact]
    public void Dispatcher_fault_quiet_period_runs_from_dismissal_not_from_display()
    {
        var now = TimeSpan.Zero;
        var policy = new DispatcherFaultPolicy(() => now);

        Assert.True(policy.Evaluate(RenderFault("stuck"), isShuttingDown: false).ShowDialog);
        now += TimeSpan.FromMinutes(5);          // the user left the dialog up
        policy.DialogClosed();

        now += TimeSpan.FromSeconds(1);
        Assert.False(policy.Evaluate(RenderFault("stuck"), isShuttingDown: false).ShowDialog);
    }

    [Fact]
    public void Fatal_dispatcher_faults_are_reported_but_never_swallowed()
    {
        // StackOverflow/AccessViolation cannot actually reach a managed handler on modern .NET;
        // they are classified for completeness, so this pins the policy, not runtime delivery.
        Exception[] fatal =
        [
            new OutOfMemoryException(),
            new StackOverflowException(),
            new AccessViolationException(),
            new SEHException(),
        ];

        foreach (var exception in fatal)
        {
            var policy = new DispatcherFaultPolicy(() => TimeSpan.Zero);
            var decision = policy.Evaluate(exception, isShuttingDown: false);

            Assert.False(decision.Handled);
            Assert.True(decision.ShowDialog);
        }

        // COMException is a sibling of SEHException, not a subclass: routine WebView2 interop
        // failures must stay recoverable.
        var interop = new DispatcherFaultPolicy(() => TimeSpan.Zero);
        Assert.True(interop.Evaluate(new COMException("RPC_E_DISCONNECTED"), isShuttingDown: false).Handled);
    }

    [Fact]
    public void Fatal_dispatcher_fault_is_recognized_through_the_inner_exception_chain()
    {
        var policy = new DispatcherFaultPolicy(() => TimeSpan.Zero);
        var wrapped = new TargetInvocationException(
            "binding callback failed", new OutOfMemoryException());

        Assert.False(policy.Evaluate(wrapped, isShuttingDown: false).Handled);
    }

    [Fact]
    public void Dispatcher_fault_during_shutdown_is_swallowed_without_a_dialog()
    {
        var policy = new DispatcherFaultPolicy(() => TimeSpan.Zero);

        var decision = policy.Evaluate(RenderFault("closing"), isShuttingDown: true);

        // Rethrowing here would let the fault escape Dispatcher.Run, skipping OnExit cleanup and
        // turning an ordinary exit into a WER crash. A modal would just block the exit.
        Assert.True(decision.Handled);
        Assert.False(decision.ShowDialog);
    }

    [Fact]
    public void Fatal_dispatcher_fault_during_shutdown_is_still_swallowed()
    {
        var policy = new DispatcherFaultPolicy(() => TimeSpan.Zero);

        // Shutdown wins over the fatal classification: the process is already going away, so there
        // is nothing left to protect by letting it terminate the hard way.
        var decision = policy.Evaluate(new OutOfMemoryException(), isShuttingDown: true);

        Assert.True(decision.Handled);
        Assert.False(decision.ShowDialog);
    }

    [Fact]
    public void Dispatcher_fault_dialog_release_is_idempotent()
    {
        var now = TimeSpan.Zero;
        var policy = new DispatcherFaultPolicy(() => now);

        Assert.True(policy.Evaluate(RenderFault("once"), isShuttingDown: false).ShowDialog);
        policy.DialogClosed();
        now += DispatcherFaultPolicy.RepeatDialogWindow;
        policy.DialogClosed();   // a second release must not re-stamp the quiet period

        Assert.True(policy.Evaluate(RenderFault("once"), isShuttingDown: false).ShowDialog);
    }
}
