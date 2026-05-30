#nullable enable

namespace SquadDash.PanelDocking;

// ──────────────────────────────────────────────────────────────────────────────
// PanelDockingService
//
// Owns the current DockLayout and provides the operations that the UI will call
// when wiring is complete.  For now, all methods update the in-memory model only
// — no WPF elements are moved.
//
// Planned wiring (tracked in tasks.md under [Docking]):
//   • MovePanel() will reparent the panel's Border from its current container
//     stack (TopPanelStrip / LeftPanelStack / RightPanelStack) to the target one.
//   • SaveLayout() / LoadLayout() will serialize DockLayout to/from a per-workspace
//     JSON file (.squad/panel-layouts.json) via JsonFileStorage.
//   • MainWindow will instantiate this service during InitializeComponent and
//     subscribe to a LayoutChanged event (yet to be defined) to trigger a layout
//     refresh whenever a panel moves.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Manages the current panel layout and named layout save/restore.
/// UI wiring is not yet implemented — this is the foundational data-model layer.
/// </summary>
internal sealed class PanelDockingService
{
    private readonly Dictionary<string, DockLayout> _savedLayouts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The live panel layout for the current session.</summary>
    public DockLayout CurrentLayout { get; private set; } = DockLayout.CreateDefault();

    /// <summary>
    /// Updates the in-memory layout to place <paramref name="panelId"/> in <paramref name="targetZone"/>.
    /// The panel is appended at the end of the target zone (highest Order + 1).
    /// </summary>
    /// <remarks>
    /// When UI wiring is added, this method will also reparent the WPF Border control
    /// and trigger a layout refresh on the main window.
    /// </remarks>
    public void MovePanel(string panelId, DockZone targetZone)
    {
        var existing = CurrentLayout.Slots.FirstOrDefault(s =>
            string.Equals(s.PanelId, panelId, StringComparison.OrdinalIgnoreCase));

        if (existing is not null && existing.Zone == targetZone)
            return; // already there — nothing to do

        // Remove the slot (if present) and re-insert in the target zone.
        var slots = CurrentLayout.Slots
            .Where(s => !string.Equals(s.PanelId, panelId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        int nextOrder = slots
            .Where(s => s.Zone == targetZone)
            .Select(s => s.Order)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        slots.Add(new PanelSlot(panelId, targetZone, nextOrder));
        CurrentLayout.Slots = slots;
    }

    /// <summary>
    /// Stores a snapshot of <see cref="CurrentLayout"/> under <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Persistence to disk is not yet implemented.
    /// Future: serialize to .squad/panel-layouts.json via JsonFileStorage.
    /// </remarks>
    public void SaveLayout(string name)
    {
        var snapshot = new DockLayout
        {
            Name = name,
            Slots = CurrentLayout.Slots.ToList()
        };
        _savedLayouts[name] = snapshot;
    }

    /// <summary>
    /// Restores a previously saved layout by name, replacing <see cref="CurrentLayout"/>.
    /// Returns <c>true</c> if the layout was found and applied, <c>false</c> otherwise.
    /// </summary>
    /// <remarks>
    /// Persistence from disk is not yet implemented.
    /// Future: also load from .squad/panel-layouts.json when the in-memory cache misses.
    /// </remarks>
    public bool LoadLayout(string name)
    {
        if (!_savedLayouts.TryGetValue(name, out var layout))
            return false;

        CurrentLayout = new DockLayout
        {
            Name = layout.Name,
            Slots = layout.Slots.ToList()
        };
        return true;
    }

    /// <summary>Returns the names of all layouts that have been saved in this session.</summary>
    public IReadOnlyList<string> SavedLayoutNames =>
        _savedLayouts.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
}
