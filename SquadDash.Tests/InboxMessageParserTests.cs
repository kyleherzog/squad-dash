namespace SquadDash.Tests;

[TestFixture]
internal sealed class InboxMessageParserTests {

    [Test]
    public void TryExtract_BareBlock_ParsesMessageAndStripsBody() {
        const string text = """
            Report ready.

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
        Assert.That(body, Is.EqualTo("Report ready."));
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Subject, Is.EqualTo("README report"));
        Assert.That(dto.From, Is.EqualTo("argus-weld"));
        Assert.That(dto.Body, Is.EqualTo("Done"));
    }

    [Test]
    public void TryExtract_FencedBlockAtEnd_ParsesMessageAndStripsFenceFromBody() {
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

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.True);
        Assert.That(body, Is.EqualTo("Report ready."));
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Subject, Is.EqualTo("README report"));
    }

    [Test]
    public void TryExtract_FencedExampleWithTrailingText_DoesNotParse() {
        const string text = """
            Example:

            ```json
            INBOX_MESSAGE_JSON:
            { "subject": "Example" }
            ```

            The real response continues here.
            """;

        var result = InboxMessageParser.TryExtract(text, out var body, out var dto);

        Assert.That(result, Is.False);
        Assert.That(body, Is.EqualTo(text));
        Assert.That(dto, Is.Null);
    }
}
