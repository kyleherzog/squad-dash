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
    private const double ColSlotWidthEmpty = ColSlotWidth / 3;  // narrower placeholder for empty (drop-target-only) zones
    private const double ColSlotHeight     = 48;
    private const double TopSlotWidth      = 40;
    private const double TopSlotHeight     = 50;
    private const double SlotGap           = 4;
    private const double ZoneGutter        = 28;  // pill (4px) + 10px breathing room on each side; used between side zone and top zone
    private const double InnerZoneGap      = 2;   // tight gap between sibling zone pairs (Left↔Left2, Right↔Right2)
    private const double MaxInnerHeight    = 200.0; // caps popup height so stacked panels shrink gracefully
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
        var left3Panels  = FilterZone(PanelsInZone(currentLayout, DockZone.Left3),  sourcePanelId, visiblePanelIds);
        var right3Panels = FilterZone(PanelsInZone(currentLayout, DockZone.Right3), sourcePanelId, visiblePanelIds);

        bool sourceInTop    = topPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInLeft   = leftPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInRight  = rightPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInLeft2  = left2Panels.Any(p => Same(p, sourcePanelId));
        bool sourceInRight2 = right2Panels.Any(p => Same(p, sourcePanelId));
        bool sourceInLeft3  = left3Panels.Any(p => Same(p, sourcePanelId));
        bool sourceInRight3 = right3Panels.Any(p => Same(p, sourcePanelId));

        // ── Suppress sibling zone when it would be a no-op target ─────────────
        // Case 1: both outer and inner zones are empty — suppress the outer zone
        //         so the user sees a single "dock here" slot per side.
        // Case 2: source is the SOLE occupant of its zone AND the sibling zone is
        //         empty — moving there is visually identical (source zone collapses,
        //         sibling expands with the same single panel). Suppress that target.
        bool suppressLeft2  = (left2Panels.Count == 0 && !sourceInLeft2 && leftPanels.Count  == 0 && !sourceInLeft)
                           || (sourceInLeft  && leftPanels.Count  == 1 && left2Panels.Count  == 0);
        bool suppressRight2 = (right2Panels.Count == 0 && !sourceInRight2 && rightPanels.Count == 0 && !sourceInRight)
                           || (sourceInRight && rightPanels.Count == 1 && right2Panels.Count == 0);
        // Symmetric: source sole in outer zone, inner zone is empty.
        bool suppressLeft   = sourceInLeft2  && left2Panels.Count  == 1 && leftPanels.Count  == 0;
        bool suppressRight  = sourceInRight2 && right2Panels.Count == 1 && rightPanels.Count == 0;

        bool suppressLeft3  = (left3Panels.Count == 0 && !sourceInLeft3 && left2Panels.Count == 0 && !sourceInLeft2)
                           || (sourceInLeft2 && left2Panels.Count == 1 && left3Panels.Count == 0);
        bool suppressRight3 = (right3Panels.Count == 0 && !sourceInRight3 && right2Panels.Count == 0 && !sourceInRight2)
                           || (sourceInRight2 && right2Panels.Count == 1 && right3Panels.Count == 0);

        // ── Compute zone dimensions ──────────────────────────────────────────

        // Left/Left2 zone column content height = total slot heights + gaps.
        // Rule B (sourceInZone): emits exactly N slots — no insertion extras.
        // When source is the sole occupant this produces 1 slot (the "you are here"
        // IsSourcePanel button); no no-op insertion slots are ever generated.
        double leftColContentHeight   = suppressLeft   ? 0 : ZoneColumnHeight(leftPanels.Count,   sourceInLeft,   ColSlotHeight);
        double rightColContentHeight  = suppressRight  ? 0 : ZoneColumnHeight(rightPanels.Count,  sourceInRight,  ColSlotHeight);
        double left2ColContentHeight  = suppressLeft2  ? 0 : ZoneColumnHeight(left2Panels.Count,  sourceInLeft2,  ColSlotHeight);
        double right2ColContentHeight = suppressRight2 ? 0 : ZoneColumnHeight(right2Panels.Count, sourceInRight2, ColSlotHeight);
        double left3ColContentHeight  = suppressLeft3  ? 0 : ZoneColumnHeight(left3Panels.Count,  sourceInLeft3,  ColSlotHeight);
        double right3ColContentHeight = suppressRight3 ? 0 : ZoneColumnHeight(right3Panels.Count, sourceInRight3, ColSlotHeight);

        // Top zone content width
        double topContentWidth = ZoneRowWidth(topPanels.Count, sourceInTop, TopSlotWidth);

        // Overall popup inner height (excluding padding):
        //   max of all column heights, top slot height + lower half breathing room
        double innerHeight = Math.Max(
            Math.Max(
                Math.Max(
                    Math.Max(leftColContentHeight,  left2ColContentHeight),
                    Math.Max(left3ColContentHeight, rightColContentHeight)),
                Math.Max(right2ColContentHeight, right3ColContentHeight)),
            TopSlotHeight * 2);   // top zone occupies upper half; lower half is breathing room

        // Clamp minimum inner height so popup never looks too small
        innerHeight = Math.Max(innerHeight, TopSlotHeight * 2 + ZoneGutter);
        // Cap maximum inner height so stacked panels shrink rather than blowing out the popup
        innerHeight = Math.Min(innerHeight, MaxInnerHeight);

        // ── Compute zone widths ──────────────────────────────────────────────

        // All side zones: ColSlotWidth when they have panels; ColSlotWidthEmpty when empty (drop-target only).
        double leftZoneWidth   = suppressLeft   ? 0 : (leftPanels.Count   == 0 && !sourceInLeft   ? ColSlotWidthEmpty : ColSlotWidth);
        double rightZoneWidth  = suppressRight  ? 0 : (rightPanels.Count  == 0 && !sourceInRight  ? ColSlotWidthEmpty : ColSlotWidth);
        double left2ZoneWidth  = suppressLeft2  ? 0 : (left2Panels.Count  == 0 && !sourceInLeft2  ? ColSlotWidthEmpty : ColSlotWidth);
        double right2ZoneWidth = suppressRight2 ? 0 : (right2Panels.Count == 0 && !sourceInRight2 ? ColSlotWidthEmpty : ColSlotWidth);
        double left3ZoneWidth  = suppressLeft3  ? 0 : (left3Panels.Count  == 0 && !sourceInLeft3  ? ColSlotWidthEmpty : ColSlotWidth);
        double right3ZoneWidth = suppressRight3 ? 0 : (right3Panels.Count == 0 && !sourceInRight3 ? ColSlotWidthEmpty : ColSlotWidth);

        double topZoneWidth = Math.Max(topContentWidth, TopSlotWidth);

        // Only include gutter for non-suppressed zones.
        // Between sibling zone pairs (Left3↔Left2↔Left, Right↔Right2↔Right3) use the tight InnerZoneGap.
        // Between the innermost side zone and the top zone use the full ZoneGutter (separator lives there).
        double innerWidth = (suppressLeft3  ? 0 : left3ZoneWidth  + (suppressLeft2  ? (suppressLeft  ? ZoneGutter : InnerZoneGap) : InnerZoneGap))
                          + (suppressLeft2  ? 0 : left2ZoneWidth  + (suppressLeft   ? ZoneGutter : InnerZoneGap))
                          + (suppressLeft   ? 0 : leftZoneWidth   + ZoneGutter)
                          + topZoneWidth
                          + (suppressRight  ? 0 : ZoneGutter + rightZoneWidth)
                          + (suppressRight2 ? 0 : (suppressRight  ? ZoneGutter : InnerZoneGap) + right2ZoneWidth)
                          + (suppressRight3 ? 0 : (suppressRight2 ? (suppressRight ? ZoneGutter : InnerZoneGap) : InnerZoneGap) + right3ZoneWidth);

        double popupWidth  = innerWidth  + PopupPadding * 2;
        double popupHeight = innerHeight + PopupPadding * 2;

        // ── Layout slot positions (relative to inner canvas, 0,0 at top-left) ──

        double left3X  = 0;
        double left2X  = suppressLeft3 ? left3X : left3X + left3ZoneWidth + (suppressLeft2 ? (suppressLeft ? ZoneGutter : InnerZoneGap) : InnerZoneGap);
        double leftX   = suppressLeft2 ? left2X : left2X + left2ZoneWidth + (suppressLeft ? ZoneGutter : InnerZoneGap);
        double topX    = suppressLeft  ? leftX  : leftX  + leftZoneWidth  + ZoneGutter;
        double rightX  = topX + topZoneWidth + (suppressRight ? 0 : ZoneGutter);
        // When Right is suppressed, Right2 is the outermost right zone and needs a full ZoneGutter
        // gap from the top zone (separator lives there). When Right is present, use InnerZoneGap.
        double right2X = suppressRight ? rightX + ZoneGutter : rightX + rightZoneWidth + InnerZoneGap;
        double right3X = suppressRight2 ? (suppressRight ? rightX + ZoneGutter : rightX + rightZoneWidth + InnerZoneGap) : right2X + right2ZoneWidth + InnerZoneGap;

        // ── Left side slots ──────────────────────────────────────────────────
        // When source is dragging from OUTSIDE the left side, show column-position slots (N+1 for N occupied).
        // When source is IN the left side, show panel-position slots (current behavior).
        bool sourceInLeftSide = sourceInLeft || sourceInLeft2 || sourceInLeft3;
        
        if (!sourceInLeftSide)
        {
            // Multi-position drop: show N+1 column slots for N occupied columns
            BuildSideColumnPositionSlots(
                allSlots,
                sourcePanelId,
                leftPanels, left2Panels, left3Panels,
                suppressLeft, suppressLeft2, suppressLeft3,
                leftX, leftZoneWidth,
                left2X, left2ZoneWidth,
                left3X, left3ZoneWidth,
                0, ColSlotHeight, innerHeight,
                DockZone.Left, DockZone.Left2, DockZone.Left3);
        }
        else
        {
            // Source is in left side: use panel-position slots (current behavior)
            if (!suppressLeft3)
                BuildColumnSlots(
                    allSlots, sourcePanelId, left3Panels, sourceInLeft3,
                    left3X, 0, left3ZoneWidth, ColSlotHeight, innerHeight, DockZone.Left3);

            if (!suppressLeft2)
                BuildColumnSlots(
                    allSlots, sourcePanelId, left2Panels, sourceInLeft2,
                    left2X, 0, left2ZoneWidth, ColSlotHeight, innerHeight, DockZone.Left2);

            if (!suppressLeft)
                BuildColumnSlots(
                    allSlots, sourcePanelId, leftPanels, sourceInLeft,
                    leftX, 0, leftZoneWidth, ColSlotHeight, innerHeight, DockZone.Left);
        }

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

        // ── Right side slots ─────────────────────────────────────────────────
        // When source is dragging from OUTSIDE the right side, show column-position slots (N+1 for N occupied).
        // When source is IN the right side, show panel-position slots (current behavior).
        bool sourceInRightSide = sourceInRight || sourceInRight2 || sourceInRight3;
        
        if (!sourceInRightSide)
        {
            // Multi-position drop: show N+1 column slots for N occupied columns
            BuildSideColumnPositionSlots(
                allSlots,
                sourcePanelId,
                rightPanels, right2Panels, right3Panels,
                suppressRight, suppressRight2, suppressRight3,
                rightX, rightZoneWidth,
                right2X, right2ZoneWidth,
                right3X, right3ZoneWidth,
                0, ColSlotHeight, innerHeight,
                DockZone.Right, DockZone.Right2, DockZone.Right3);
        }
        else
        {
            // Source is in right side: use panel-position slots (current behavior)
            if (!suppressRight)
                BuildColumnSlots(
                    allSlots, sourcePanelId, rightPanels, sourceInRight,
                    rightX, 0, rightZoneWidth, ColSlotHeight, innerHeight, DockZone.Right);

            if (!suppressRight2)
                BuildColumnSlots(
                    allSlots, sourcePanelId, right2Panels, sourceInRight2,
                    right2X, 0, right2ZoneWidth, ColSlotHeight, innerHeight, DockZone.Right2);

            if (!suppressRight3)
                BuildColumnSlots(
                    allSlots, sourcePanelId, right3Panels, sourceInRight3,
                    right3X, 0, right3ZoneWidth, ColSlotHeight, innerHeight, DockZone.Right3);
        }

        // ── Separators ───────────────────────────────────────────────────────
        // Thin pill-shaped vertical dividers between the top zone and each side group.
        // A separator is placed in the ZoneGutter midpoint when that side exists.
        const double SeparatorWidth = 4.0;
        // Left separator: topX > 0 means at least one left zone exists with a gutter before the top zone.
        if (topX > 0)
        {
            double sepX = topX - ZoneGutter / 2.0 - SeparatorWidth / 2.0;
            allSlots.Add(new SlotButtonViewModel(
                Label: string.Empty, IsSourcePanel: false, IsExpansionButton: false,
                X: sepX, Y: 0,
                Width: SeparatorWidth, Height: innerHeight,
                TargetZone: DockZone.Top, TargetOrder: -1,
                SourcePanelId: sourcePanelId)
            { IsSeparator = true });
        }
        // Right separator: top zone does not extend to innerWidth means a right side exists.
        double topZoneRightEdge = topX + topZoneWidth;
        if (topZoneRightEdge < innerWidth)
        {
            double sepX = topZoneRightEdge + ZoneGutter / 2.0 - SeparatorWidth / 2.0;
            allSlots.Add(new SlotButtonViewModel(
                Label: string.Empty, IsSourcePanel: false, IsExpansionButton: false,
                X: sepX, Y: 0,
                Width: SeparatorWidth, Height: innerHeight,
                TargetZone: DockZone.Top, TargetOrder: -2,
                SourcePanelId: sourcePanelId)
            { IsSeparator = true });
        }

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
        effectiveSlotH = Math.Max(effectiveSlotH, 16.0); // minimum slot height even when many panels stack

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

    /// <summary>
    /// Builds column-position slots for an entire side (e.g., Left+Left2+Left3) when
    /// source is dragging from outside that side. Shows N+1 drop targets for N occupied columns.
    /// </summary>
    private static void BuildSideColumnPositionSlots(
        List<SlotButtonViewModel> result,
        string sourcePanelId,
        List<string> innerPanels,    // Left or Right
        List<string> middlePanels,   // Left2 or Right2
        List<string> outerPanels,    // Left3 or Right3
        bool suppressInner,
        bool suppressMiddle,
        bool suppressOuter,
        double innerX, double innerWidth,
        double middleX, double middleWidth,
        double outerX, double outerWidth,
        double slotY, double slotH, double availableHeight,
        DockZone innerZone, DockZone middleZone, DockZone outerZone)
    {
        // Count occupied columns (visible, non-suppressed)
        int occupiedCount = 0;
        bool innerOccupied = !suppressInner && innerPanels.Count > 0;
        bool middleOccupied = !suppressMiddle && middlePanels.Count > 0;
        bool outerOccupied = !suppressOuter && outerPanels.Count > 0;
        
        if (innerOccupied) occupiedCount++;
        if (middleOccupied) occupiedCount++;
        if (outerOccupied) occupiedCount++;

        // Generate N+1 column-position slots
        int slotCount = occupiedCount + 1;
        double effectiveSlotH = slotCount > 0
            ? (availableHeight - Math.Max(0, slotCount - 1) * SlotGap) / slotCount
            : slotH;
        effectiveSlotH = Math.Max(effectiveSlotH, 16.0);

        double curY = slotY;

        // Determine which zones to show slots for based on what's occupied
        var columnTargets = new List<(DockZone zone, double x, double width, string label)>();
        
        // Always offer the innermost position
        if (!suppressInner)
            columnTargets.Add((innerZone, innerX, innerWidth, innerPanels.Count > 0 ? "#" : "—"));
        
        // If inner is occupied or middle exists, offer middle position
        if (innerOccupied && !suppressMiddle)
            columnTargets.Add((middleZone, middleX, middleWidth, middlePanels.Count > 0 ? "#" : "—"));
        
        // If inner+middle are occupied or outer exists, offer outer position
        if ((innerOccupied && middleOccupied) || (innerOccupied && suppressMiddle) && !suppressOuter)
            columnTargets.Add((outerZone, outerX, outerWidth, outerPanels.Count > 0 ? "#" : "—"));
        
        // Special case: if nothing is occupied, show just the inner slot
        if (occupiedCount == 0 && !suppressInner)
        {
            result.Add(new SlotButtonViewModel(
                Label: "—",
                IsSourcePanel: false,
                IsExpansionButton: false,
                X: innerX,
                Y: slotY,
                Width: innerWidth,
                Height: availableHeight,
                TargetZone: innerZone,
                TargetOrder: -100, // Special marker: insert at this column, shuffle others outward
                SourcePanelId: sourcePanelId));
            return;
        }

        // Generate one slot per column position
        foreach (var (zone, x, width, label) in columnTargets)
        {
            result.Add(new SlotButtonViewModel(
                Label: label,
                IsSourcePanel: false,
                IsExpansionButton: false,
                X: x,
                Y: curY,
                Width: width,
                Height: effectiveSlotH,
                TargetZone: zone,
                TargetOrder: -100, // Special marker: insert at this column, shuffle others outward
                SourcePanelId: sourcePanelId));
            curY += effectiveSlotH + SlotGap;
        }
    }
}
