namespace SquadDash.Tests;

[TestFixture]
internal sealed class InboxMessageParserTests {

    private const string MinimalJson = """
        {
          "subject": "README report",
          "from": "argus-weld",
          "body": "Done",
          "attachments": []
        }
        """;

    [Test]
    public void TryExtract_BareBlock_ParsesMessageAndStripsBody() {
        var text = $"""
            Report ready.

            INBOX_MESSAGE_JSON:
            {MinimalJson}
            """;

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.True);
        Assert.That(body, Is.EqualTo("Report ready."));
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Subject, Is.EqualTo("README report"));
        Assert.That(dto.From, Is.EqualTo("argus-weld"));
        Assert.That(dto.Body, Is.EqualTo("Done"));
    }

    [Test]
    public void TryExtract_FencedBlockAtEnd_ParsesMessageAndStripsFenceFromBody() {
        var text = $"""
            Report ready.

            ```
            INBOX_MESSAGE_JSON:
            {MinimalJson}
            ```
            """;

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.True);
        Assert.That(body, Is.EqualTo("Report ready."));
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Subject, Is.EqualTo("README report"));
    }

    /// <summary>
    /// Regression: model emits INBOX block then appends a prose/markdown summary after it.
    /// Parser must succeed and strip from the marker onwards.
    /// </summary>
    [Test]
    public void TryExtract_BlockInMiddleWithTrailingProse_ParsesAndStripsBlock() {
        var text = $"""
            Report ready.

            INBOX_MESSAGE_JSON:
            {MinimalJson}

            ## Summary
            **Tasks Found: 52** — see table above for details.
            """;

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.True);
        Assert.That(body, Is.EqualTo("Report ready."));
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Subject, Is.EqualTo("README report"));
    }

    [Test]
    public void TryExtract_BlockAtStartWithTrailingProse_ParsesAndBodyIsEmpty() {
        var text = $"""
            INBOX_MESSAGE_JSON:
            {MinimalJson}

            Some trailing summary text here.
            """;

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.True);
        Assert.That(body, Is.EqualTo(string.Empty));
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Subject, Is.EqualTo("README report"));
    }

    [Test]
    public void TryExtract_MultipleInboxBlocks_UsesLastOne() {
        const string text = """
            First pass:

            INBOX_MESSAGE_JSON:
            {
              "subject": "First",
              "from": "agent-a",
              "body": "old",
              "attachments": []
            }

            Second pass:

            INBOX_MESSAGE_JSON:
            {
              "subject": "README report",
              "from": "argus-weld",
              "body": "Done",
              "attachments": []
            }
            """;

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.True);
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Subject, Is.EqualTo("README report"), "Should use the LAST INBOX block");
    }

    /// <summary>
    /// The fenced-with-trailing-text scenario now parses successfully because the
    /// parser is intentionally tolerant of content after the closing brace.
    /// </summary>
    [Test]
    public void TryExtract_FencedBlockWithTrailingText_NowParses() {
        const string text = """
            Example:

            ```json
            INBOX_MESSAGE_JSON:
            { "subject": "Example", "from": "", "body": "", "attachments": [] }
            ```

            The real response continues here.
            """;

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.True);
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Subject, Is.EqualTo("Example"));
    }

    [Test]
    public void TryExtract_NoMarker_ReturnsFalse() {
        const string text = "Just some plain text without any marker.";

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.False);
        Assert.That(body, Is.EqualTo(text));
        Assert.That(dto, Is.Null);
    }
}
