#nullable enable

namespace SquadDash.PanelDocking;

internal enum SyntheticInsertKind
{
    None,
    InsertBefore,   // Thin is on the outer side of a zone — preview = left-edge strip of target zone
    InsertAfter,    // Thin is on the inner side of a zone — preview = right-edge strip of target zone
}

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

    /// <summary>
    /// Describes how a synthetic thin slot inserts into the layout.
    /// </summary>
    public SyntheticInsertKind InsertKind { get; init; } = SyntheticInsertKind.None;

    /// <summary>
    /// True for synthetic thin "inter-zone" drop-target slots.
    /// InsertBefore = outer side of zone. InsertAfter = inner side of zone.
    /// </summary>
    public bool IsSyntheticInsert => InsertKind != SyntheticInsertKind.None;
}

internal sealed record DockingMapViewModel(
    IReadOnlyList<SlotButtonViewModel> Slots,
    double PopupWidth,
    double PopupHeight,
    double SourceSlotCenterX,
    double SourceSlotCenterY,
    double LeftSectionCenterX,
    double TopSectionCenterX,
    double RightSectionCenterX,
    bool HasLeftSection,
    bool HasRightSection
);
