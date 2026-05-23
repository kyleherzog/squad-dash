namespace SquadDash.Tests;

[TestFixture]
internal sealed class TranscriptTextUtilitiesTests {

    [Test]
    public void SanitizeResponseText_InlineInboxSentinelMention_DoesNotStripText() {
        const string text = "The parser only accepts a bare `INBOX_MESSAGE_JSON:` line at the end.";

        var sanitized = TranscriptTextUtilities.SanitizeResponseText(text);

        Assert.That(sanitized, Is.EqualTo(text));
    }

    [Test]
    public void SanitizeResponseText_CodeFencedExampleWithTrailingText_DoesNotStripText() {
        const string text = """
            Example:

            ```json
            INBOX_MESSAGE_JSON:
            { "subject": "Example" }
            ```

            The real response continues here.
            """;

        var sanitized = TranscriptTextUtilities.SanitizeResponseText(text);

        Assert.That(sanitized, Is.EqualTo(text.TrimEnd()));
    }

    [Test]
    public void SanitizeResponseText_TopLevelPartialInboxBlock_StripsWhileStreaming() {
        const string text = """
            Report ready.

            INBOX_MESSAGE_JSON:
            {
            """;

        var sanitized = TranscriptTextUtilities.SanitizeResponseText(text);

        Assert.That(sanitized, Is.EqualTo("Report ready."));
    }

    [Test]
    public void SanitizeResponseText_FencedInboxBlockAtEnd_StripsDeliveredPayload() {
        const string text = """
            Report ready.

            ```
            INBOX_MESSAGE_JSON:
            {
              "subject": "README report",
              "from": "argus-weld",
              "body": "Done",
              "attachments": []
            }
            ```
            """;

        var sanitized = TranscriptTextUtilities.SanitizeResponseText(text);

        Assert.That(sanitized, Is.EqualTo("Report ready."));
    }
}
