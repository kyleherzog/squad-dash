#nullable enable

namespace SquadDash.PanelDocking;

/// <summary>
/// A named snapshot of panel placement that can be saved and restored per workspace.
/// </summary>
public sealed class DockLayout
{
    /// <summary>User-visible name for this layout (e.g. "Default", "Focus", "Review").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One slot entry per dockable panel included in this layout.</summary>
    public List<PanelSlot> Slots { get; set; } = new();

    /// <summary>Saved width of the left dock zone column; null means use default.</summary>
    public double? LeftZoneWidth { get; set; }

    /// <summary>Saved width of the right dock zone column; null means use default.</summary>
    public double? RightZoneWidth { get; set; }

    /// <summary>
    /// Returns the canonical default layout: every dockable panel in the Top zone,
    /// ordered to match their current left-to-right position in the status strip.
    /// </summary>
    public static DockLayout CreateDefault() => new()
    {
        Name = "Default",
        Slots =
        [
            new PanelSlot("tasks",       DockZone.Top, 0),
            new PanelSlot("approvals",   DockZone.Top, 1),
            new PanelSlot("inbox",       DockZone.Top, 2),
            new PanelSlot("maintenance", DockZone.Top, 3),
            new PanelSlot("notes",       DockZone.Top, 4),
            new PanelSlot("health",      DockZone.Top, 5),
            new PanelSlot("trace",       DockZone.Top, 6),
        ]
    };
}
