using System.IO;
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
}
