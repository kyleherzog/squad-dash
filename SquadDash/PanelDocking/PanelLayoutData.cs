#nullable enable

namespace SquadDash.PanelDocking;

// Data snapshot of panel placement + visibility, free of WPF dependencies.
public sealed class PanelLayoutData
{
    public List<PanelSlot> Slots { get; set; } = new();
    // Set of panel IDs that are currently visible (non-collapsed).
    public IReadOnlySet<string> VisiblePanelIds { get; set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

// A destination slot button that the user can click in the docking map.
public sealed record SlotButtonInfo(DockZone Zone, int Order);

// Interface used by PanelDockingService to notify a recorder of completed moves
// without creating a compile-time dependency on the dev-only DockingTestRecorder class.
internal interface IDockingMoveRecorder
{
    void OnMoveCompleted(string sourcePanelId, DockZone targetZone, int targetOrder, PanelLayoutData layoutAfter);
    void OnDockingMapBuilt(IReadOnlyList<SlotButtonViewModel> slots);
}
