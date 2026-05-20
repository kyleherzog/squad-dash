using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SquadDash.Tests;

/// <summary>
/// Behavioral specs for <see cref="IdleDetectionService"/>.
/// Tests will compile once Arjun Sen's Phase 1 implementation lands.
/// </summary>
[TestFixture]
internal sealed class IdleDetectionServiceTests {

    private IdleDetectionService _service = null!;

    [SetUp]
    public void SetUp() {
        _service = new IdleDetectionService();
    }

    [TearDown]
    public void TearDown() {
        _service.Stop();
    }

    // Very small threshold so tests complete in milliseconds.
    private const double ThresholdMinutes = 0.002; // ≈ 120 ms

    // ── Suppression while prompt is running ───────────────────────────────────

    [Test]
    public async Task IdleThresholdReached_NotFiredWhilePromptIsRunning() {
        var fired = false;
        _service.IdleThresholdReached += () => fired = true;

        _service.SetPromptActive(true);
        _service.Start(ThresholdMinutes);

        await Task.Delay(400);

        Assert.That(fired, Is.False, "IdleThresholdReached must not fire while a prompt is running");
    }

    // ── Suppression while loop is running ─────────────────────────────────────

    [Test]
    public async Task IdleThresholdReached_NotFiredWhileLoopIsRunning() {
        var fired = false;
        _service.IdleThresholdReached += () => fired = true;

        _service.SetLoopActive(true);
        _service.Start(ThresholdMinutes);

        await Task.Delay(400);

        Assert.That(fired, Is.False, "IdleThresholdReached must not fire while the loop is running");
    }

    // ── Normal idle fire ──────────────────────────────────────────────────────

    [Test]
    public async Task IdleThresholdReached_FiredAfterThresholdWhenBothInactive() {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.IdleThresholdReached += () => tcs.TrySetResult();

        _service.Start(ThresholdMinutes);

        var fired = await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task;
        Assert.That(fired, Is.True, "IdleThresholdReached should fire after the threshold elapses");
    }

    // ── RecordActivity resets timer ───────────────────────────────────────────

    [Test]
    public async Task RecordActivity_ResetsIdleTimer() {
        // Use a slightly longer threshold so we can reset it multiple times
        // before allowing it to expire.
        const double longerThresholdMinutes = 0.005; // ≈ 300 ms
        var fireCount = 0;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.IdleThresholdReached += () => { fireCount++; tcs.TrySetResult(); };

        _service.Start(longerThresholdMinutes);

        // Reset the timer several times within the threshold window.
        for (int i = 0; i < 4; i++) {
            await Task.Delay(50);
            _service.RecordActivity();
        }

        // Allow sufficient time for the threshold to expire from the last reset.
        var fired = await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task;
        Assert.That(fired, Is.True, "Event should fire once the threshold elapses after last activity");
        Assert.That(fireCount, Is.EqualTo(1), "Should fire exactly once, not on every activity");
    }

    // ── ForceIdle — immediate when both idle ──────────────────────────────────

    [Test]
    public async Task ForceIdle_WhenQueueAndLoopInactive_TriggersImmediately() {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.IdleThresholdReached += () => tcs.TrySetResult();

        // Use a long threshold so the natural timer never fires during the test.
        _service.Start(thresholdMinutes: 60);
        _service.ForceIdle();

        var fired = await Task.WhenAny(tcs.Task, Task.Delay(500)) == tcs.Task;
        Assert.That(fired, Is.True, "ForceIdle() should trigger IdleThresholdReached without waiting for the timer");
    }

    // ── ForceIdle — deferred while loop is running ────────────────────────────

    [Test]
    public async Task ForceIdle_WhenLoopRunning_DefersUntilLoopStops() {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.IdleThresholdReached += () => tcs.TrySetResult();

        _service.SetLoopActive(true);
        _service.Start(thresholdMinutes: 60);
        _service.ForceIdle();

        // Should NOT fire while the loop is still active.
        await Task.Delay(200);
        Assert.That(tcs.Task.IsCompleted, Is.False, "ForceIdle() must not fire while the loop is still running");

        // Stopping the loop should release the deferred trigger.
        _service.SetLoopActive(false);
        var firedAfterStop = await Task.WhenAny(tcs.Task, Task.Delay(1000)) == tcs.Task;
        Assert.That(firedAfterStop, Is.True, "Deferred ForceIdle() should fire once the loop stops");
    }

    // ── SetRunnerActive(true) suppresses re-triggering ────────────────────────

    [Test]
    public async Task SetRunnerActive_True_SuppressesRetriggering() {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.IdleThresholdReached += () => tcs.TrySetResult();
        _service.Start(ThresholdMinutes);

        // Wait for the first natural fire.
        var firstFire = await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task;
        Assert.That(firstFire, Is.True, "Precondition: first fire should occur");

        // Runner picks up the idle signal and marks itself active.
        _service.SetRunnerActive(true);

        var secondTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.IdleThresholdReached += () => secondTcs.TrySetResult();

        // The threshold period elapses again, but the runner is still active.
        var refiredWhileRunnerActive = await Task.WhenAny(secondTcs.Task, Task.Delay(400)) == secondTcs.Task;
        Assert.That(refiredWhileRunnerActive, Is.False,
            "IdleThresholdReached must not re-fire while SetRunnerActive(true)");
    }

    // ── SetRunnerActive(false) re-enables triggering ──────────────────────────

    [Test]
    public async Task SetRunnerActive_False_ReEnablesTriggering() {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.IdleThresholdReached += () => tcs.TrySetResult();

        _service.SetRunnerActive(true);
        _service.Start(ThresholdMinutes);

        // Threshold passes while runner is marked active — no fire expected.
        await Task.Delay(300);
        Assert.That(tcs.Task.IsCompleted, Is.False, "Precondition: should not fire while runner is active");

        // Runner finishes; idle detection should re-arm.
        _service.SetRunnerActive(false);

        var firedAfterRunnerDone = await Task.WhenAny(tcs.Task, Task.Delay(1000)) == tcs.Task;
        Assert.That(firedAfterRunnerDone, Is.True,
            "IdleThresholdReached should fire after SetRunnerActive(false) when the threshold elapses");
    }

    // ── Thread safety ─────────────────────────────────────────────────────────

    [Test]
    public void ConcurrentRecordActivity_DoesNotCauseDataRaces() {
        _service.Start(thresholdMinutes: 0.1); // 6 s — long enough to avoid natural fire
        var threads = new Thread[20];
        Exception? caught = null;

        for (int i = 0; i < threads.Length; i++) {
            threads[i] = new Thread(() => {
                try {
                    for (int j = 0; j < 200; j++)
                        _service.RecordActivity();
                }
                catch (Exception ex) {
                    Interlocked.CompareExchange(ref caught, ex, null);
                }
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join(TimeSpan.FromSeconds(10));

        Assert.That(caught, Is.Null,
            $"Concurrent RecordActivity() calls must not throw; got: {caught?.Message}");
    }
}
