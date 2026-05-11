namespace SquadDash.Tests;

[TestFixture]
internal sealed class ToolTranscriptOutputLimiterTests {
    [Test]
    public void TrimForLiveTranscript_ReturnsNull_ForWhitespace() {
        var descriptor = new ToolTranscriptDescriptor("powershell");

        var result = ToolTranscriptOutputLimiter.TrimForLiveTranscript(descriptor, "   ");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void TrimForLiveTranscript_UsesViewLimit_ForViewTools() {
        var descriptor = new ToolTranscriptDescriptor("view");
        var text = new string('x', ToolTranscriptOutputLimiter.ViewToolLiveOutputCharLimit + 50);

        var result = ToolTranscriptOutputLimiter.TrimForLiveTranscript(descriptor, text);

        Assert.Multiple(() => {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!, Does.StartWith(new string('x', ToolTranscriptOutputLimiter.ViewToolLiveOutputCharLimit)));
            Assert.That(result, Does.Contain("SquadDash truncated 50 characters"));
        });
    }

    [Test]
    public void TrimForLiveTranscript_UsesDefaultLimit_ForNonViewTools() {
        var descriptor = new ToolTranscriptDescriptor("powershell");
        var text = new string('x', ToolTranscriptOutputLimiter.DefaultLiveOutputCharLimit + 25);

        var result = ToolTranscriptOutputLimiter.TrimForLiveTranscript(descriptor, text);

        Assert.Multiple(() => {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!, Does.StartWith(new string('x', ToolTranscriptOutputLimiter.DefaultLiveOutputCharLimit)));
            Assert.That(result, Does.Contain("SquadDash truncated 25 characters"));
        });
    }
}
