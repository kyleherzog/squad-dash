#nullable enable

using System.IO;
using System.Text.Json;
using NUnit.Framework;
using SquadDash.PanelDocking;

namespace SquadDash.Tests;

[TestFixture]
public class DockingLayoutEngineTests
{
    // ── JSON deserialization DTOs ────────────────────────────────────────────

    private sealed class TestCaseJson
    {
        public string Name { get; set; } = "";
        public string SourcePanelId { get; set; } = "";
        public Dictionary<string, List<string>> InitialLayout { get; set; } = new();
        public List<SlotButtonInfoJson> SlotButtons { get; set; } = new();
        public MoveActionJson Action { get; set; } = new();
        public string ChosenPreview { get; set; } = "";
        public Dictionary<string, List<string>> ExpectedLayout { get; set; } = new();
    }

    private sealed class SlotButtonInfoJson
    {
        public string Zone { get; set; } = "";
        public int Order { get; set; }
        public string Preview { get; set; } = "";
    }

    private sealed class MoveActionJson
    {
        public string TargetZone { get; set; } = "";
        public int TargetOrder { get; set; }
    }

    // ── Test case source ─────────────────────────────────────────────────────

    private static readonly string TestCasesFolder = Path.GetFullPath(
        Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\..\SquadDash.Tests\DockingTestCases"));

    private static IEnumerable<TestCaseData> DockingTestCases()
    {
        if (!Directory.Exists(TestCasesFolder))
            yield break;

        foreach (var file in Directory.EnumerateFiles(TestCasesFolder, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            yield return new TestCaseData(file).SetName(name);
        }
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(DockingTestCases))]
    public void RecordedDockingTestCase(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var tc   = JsonSerializer.Deserialize<TestCaseJson>(json, opts)
                   ?? throw new InvalidOperationException($"Failed to deserialize {filePath}");

        var initialLayout  = DockingLayoutEngine.ParseLayoutFromJson(tc.InitialLayout);
        var expectedLayout = DockingLayoutEngine.ParseLayoutFromJson(tc.ExpectedLayout);

        // ── 1. BuildSlotButtons matches recorded slots ────────────────────
        var builtSlots = DockingLayoutEngine.BuildSlotButtons(tc.SourcePanelId, initialLayout);

        var builtSet    = builtSlots.Select(s => (DockingLayoutEngine.GetZoneDisplayName(s.Zone), s.Order))
                                    .ToHashSet();
        var recordedSet = tc.SlotButtons.Select(s => (s.Zone, s.Order))
                                        .ToHashSet();

        Assert.That(builtSet, Is.EqualTo(recordedSet),
            "BuildSlotButtons result does not match recorded slot buttons");

        // ── 2. GetNormalizedPreviewDescription matches recorded previews ──
        foreach (var slotJson in tc.SlotButtons)
        {
            var zone    = ParseDisplayName(slotJson.Zone);
            var preview = DockingLayoutEngine.GetNormalizedPreviewDescription(zone, slotJson.Order, initialLayout);
            Assert.That(preview, Is.EqualTo(slotJson.Preview),
                $"Preview mismatch for zone={slotJson.Zone} order={slotJson.Order}");
        }

        // ── 3. ApplyMove result matches expectedLayout ────────────────────
        var targetZone   = ParseDisplayName(tc.Action.TargetZone);
        var resultLayout = DockingLayoutEngine.ApplyMove(
            tc.SourcePanelId, targetZone, tc.Action.TargetOrder, initialLayout);

        var actualDict   = DockingLayoutEngine.LayoutToJson(resultLayout);
        var expectedDict = DockingLayoutEngine.LayoutToJson(expectedLayout);

        foreach (var (zoneName, expectedPanels) in expectedDict)
        {
            var actualPanels = actualDict.TryGetValue(zoneName, out var ap) ? ap : new List<string>();
            Assert.That(actualPanels, Is.EqualTo(expectedPanels),
                $"Layout mismatch for zone '{zoneName}'");
        }
    }

    [Test]
    public void NoCasesIsInconclusive()
    {
        if (!Directory.Exists(TestCasesFolder) ||
            !Directory.EnumerateFiles(TestCasesFolder, "*.json").Any())
        {
            Assert.Inconclusive("No docking test case JSON files found in DockingTestCases folder.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DockZone ParseDisplayName(string displayName) => displayName switch
    {
        "Top"     => DockZone.Top,
        "Left 1"  => DockZone.Left,
        "Left 2"  => DockZone.Left2,
        "Right 1" => DockZone.Right,
        "Right 2" => DockZone.Right2,
        _         => throw new ArgumentException($"Unknown zone display name: '{displayName}'"),
    };
}
