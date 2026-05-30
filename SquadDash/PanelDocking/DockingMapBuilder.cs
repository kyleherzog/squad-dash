#nullable enable

namespace SquadDash.PanelDocking;

/// <summary>
/// Builds the DockingMapViewModel from the current layout and source panel.
/// Pure data logic — no WPF dependencies.
/// </summary>
internal static class DockingMapBuilder
{
    // Abbreviated panel labels (≤ 6 characters)
    private static readonly Dictionary<string, string> PanelLabels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["loop"]        = "Loop",
            ["tasks"]       = "Tasks",
            ["approvals"]   = "Aprvl",
            ["notes"]       = "Notes",
            ["inbox"]       = "Inbox",
            ["maintenance"] = "Maint",
        };

    // Sizing constants
    private const double ColSlotWidth      = 48;
    private const double ColSlotHeight     = 48;
    private const double TopSlotWidth      = 40;
    private const double TopSlotHeight     = 50;
    private const double SlotGap           = 4;
    private const double ZoneGutter        = 8;
    private const double PopupPadding      = 8;

    /// <summary>
    /// Builds the full DockingMapViewModel for the given source panel and current layout.
    /// Implements Rules A, B, C, D from the spec.
    /// </summary>
    internal static DockingMapViewModel BuildDockingMap(
        string sourcePanelId,
        DockLayout currentLayout,
        IReadOnlySet<string>? visiblePanelIds = null)
    {
        var allSlots = new List<SlotButtonViewModel>();

        // Partition panels by zone, ordered by their Order field.
        // Filter to only visible panels so the map reflects what the user actually sees.
        // The source panel is always included (user clicked its grip, so it is visible).
        var topPanels    = FilterZone(PanelsInZone(currentLayout, DockZone.Top),    sourcePanelId, visiblePanelIds);
        var leftPanels   = FilterZone(PanelsInZone(currentLayout, DockZone.Left),   sourcePanelId, visiblePanelIds);
        var rightPanels  = FilterZone(PanelsInZone(currentLayout, DockZone.Right),  sourcePanelId, visiblePanelIds);
        var left2Panels  = FilterZone(PanelsInZone(currentLayout, DockZone.Left2),  sourcePanelId, visiblePanelIds);
        var right2Panels = FilterZone(PanelsInZone(currentLayout, DockZone.Right2), sourcePanelId, visiblePanelIds);

        bool sourceInTop    = topPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInLeft   = leftPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInRight  = rightPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInLeft2  = left2Panels.Any(p => Same(p, sourcePanelId));
        bool sourceInRight2 = right2Panels.Any(p => Same(p, sourcePanelId));

        // ── Compute zone dimensions ──────────────────────────────────────────

        // Left/Left2 zone column content height = total slot heights + gaps
        double leftColContentHeight   = ZoneColumnHeight(leftPanels.Count,   sourceInLeft,   ColSlotHeight);
        double rightColContentHeight  = ZoneColumnHeight(rightPanels.Count,  sourceInRight,  ColSlotHeight);
        double left2ColContentHeight  = ZoneColumnHeight(left2Panels.Count,  sourceInLeft2,  ColSlotHeight);
        double right2ColContentHeight = ZoneColumnHeight(right2Panels.Count, sourceInRight2, ColSlotHeight);

        // Top zone content width
        double topContentWidth = ZoneRowWidth(topPanels.Count, sourceInTop, TopSlotWidth);

        // Overall popup inner height (excluding padding):
        //   max of all column heights, top slot height + lower half breathing room
        double innerHeight = Math.Max(
            Math.Max(
                Math.Max(leftColContentHeight,  left2ColContentHeight),
                Math.Max(rightColContentHeight, right2ColContentHeight)),
            TopSlotHeight * 2);   // top zone occupies upper half; lower half is breathing room

        // Clamp minimum inner height so popup never looks too small
        innerHeight = Math.Max(innerHeight, TopSlotHeight * 2 + ZoneGutter);

        // ── Compute zone widths ──────────────────────────────────────────────

        // All side zones: always ColSlotWidth wide (one column skeleton)
        double leftZoneWidth   = ColSlotWidth;
        double rightZoneWidth  = ColSlotWidth;
        double left2ZoneWidth  = ColSlotWidth;
        double right2ZoneWidth = ColSlotWidth;

        double topZoneWidth = Math.Max(topContentWidth, TopSlotWidth);

        double innerWidth = left2ZoneWidth + ZoneGutter
                          + leftZoneWidth  + ZoneGutter
                          + topZoneWidth   + ZoneGutter
                          + rightZoneWidth + ZoneGutter
                          + right2ZoneWidth;

        double popupWidth  = innerWidth  + PopupPadding * 2;
        double popupHeight = innerHeight + PopupPadding * 2;

        // ── Layout slot positions (relative to inner canvas, 0,0 at top-left) ──

        double left2X  = 0;
        double leftX   = left2ZoneWidth + ZoneGutter;
        double topX    = leftX + leftZoneWidth + ZoneGutter;
        double rightX  = topX + topZoneWidth + ZoneGutter;
        double right2X = rightX + rightZoneWidth + ZoneGutter;
        double upperHalfHeight = innerHeight / 2;

        // Left2 zone slots (outermost left column)
        BuildColumnSlots(
            allSlots,
            sourcePanelId,
            left2Panels,
            sourceInLeft2,
            left2X, 0,
            ColSlotWidth, ColSlotHeight,
            innerHeight,
            DockZone.Left2);

        // Left zone slots
        BuildColumnSlots(
            allSlots,
            sourcePanelId,
            leftPanels,
            sourceInLeft,
            leftX, 0,
            ColSlotWidth, ColSlotHeight,
            innerHeight,
            DockZone.Left);

        // Top zone slots — top-aligned so their tops line up with the column panel tops
        BuildRowSlots(
            allSlots,
            sourcePanelId,
            topPanels,
            sourceInTop,
            topX, 0,
            TopSlotWidth, TopSlotHeight,
            topZoneWidth,
            DockZone.Top);

        // Right zone slots
        BuildColumnSlots(
            allSlots,
            sourcePanelId,
            rightPanels,
            sourceInRight,
            rightX, 0,
            ColSlotWidth, ColSlotHeight,
            innerHeight,
            DockZone.Right);

        // Right2 zone slots (outermost right column)
        BuildColumnSlots(
            allSlots,
            sourcePanelId,
            right2Panels,
            sourceInRight2,
            right2X, 0,
            ColSlotWidth, ColSlotHeight,
            innerHeight,
            DockZone.Right2);

        // ── Find the source panel slot center ───────────────────────────────
        var srcSlot = allSlots.FirstOrDefault(s => s.IsSourcePanel);
        double srcCenterX = srcSlot is not null
            ? srcSlot.X + srcSlot.Width  / 2 + PopupPadding
            : popupWidth  / 2;
        double srcCenterY = srcSlot is not null
            ? srcSlot.Y + srcSlot.Height / 2 + PopupPadding
            : popupHeight / 2;

        return new DockingMapViewModel(
            allSlots,
            popupWidth,
            popupHeight,
            srcCenterX,
            srcCenterY);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static List<string> PanelsInZone(DockLayout layout, DockZone zone) =>
        layout.Slots
              .Where(s => s.Zone == zone)
              .OrderBy(s => s.Order)
              .Select(s => s.PanelId)
              .ToList();

    private static List<string> FilterZone(
        List<string> panels,
        string sourcePanelId,
        IReadOnlySet<string>? visiblePanelIds) =>
        visiblePanelIds is null
            ? panels
            : panels.Where(p => visiblePanelIds.Contains(p) || Same(p, sourcePanelId)).ToList();

    private static bool Same(string panelId, string sourcePanelId) =>
        string.Equals(panelId, sourcePanelId, StringComparison.OrdinalIgnoreCase);

    private static string LabelFor(string panelId) =>
        PanelLabels.TryGetValue(panelId, out var lbl) ? lbl : panelId[..Math.Min(6, panelId.Length)];

    // Total height needed for a column zone (Rule B has N slots, Rule C has N+1, Rule D has 1)
    private static double ZoneColumnHeight(int panelCount, bool sourceInZone, double slotHeight)
    {
        if (panelCount == 0) return slotHeight; // Rule D: 1 full-height slot
        int buttonCount = sourceInZone ? panelCount : panelCount + 1; // Rule B vs C
        return buttonCount * slotHeight + Math.Max(0, buttonCount - 1) * SlotGap;
    }

    // Total width needed for a row zone
    private static double ZoneRowWidth(int panelCount, bool sourceInZone, double slotWidth)
    {
        if (panelCount == 0) return slotWidth; // Rule D
        int buttonCount = sourceInZone ? panelCount : panelCount + 1;
        return buttonCount * slotWidth + Math.Max(0, buttonCount - 1) * SlotGap;
    }

    /// <summary>
    /// Adds slot buttons for a column (Left or Right) zone.
    /// </summary>
    private static void BuildColumnSlots(
        List<SlotButtonViewModel> result,
        string sourcePanelId,
        List<string> zonePanels,
        bool sourceInZone,
        double zoneX, double zoneY,
        double slotW, double slotH,
        double zoneAvailableHeight,
        DockZone zone)
    {
        if (zonePanels.Count == 0 && !sourceInZone)
        {
            // Rule D: one full-height "dock here" slot
            result.Add(new SlotButtonViewModel(
                Label:           "—",
                IsSourcePanel:   false,
                IsExpansionButton: false,
                X:               zoneX,
                Y:               zoneY,
                Width:           slotW,
                Height:          zoneAvailableHeight,
                TargetZone:      zone,
                TargetOrder:     0,
                SourcePanelId:   sourcePanelId));
            return;
        }

        // Rule B (sourceInZone) or Rule C (!sourceInZone)
        int buttonCount = sourceInZone ? zonePanels.Count : zonePanels.Count + 1;

        // Stretch slots to fill the full available height (equal share each)
        double effectiveSlotH = buttonCount > 0
            ? (zoneAvailableHeight - Math.Max(0, buttonCount - 1) * SlotGap) / buttonCount
            : slotH;
        effectiveSlotH = Math.Max(effectiveSlotH, slotH); // never smaller than the nominal size

        double curY = zoneY;

        for (int i = 0; i < zonePanels.Count; i++)
        {
            string pid    = zonePanels[i];
            bool isSrc    = Same(pid, sourcePanelId);
            result.Add(new SlotButtonViewModel(
                Label:           LabelFor(pid),
                IsSourcePanel:   isSrc,
                IsExpansionButton: false,
                X:               zoneX,
                Y:               curY,
                Width:           slotW,
                Height:          effectiveSlotH,
                TargetZone:      zone,
                TargetOrder:     i,
                SourcePanelId:   sourcePanelId));
            curY += effectiveSlotH + SlotGap;
        }

        if (!sourceInZone)
        {
            // Append-at-end insertion slot (Rule C)
            result.Add(new SlotButtonViewModel(
                Label:           "+",
                IsSourcePanel:   false,
                IsExpansionButton: false,
                X:               zoneX,
                Y:               curY,
                Width:           slotW,
                Height:          effectiveSlotH,
                TargetZone:      zone,
                TargetOrder:     zonePanels.Count,
                SourcePanelId:   sourcePanelId));
        }
    }

    /// <summary>
    /// Adds slot buttons for the Top zone row.
    /// </summary>
    private static void BuildRowSlots(
        List<SlotButtonViewModel> result,
        string sourcePanelId,
        List<string> zonePanels,
        bool sourceInZone,
        double zoneX, double zoneY,
        double slotW, double slotH,
        double zoneAvailableWidth,
        DockZone zone)
    {
        if (zonePanels.Count == 0 && !sourceInZone)
        {
            // Rule D: one full-width "dock here" slot
            result.Add(new SlotButtonViewModel(
                Label:           "—",
                IsSourcePanel:   false,
                IsExpansionButton: false,
                X:               zoneX,
                Y:               zoneY,
                Width:           zoneAvailableWidth,
                Height:          slotH,
                TargetZone:      zone,
                TargetOrder:     0,
                SourcePanelId:   sourcePanelId));
            return;
        }

        int buttonCount = sourceInZone ? zonePanels.Count : zonePanels.Count + 1;
        double totalW   = buttonCount * slotW + Math.Max(0, buttonCount - 1) * SlotGap;

        // Center the row horizontally
        double startX = zoneX + Math.Max(0, (zoneAvailableWidth - totalW) / 2);
        double curX   = startX;

        for (int i = 0; i < zonePanels.Count; i++)
        {
            string pid = zonePanels[i];
            bool isSrc = Same(pid, sourcePanelId);
            result.Add(new SlotButtonViewModel(
                Label:           LabelFor(pid),
                IsSourcePanel:   isSrc,
                IsExpansionButton: false,
                X:               curX,
                Y:               zoneY,
                Width:           slotW,
                Height:          slotH,
                TargetZone:      zone,
                TargetOrder:     i,
                SourcePanelId:   sourcePanelId));
            curX += slotW + SlotGap;
        }

        if (!sourceInZone)
        {
            // Append-at-end insertion slot (Rule C)
            result.Add(new SlotButtonViewModel(
                Label:           "+",
                IsSourcePanel:   false,
                IsExpansionButton: false,
                X:               curX,
                Y:               zoneY,
                Width:           slotW,
                Height:          slotH,
                TargetZone:      zone,
                TargetOrder:     zonePanels.Count,
                SourcePanelId:   sourcePanelId));
        }
    }
}
