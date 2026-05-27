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
    public void SanitizeResponseText_BacktickInlineWithJson_DoesNotStripOrLeaveOrphanBacktick() {
        // An inline code span like `INBOX_MESSAGE_JSON: { "from": "argus-weld" }` is NOT a real
        // block — it should be left fully intact, not stripped to a lone backtick.
        const string text = "See this example: `INBOX_MESSAGE_JSON: { \"from\": \"argus-weld\" }` for details.";

        var sanitized = TranscriptTextUtilities.SanitizeResponseText(text);

        Assert.That(sanitized, Is.EqualTo(text));
    }

    [Test]
    public void SanitizeResponseText_BacktickInlineFollowedByTopLevelBlock_StripsOnlyBlock() {
        // When there is BOTH an inline backtick mention AND a real top-level block, only the
        // real block should be stripped. The inline mention should survive.
        const string text = """
            Example syntax: `INBOX_MESSAGE_JSON: {...}` — use this format.

            INBOX_MESSAGE_JSON:
            { "subject": "Real", "from": "argus-weld", "body": "Done", "attachments": [] }
            """;

        var sanitized = TranscriptTextUtilities.SanitizeResponseText(text);

        Assert.That(sanitized, Does.Contain("`INBOX_MESSAGE_JSON:"));
        Assert.That(sanitized, Does.Not.Contain("\"subject\": \"Real\""));
    }

    /// <summary>
    /// Previously this was expected not to strip (old end-anchor behavior). Now that the parser
    /// is intentionally tolerant of trailing prose, even a fenced example block is stripped.
    /// </summary>
    [Test]
    public void SanitizeResponseText_CodeFencedExampleWithTrailingText_NowStrips() {
        const string text = """
            Example:

            ```json
            INBOX_MESSAGE_JSON:
            { "subject": "Example", "from": "", "body": "", "attachments": [] }
            ```

            The real response continues here.
            """;

        var sanitized = TranscriptTextUtilities.SanitizeResponseText(text);

        Assert.That(sanitized, Is.EqualTo("Example:"));
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

    [Test]
    public void MergeStreamingAndFinalResponse_AppendsTail_WhenFinalStartsWithStreamedText() {
        const string streamed = "Okay, now I have everything I need to solve the problem.";
        const string final = """
            Okay, now I have everything I need to solve the problem.

            ## Findings

            The report continues with the actual analysis.
            """;

        var merged = TranscriptTextUtilities.MergeStreamingAndFinalResponse(
            streamed,
            final,
            out var tail);

        Assert.Multiple(() => {
            Assert.That(merged, Is.EqualTo(final));
            Assert.That(tail, Does.Contain("## Findings"));
        });
    }

    [Test]
    public void MergeStreamingAndFinalResponse_UsesLongerFinal_WhenStreamedTextIsIncomplete() {
        const string streamed = "Short partial opening.";
        const string final = "Full final response with more detail, but a slightly different opening.";

        var merged = TranscriptTextUtilities.MergeStreamingAndFinalResponse(
            streamed,
            final,
            out var tail);

        Assert.Multiple(() => {
            Assert.That(merged, Is.EqualTo(final));
            Assert.That(tail, Is.Null);
        });
    }
}
