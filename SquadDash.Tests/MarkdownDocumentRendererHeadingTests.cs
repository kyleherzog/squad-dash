using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using SquadDash;

namespace SquadDash.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
internal sealed class MarkdownDocumentRendererHeadingTests {

    // ── Helpers ───────────────────────────────────────────────────────────

    private static MarkdownDocumentRenderer MakeRenderer(string? gitHubUrl = "https://github.com/owner/repo") =>
        new(
            getFontSize:               () => 14.0,
            getWorkspaceGitHubUrl:     () => gitHubUrl,
            onLinkClicked:             _ => { },
            onException:               (_, _) => { },
            resolveContinuationThread: _ => null,
            onQuickReplyButtonClick:   (_, _) => { },
            appendResponseSegment:     (_, _, _) => { },
            scrollToEndIfAtBottom:     _ => { },
            getCoordinatorThread:      () => null!);

    private static IEnumerable<Inline> FlattenInlines(IEnumerable<Inline> inlines) {
        foreach (var inline in inlines) {
            yield return inline;
            if (inline is Span span)
                foreach (var child in FlattenInlines(span.Inlines))
                    yield return child;
        }
    }

    private static List<Block> BuildHeadingBlocks(string markdownLine, string? gitHubUrl = "https://github.com/owner/repo") {
        var renderer = MakeRenderer(gitHubUrl);
        var thread   = new TranscriptThreadState("t1", TranscriptThreadKind.Coordinator, "Test", DateTimeOffset.Now);
        var section  = new Section();
        var turn     = new TranscriptTurnView(thread, "prompt", DateTimeOffset.Now, section, []);
        var entry    = new TranscriptResponseEntry(turn, 1, section, allowQuickReplies: false);
        return renderer.BuildResponseBlocks(entry, markdownLine, allowQuickReplies: false).ToList();
    }

    // ── Commit hash in heading ────────────────────────────────────────────

    [Test]
    public void Heading_WithBacktickCommitHash_RendersHashAsHyperlink() {
        // ### ✅ `LoopController` hardened — committed `7932ea8`
        var blocks = BuildHeadingBlocks("### ✅ `LoopController` hardened — committed `7932ea8`");

        var paragraph = blocks.OfType<Paragraph>().Single();
        var allInlines = FlattenInlines(paragraph.Inlines).ToList();

        Assert.That(allInlines.OfType<Hyperlink>().Any(h => {
            var run = h.Inlines.OfType<Run>().FirstOrDefault();
            return run?.Text == "7932ea8";
        }), Is.True, "Commit hash inside backticks in a heading should render as a Hyperlink");
    }

    [Test]
    public void Heading_WithBareCommitHash_RendersHashAsHyperlink() {
        var blocks = BuildHeadingBlocks("## Merged abc1234f into main");

        var paragraph = blocks.OfType<Paragraph>().Single();
        var allInlines = FlattenInlines(paragraph.Inlines).ToList();

        Assert.That(allInlines.OfType<Hyperlink>().Any(h => {
            var run = h.Inlines.OfType<Run>().FirstOrDefault();
            return run?.Text == "abc1234f";
        }), Is.True, "Bare commit hash in a heading should render as a Hyperlink");
    }

    [Test]
    public void Heading_WithNoGitHubUrl_DoesNotRenderHashAsHyperlink() {
        // Without a GitHub URL, bare hashes should NOT become links
        var blocks = BuildHeadingBlocks("## Merged abc1234f into main", gitHubUrl: null);

        var paragraph = blocks.OfType<Paragraph>().Single();
        var allInlines = FlattenInlines(paragraph.Inlines).ToList();

        Assert.That(allInlines.OfType<Hyperlink>(), Is.Empty,
            "No Hyperlink should be created when GitHub URL is not configured");
    }

    // ── Heading still bold ────────────────────────────────────────────────

    [Test]
    public void Heading_PlainText_IsRenderedBold() {
        var blocks = BuildHeadingBlocks("### Summary");

        var paragraph = blocks.OfType<Paragraph>().Single();
        // After fix, inlines are wrapped in a Bold span
        var bold = FlattenInlines(paragraph.Inlines).OfType<Bold>().FirstOrDefault()
                   ?? paragraph.Inlines.OfType<Bold>().FirstOrDefault();

        Assert.That(bold, Is.Not.Null, "Heading text should be wrapped in a Bold inline");
    }

    // ── Heading font size ─────────────────────────────────────────────────

    [Test]
    public void Heading_Level1_HasLargerFontThanLevel3() {
        var h1 = BuildHeadingBlocks("# Big title").OfType<Paragraph>().Single();
        var h3 = BuildHeadingBlocks("### Small title").OfType<Paragraph>().Single();

        Assert.That(h1.FontSize, Is.GreaterThan(h3.FontSize));
    }
}
