namespace SquadDash.Tests;

[TestFixture]
internal sealed class BackgroundCancelCompletionPolicyTests {
    [Test]
    public void ShouldForceFinalize_ReturnsTrue_WhenCancellationIsAcknowledged() {
        Assert.That(BackgroundCancelCompletionPolicy.ShouldForceFinalize(cancelAcknowledged: true), Is.True);
    }

    [Test]
    public void ShouldForceFinalize_ReturnsFalse_WhenCancellationIsNotAcknowledged() {
        Assert.That(BackgroundCancelCompletionPolicy.ShouldForceFinalize(cancelAcknowledged: false), Is.False);
    }
}
