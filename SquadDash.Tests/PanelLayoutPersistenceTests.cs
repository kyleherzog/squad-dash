#nullable enable

using System;
using System.Text.Json;
using SquadDash.PanelDocking;

namespace SquadDash.Tests;

[TestFixture]
internal sealed class PanelLayoutPersistenceTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PanelLayoutTests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── SaveLayout ──────────────────────────────────────────────────────────

    [Test]
    public void SaveLayout_WritesValidJsonToExpectedPath()
    {
        var svc = new PanelDockingService();

        svc.SaveLayout(_tempDir);

        var expectedPath = Path.Combine(_tempDir, ".squad", "panel-layouts.json");
        Assert.That(File.Exists(expectedPath), Is.True, "panel-layouts.json should exist");

        var json = File.ReadAllText(expectedPath);
        Assert.That(() => JsonDocument.Parse(json), Throws.Nothing, "file must be valid JSON");
    }

    [Test]
    public void SaveLayout_CreatesSquadDirectory_IfMissing()
    {
        var svc = new PanelDockingService();

        svc.SaveLayout(_tempDir);

        Assert.That(Directory.Exists(Path.Combine(_tempDir, ".squad")), Is.True);
    }

    [Test]
    public void SaveLayout_ActiveLayoutMatchesCurrentLayoutName()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);

        svc.SaveLayout(_tempDir);

        var file = ReadFile();
        Assert.That(file.ActiveLayout, Is.EqualTo("Default"));
    }

    [Test]
    public void SaveLayout_UpsertsByName_DoesNotDuplicate()
    {
        var svc = new PanelDockingService();
        svc.SaveLayout(_tempDir);
        svc.MovePanel("tasks", DockZone.Left);
        svc.SaveLayout(_tempDir);

        var file = ReadFile();
        Assert.That(file.Layouts.Count(l => l.Name == "Default"), Is.EqualTo(1));
    }

    [Test]
    public void SaveLayout_PreservesMultipleNamedLayouts()
    {
        var svc = new PanelDockingService();
        svc.SaveLayout(_tempDir);

        svc.RenameCurrentLayout("Focus");
        svc.MovePanel("inbox", DockZone.Right);
        svc.SaveLayout(_tempDir);

        var file = ReadFile();
        Assert.That(file.Layouts.Select(l => l.Name), Is.EquivalentTo(new[] { "Default", "Focus" }));
    }

    // ── LoadLayout ──────────────────────────────────────────────────────────

    [Test]
    public void LoadLayout_MissingFile_ReturnsDefault()
    {
        var svc = new PanelDockingService();

        var layout = svc.LoadLayout(_tempDir);

        Assert.That(layout.Name, Is.EqualTo("Default"));
        Assert.That(layout.Slots, Has.Count.GreaterThan(0));
    }

    [Test]
    public void LoadLayout_UnknownActiveLayoutName_FallsBackToDefault()
    {
        var svc = new PanelDockingService();
        // Save a layout and then manually corrupt activeLayout name
        svc.SaveLayout(_tempDir);
        var path = Path.Combine(_tempDir, ".squad", "panel-layouts.json");
        var json = File.ReadAllText(path).Replace("\"Default\"", "\"Nonexistent\"", StringComparison.Ordinal);
        // Keep layouts as Default but set activeLayout to nonexistent
        var doc = JsonDocument.Parse(json);
        // Rewrite activeLayout only
        json = json.Replace("\"activeLayout\": \"Nonexistent\"", "\"activeLayout\": \"DoesNotExist\"", StringComparison.Ordinal);
        File.WriteAllText(path, json);

        var loaded = svc.LoadLayout(_tempDir);

        Assert.That(loaded.Name, Is.EqualTo("Default"));
    }

    [Test]
    public void LoadLayout_RestoresCurrentLayout()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        svc.SaveLayout(_tempDir);

        var svc2 = new PanelDockingService();
        var loaded = svc2.LoadLayout(_tempDir);

        var tasksSlot = loaded.Slots.Single(s => s.PanelId == "tasks");
        Assert.That(tasksSlot.Zone, Is.EqualTo(DockZone.Left));
    }

    [Test]
    public void LoadLayout_SetsCurrentLayout()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("inbox", DockZone.Right);
        svc.SaveLayout(_tempDir);

        var svc2 = new PanelDockingService();
        svc2.LoadLayout(_tempDir);

        Assert.That(svc2.CurrentLayout.Slots.Single(s => s.PanelId == "inbox").Zone,
                    Is.EqualTo(DockZone.Right));
    }

    // ── Round-trip ──────────────────────────────────────────────────────────

    [Test]
    public void RoundTrip_SaveThenLoad_ProducesIdenticalSlots()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        svc.MovePanel("inbox", DockZone.Right);
        svc.SaveLayout(_tempDir);

        var svc2 = new PanelDockingService();
        var loaded = svc2.LoadLayout(_tempDir);

        Assert.That(loaded.Name, Is.EqualTo(svc.CurrentLayout.Name));
        foreach (var originalSlot in svc.CurrentLayout.Slots)
        {
            var loadedSlot = loaded.Slots.SingleOrDefault(s => s.PanelId == originalSlot.PanelId);
            Assert.That(loadedSlot, Is.Not.Null, $"Missing slot for {originalSlot.PanelId}");
            Assert.That(loadedSlot!.Zone, Is.EqualTo(originalSlot.Zone), $"Zone mismatch for {originalSlot.PanelId}");
            Assert.That(loadedSlot.Order, Is.EqualTo(originalSlot.Order), $"Order mismatch for {originalSlot.PanelId}");
        }
    }

    // ── DockZone string serialization ───────────────────────────────────────

    [Test]
    public void SaveLayout_DockZone_SerializesAsString()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        svc.MovePanel("inbox", DockZone.Right);
        svc.SaveLayout(_tempDir);

        var json = File.ReadAllText(Path.Combine(_tempDir, ".squad", "panel-layouts.json"));

        Assert.That(json, Does.Contain("\"Left\""), "DockZone.Left should be serialized as string \"Left\"");
        Assert.That(json, Does.Contain("\"Right\""), "DockZone.Right should be serialized as string \"Right\"");
        Assert.That(json, Does.Contain("\"Top\""), "DockZone.Top should be serialized as string \"Top\"");
        // Verify zone values are strings not integers (integers would appear as "zone": 0/1/2)
        Assert.That(json, Does.Not.Match(@"""zone""\s*:\s*\d"), "DockZone must NOT serialize as integer");
    }

    // ── RenameCurrentLayout ─────────────────────────────────────────────────

    [Test]
    public void RenameCurrentLayout_UpdatesCurrentLayoutName()
    {
        var svc = new PanelDockingService();
        svc.RenameCurrentLayout("Focus");

        Assert.That(svc.CurrentLayout.Name, Is.EqualTo("Focus"));
    }

    [Test]
    public void RenameCurrentLayout_SavedUnderNewName()
    {
        var svc = new PanelDockingService();
        svc.RenameCurrentLayout("Compact");
        svc.MovePanel("notes", DockZone.Left);
        svc.SaveLayout(_tempDir);

        var file = ReadFile();
        Assert.That(file.ActiveLayout, Is.EqualTo("Compact"));
        Assert.That(file.Layouts.Single().Name, Is.EqualTo("Compact"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private PanelLayoutsFile ReadFile()
    {
        var json = File.ReadAllText(Path.Combine(_tempDir, ".squad", "panel-layouts.json"));
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        return JsonSerializer.Deserialize<PanelLayoutsFile>(json, options)!;
    }
}
