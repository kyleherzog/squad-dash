using System.Threading.Tasks;

namespace SquadDash.Tests;

[TestFixture]
internal sealed class PostedUiActionTrackerTests {
    [Test]
    public async Task WaitForDrainAsync_CompletesImmediately_WhenNoActionsArePending() {
        var tracker = new PostedUiActionTracker();

        await tracker.WaitForDrainAsync();
    }

    [Test]
    public async Task WaitForDrainAsync_WaitsUntilRegisteredActionCompletes() {
        var tracker = new PostedUiActionTracker();
        var sequence = tracker.RegisterPostedAction();
        var waitTask = tracker.WaitForDrainAsync();

        Assert.That(waitTask.IsCompleted, Is.False);

        tracker.MarkCompleted(sequence);

        await waitTask;
    }

    [Test]
    public async Task WaitForDrainAsync_DoesNotComplete_WhenLaterActionCompletesFirst() {
        var tracker = new PostedUiActionTracker();
        var first = tracker.RegisterPostedAction();
        var second = tracker.RegisterPostedAction();
        var waitTask = tracker.WaitForDrainAsync();

        tracker.MarkCompleted(second);

        Assert.That(waitTask.IsCompleted, Is.False);

        tracker.MarkCompleted(first);

        await waitTask;
    }
}
