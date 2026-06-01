#nullable enable

namespace SquadDash.PanelDocking;

internal sealed record SlotButtonViewModel(
    string Label,
    bool IsSourcePanel,
    bool IsExpansionButton,
    double X,
    double Y,
    double Width,
    double Height,
    DockZone TargetZone,
    int TargetOrder,
    string SourcePanelId)
{
    /// <summary>
    /// True for the thin vertical pill-shaped separators that flank the Top zone.
    /// These are purely decorative — no click handling.
    /// </summary>
    public bool IsSeparator { get; init; } = false;
}

internal sealed record DockingMapViewModel(
    IReadOnlyList<SlotButtonViewModel> Slots,
    double PopupWidth,
    double PopupHeight,
    double SourceSlotCenterX,
    double SourceSlotCenterY
);
