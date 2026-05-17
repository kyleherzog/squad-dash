using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SquadDash.Screenshots;

namespace SquadDash.Tests;

[TestFixture]
public class ScreenshotNamingHelperTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static EdgeAnchorRecord Anchor(params string[] names) =>
        new("Top", names, NeedsName: names.Length == 0, 0, 0, 0, 0, 0);

    private static IReadOnlyList<EdgeAnchorRecord> FourAnchors(
        IReadOnlyList<string> topNames,
        IReadOnlyList<string> rightNames,
        IReadOnlyList<string> bottomNames,
        IReadOnlyList<string> leftNames) =>
    [
        new("Top",    topNames,    NeedsName: topNames.Count    == 0, 0, 0, 0, 0, 0),
        new("Right",  rightNames,  NeedsName: rightNames.Count  == 0, 0, 0, 0, 0, 0),
        new("Bottom", bottomNames, NeedsName: bottomNames.Count == 0, 0, 0, 0, 0, 0),
        new("Left",   leftNames,   NeedsName: leftNames.Count   == 0, 0, 0, 0, 0, 0),
    ];

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Test]
    public void SuggestName_PascalCaseAnchorName_ProducesKebabCasePlusTheme()
    {
        var anchors = FourAnchors(["AgentStatusCard"], [], [], []);

        var result = ScreenshotNamingHelper.SuggestName("Dark", anchors);

        Assert.That(result, Is.EqualTo("agent-status-card-dark"));
    }

    [Test]
    public void SuggestName_AllAnchorsUnnamed_ReturnsTimestampFallback()
    {
        var anchors = FourAnchors([], [], [], []);

        var result = ScreenshotNamingHelper.SuggestName("Dark", anchors);

        Assert.That(result, Does.Match(@"^capture-dark-\d{14}$"),
            $"Expected timestamp fallback pattern, got: {result}");
    }

    [Test]
    public void SuggestName_ExcessiveDistinctTokens_CappedAtFourPlusTheme()
    {
        // Five distinct single-word names → 5 tokens before capping, capped to 4 + theme = 5 segments.
        var anchors = FourAnchors(
            ["AgentCard"],
            ["ToolbarButton"],
            ["SidebarPanel"],
            ["StatusBadge", "FooterLink"]);

        var result = ScreenshotNamingHelper.SuggestName("light", anchors);

        var segments = result.Split('-');
        Assert.That(segments.Length, Is.EqualTo(5),
            $"Expected 4 tokens + theme = 5 segments, got: {result}");
        Assert.That(segments[^1], Is.EqualTo("light"));
    }

    [Test]
    public void SuggestName_NullTheme_NormalizesToUnknown()
    {
        var anchors = FourAnchors(["MyControl"], [], [], []);

        var result = ScreenshotNamingHelper.SuggestName(null!, anchors);

        Assert.That(result, Does.EndWith("-unknown"),
            $"Expected result to end with '-unknown', got: {result}");
    }
}
