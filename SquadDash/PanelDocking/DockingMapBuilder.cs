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

        // ── Suppress no-op columns ───────────────────────────────────────────
        // If the source panel is the only occupant of its zone, moving it there
        // would be a no-op; hide the column entirely.
        bool suppressLeft   = sourceInLeft   && leftPanels.Count   == 1;
        bool suppressLeft2  = sourceInLeft2  && left2Panels.Count  == 1;
        bool suppressRight  = sourceInRight  && rightPanels.Count  == 1;
        bool suppressRight2 = sourceInRight2 && right2Panels.Count == 1;

        // ── Compute zone dimensions ──────────────────────────────────────────

        // Left/Left2 zone column content height = total slot heights + gaps
        double leftColContentHeight   = suppressLeft   ? 0 : ZoneColumnHeight(leftPanels.Count,   sourceInLeft,   ColSlotHeight);
        double rightColContentHeight  = suppressRight  ? 0 : ZoneColumnHeight(rightPanels.Count,  sourceInRight,  ColSlotHeight);
        double left2ColContentHeight  = suppressLeft2  ? 0 : ZoneColumnHeight(left2Panels.Count,  sourceInLeft2,  ColSlotHeight);
        double right2ColContentHeight = suppressRight2 ? 0 : ZoneColumnHeight(right2Panels.Count, sourceInRight2, ColSlotHeight);

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

        // ── Compute zone widths and X positions ──────────────────────────────
        // Suppressed zones contribute 0 width and no gutter so the popup shrinks.

        double left2ZoneWidth  = suppressLeft2  ? 0 : ColSlotWidth;
        double leftZoneWidth   = suppressLeft   ? 0 : ColSlotWidth;
        double rightZoneWidth  = suppressRight  ? 0 : ColSlotWidth;
        double right2ZoneWidth = suppressRight2 ? 0 : ColSlotWidth;
        double topZoneWidth    = Math.Max(topContentWidth, TopSlotWidth);

        // Build X positions left-to-right, accumulating only non-suppressed zone widths+gutters.
        double left2X, leftX, topX, rightX, right2X;
        {
            double x = 0;
            left2X = x; if (!suppressLeft2) x += left2ZoneWidth + ZoneGutter;
            leftX  = x; if (!suppressLeft)  x += leftZoneWidth  + ZoneGutter;
            topX   = x;                      x += topZoneWidth   + ZoneGutter;
            rightX = x; if (!suppressRight) x += rightZoneWidth + ZoneGutter;
            right2X = x;
        }

        double innerWidth  = right2X + right2ZoneWidth;
        double popupWidth  = innerWidth  + PopupPadding * 2;
        double popupHeight = innerHeight + PopupPadding * 2;

        // Left2 zone slots (outermost left column)
        if (!suppressLeft2)
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
        if (!suppressLeft)
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
        if (!suppressRight)
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
        if (!suppressRight2)
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
