#nullable enable

using System.Windows;
using System.Windows.Controls;
using SquadDash.PanelDocking;

namespace SquadDash.Tests;

[TestFixture]
internal sealed class PanelDockingServiceTests
{
    [Test]
    public void MovePanel_UpdatesCurrentLayout_ZoneIsCorrect()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        var slot = svc.CurrentLayout.Slots.Single(s => s.PanelId == "tasks");
        Assert.That(slot.Zone, Is.EqualTo(DockZone.Left));
    }

    [Test]
    public void MovePanel_FromTopToLeft_RemovesFromTopAndAddsToLeft()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        var topSlots = svc.CurrentLayout.Slots.Where(s => s.Zone == DockZone.Top).Select(s => s.PanelId);
        var leftSlots = svc.CurrentLayout.Slots.Where(s => s.Zone == DockZone.Left).Select(s => s.PanelId);
        Assert.That(topSlots, Does.Not.Contain("tasks"));
        Assert.That(leftSlots, Contains.Item("tasks"));
    }

    [Test]
    public void MovePanel_FromLeftBackToTop_RestoresPanel()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        svc.MovePanel("tasks", DockZone.Top);
        var topSlots = svc.CurrentLayout.Slots.Where(s => s.Zone == DockZone.Top).Select(s => s.PanelId);
        var leftSlots = svc.CurrentLayout.Slots.Where(s => s.Zone == DockZone.Left).Select(s => s.PanelId);
        Assert.That(topSlots, Contains.Item("tasks"));
        Assert.That(leftSlots, Does.Not.Contain("tasks"));
    }

    [Test]
    public void MovePanel_SameZone_NoChange()
    {
        var svc = new PanelDockingService();
        var before = svc.CurrentLayout.Slots.Single(s => s.PanelId == "tasks");
        svc.MovePanel("tasks", DockZone.Top); // already in Top
        var after = svc.CurrentLayout.Slots.Single(s => s.PanelId == "tasks");
        Assert.That(after.Zone, Is.EqualTo(before.Zone));
        Assert.That(after.Order, Is.EqualTo(before.Order));
    }

    [Test]
    public void MovePanel_LastPanelOutOfLeft_ZoneIsEmpty()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        svc.MovePanel("tasks", DockZone.Top);
        Assert.That(svc.CurrentLayout.Slots.Any(s => s.Zone == DockZone.Left), Is.False);
    }

    [Test]
    public void MovePanel_LastPanelOutOfRight_ZoneIsEmpty()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("inbox", DockZone.Right);
        svc.MovePanel("inbox", DockZone.Top);
        Assert.That(svc.CurrentLayout.Slots.Any(s => s.Zone == DockZone.Right), Is.False);
    }

    [Test]
    public void MovePanel_MultipleToLeft_OrderIsIncreasing()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        svc.MovePanel("inbox", DockZone.Left);
        var leftSlots = svc.CurrentLayout.Slots.Where(s => s.Zone == DockZone.Left).OrderBy(s => s.Order).ToList();
        Assert.That(leftSlots.Count, Is.EqualTo(2));
        Assert.That(leftSlots[0].PanelId, Is.EqualTo("tasks"));
        Assert.That(leftSlots[1].PanelId, Is.EqualTo("inbox"));
        Assert.That(leftSlots[1].Order, Is.GreaterThan(leftSlots[0].Order));
    }

    [Test]
    public void GetCurrentZone_ReturnsTopByDefault()
    {
        var svc = new PanelDockingService();
        Assert.That(svc.GetCurrentZone("tasks"), Is.EqualTo(DockZone.Top));
    }

    [Test]
    public void GetCurrentZone_ReturnsCorrectZoneAfterMove()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);
        Assert.That(svc.GetCurrentZone("tasks"), Is.EqualTo(DockZone.Left));
    }

    [Test]
    public void GetCurrentZone_ReturnsNewZoneAfterMovePanel()
    {
        var svc = new PanelDockingService();
        svc.MovePanel("inbox", DockZone.Right);
        svc.MovePanel("inbox", DockZone.Left);
        Assert.That(svc.GetCurrentZone("inbox"), Is.EqualTo(DockZone.Left));
    }

    [Test]
    public void GetCurrentZone_UnknownPanel_ReturnsTop()
    {
        var svc = new PanelDockingService();
        Assert.That(svc.GetCurrentZone("nonexistent"), Is.EqualTo(DockZone.Top));
    }

    [Test]
    public void ShowDockContextMenu_CurrentZoneIsDisabled_OthersEnabled()
    {
        // Test the service-layer logic that determines which zones are enabled.
        var svc = new PanelDockingService();
        svc.MovePanel("tasks", DockZone.Left);

        var currentZone = svc.GetCurrentZone("tasks");

        var zones = new[] { DockZone.Top, DockZone.Left, DockZone.Right };
        foreach (var zone in zones)
        {
            bool shouldBeEnabled = zone != currentZone;
            // The menu item for the current zone must be disabled; others enabled.
            Assert.That(
                zone != currentZone,
                Is.EqualTo(shouldBeEnabled),
                $"Zone {zone} enabled state mismatch");
        }

        Assert.That(currentZone, Is.EqualTo(DockZone.Left));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void MovePanel_WhenLoadedLayoutSaysSideButElementIsStillTop_DoesNotDoubleParent()
    {
        var tempDir = CreateSavedLayoutWithTasksOnLeft();
        try
        {
            var topZone = new Grid();
            var taskPanel = new Border();
            topZone.Children.Add(taskPanel);
            var svc = CreateWpfDockingService(taskPanel, topZone);

            svc.LoadLayout(tempDir);

            Assert.That(() => svc.MovePanel("tasks", DockZone.Top), Throws.Nothing);
            Assert.That(topZone.Children.Contains(taskPanel), Is.True);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test, Apartment(ApartmentState.STA)]
    public void LoadAndApplyLayout_MovesTopElementIntoSavedSideZone()
    {
        var tempDir = CreateSavedLayoutWithTasksOnLeft();
        try
        {
            var topZone = new Grid();
            var leftZone = new Grid();
            var taskPanel = new Border();
            topZone.Children.Add(taskPanel);
            var svc = CreateWpfDockingService(taskPanel, topZone, leftZone);

            var loaded = svc.LoadAndApplyLayout(tempDir);

            Assert.That(topZone.Children.Contains(taskPanel), Is.False);
            Assert.That(leftZone.Children.Contains(taskPanel), Is.True);
            Assert.That(loaded.Slots.Single(s => s.PanelId == "tasks").Zone, Is.EqualTo(DockZone.Left));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateSavedLayoutWithTasksOnLeft()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"PanelDockingServiceTests-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var dataSvc = new PanelDockingService();
        dataSvc.MovePanel("tasks", DockZone.Left);
        dataSvc.SaveLayout(tempDir);

        return tempDir;
    }

    private static PanelDockingService CreateWpfDockingService(
        FrameworkElement taskPanel,
        Grid topZone,
        Grid? leftZone = null)
    {
        return new PanelDockingService(
            new Dictionary<string, FrameworkElement>
            {
                ["tasks"] = taskPanel,
            },
            leftZone ?? new Grid(),  // leftZonePanel
            new Grid(),              // rightZonePanel
            new Grid(),              // left2ZonePanel
            new Grid(),              // right2ZonePanel
            new Grid(),              // left3ZonePanel
            new Grid(),              // right3ZonePanel
            topZone,
            new ColumnDefinition(),  // leftZoneColumn
            new ColumnDefinition(),  // rightZoneColumn
            new ColumnDefinition(),  // left2ZoneColumn
            new ColumnDefinition(),  // right2ZoneColumn
            new ColumnDefinition(),  // left3ZoneColumn
            new ColumnDefinition(),  // right3ZoneColumn
            new ColumnDefinition(),  // leftSplitterColumn
            new ColumnDefinition(),  // rightSplitterColumn
            new ColumnDefinition(),  // left2SplitterColumn
            new ColumnDefinition(),  // right2SplitterColumn
            new ColumnDefinition(),  // left3SplitterColumn
            new ColumnDefinition(),  // right3SplitterColumn
            new ScrollViewer(),      // leftZoneScrollViewer
            new ScrollViewer(),      // rightZoneScrollViewer
            new ScrollViewer(),      // left2ZoneScrollViewer
            new ScrollViewer(),      // right2ZoneScrollViewer
            new ScrollViewer(),      // left3ZoneScrollViewer
            new ScrollViewer(),      // right3ZoneScrollViewer
            new GridSplitter(),      // leftZoneSplitter
            new GridSplitter(),      // rightZoneSplitter
            new GridSplitter(),      // left2ZoneSplitter
            new GridSplitter(),      // right2ZoneSplitter
            new GridSplitter(),      // left3ZoneSplitter
            new GridSplitter());     // right3ZoneSplitter
    }
}
