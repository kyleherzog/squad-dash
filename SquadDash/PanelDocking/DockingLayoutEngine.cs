#nullable enable

namespace SquadDash.PanelDocking;

/// <summary>
/// Pure static logic for docking layout — no WPF dependencies.
/// Replicates the zone filtering and suppression logic of DockingMapBuilder
/// and the move logic of PanelDockingService.MovePanel.
/// </summary>
internal static class DockingLayoutEngine
{
    // ── Zone name helpers ────────────────────────────────────────────────────

    public static string GetZoneDisplayName(DockZone zone) => zone switch
    {
        DockZone.Top    => "Top",
        DockZone.Left   => "Left 1",
        DockZone.Left2  => "Left 2",
        DockZone.Left3  => "Left 3",
        DockZone.Right  => "Right 1",
        DockZone.Right2 => "Right 2",
        DockZone.Right3 => "Right 3",
        _               => zone.ToString()
    };

    public static string GetZoneFileTag(DockZone zone) => zone switch
    {
        DockZone.Top    => "Top",
        DockZone.Left   => "Left1",
        DockZone.Left2  => "Left2",
        DockZone.Left3  => "Left3",
        DockZone.Right  => "Right1",
        DockZone.Right2 => "Right2",
        DockZone.Right3 => "Right3",
        _               => zone.ToString()
    };

    // ── JSON serialization helpers ───────────────────────────────────────────

    public static Dictionary<string, List<string>> LayoutToJson(PanelLayoutData layout)
    {
        var result = new Dictionary<string, List<string>>();
        // When the layout carries visibility information, only serialize panels the user
        // can actually see.  Health/Trace and other always-hidden panels live in Slots
        // (they have a zone assignment) but must not pollute test-case snapshots.
        bool hasVisibility = layout.VisiblePanelIds.Count > 0;
        foreach (var zone in new[] { DockZone.Top, DockZone.Left, DockZone.Left2, DockZone.Left3, DockZone.Right, DockZone.Right2, DockZone.Right3 })
        {
            result[GetZoneDisplayName(zone)] = layout.Slots
                .Where(s => s.Zone == zone && (!hasVisibility || layout.VisiblePanelIds.Contains(s.PanelId)))
                .OrderBy(s => s.Order)
                .Select(s => s.PanelId)
                .ToList();
        }
        return result;
    }

    public static PanelLayoutData ParseLayoutFromJson(Dictionary<string, List<string>> json)
    {
        var displayToZone = new Dictionary<string, DockZone>(StringComparer.OrdinalIgnoreCase)
        {
            ["Top"]     = DockZone.Top,
            ["Left 1"]  = DockZone.Left,
            ["Left 2"]  = DockZone.Left2,
            ["Left 3"]  = DockZone.Left3,
            ["Right 1"] = DockZone.Right,
            ["Right 2"] = DockZone.Right2,
            ["Right 3"] = DockZone.Right3,
        };

        var slots = new List<PanelSlot>();
        var allPanelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (zoneName, panelIds) in json)
        {
            if (!displayToZone.TryGetValue(zoneName, out var zone)) continue;
            for (int i = 0; i < panelIds.Count; i++)
            {
                slots.Add(new PanelSlot(panelIds[i], zone, i));
                allPanelIds.Add(panelIds[i]);
            }
        }

        return new PanelLayoutData
        {
            Slots = slots,
            VisiblePanelIds = allPanelIds,
        };
    }

    // ── Slot button building ─────────────────────────────────────────────────

    /// <summary>
    /// Returns all destination (non-source) slot buttons for the given source panel,
    /// applying the same zone-filtering and suppression logic as DockingMapBuilder.
    /// </summary>
    public static List<SlotButtonInfo> BuildSlotButtons(string sourcePanelId, PanelLayoutData layout)
    {
        var result = new List<SlotButtonInfo>();

        var topPanels    = FilterZone(PanelsInZone(layout, DockZone.Top),    sourcePanelId, layout.VisiblePanelIds);
        var leftPanels   = FilterZone(PanelsInZone(layout, DockZone.Left),   sourcePanelId, layout.VisiblePanelIds);
        var rightPanels  = FilterZone(PanelsInZone(layout, DockZone.Right),  sourcePanelId, layout.VisiblePanelIds);
        var left2Panels  = FilterZone(PanelsInZone(layout, DockZone.Left2),  sourcePanelId, layout.VisiblePanelIds);
        var right2Panels = FilterZone(PanelsInZone(layout, DockZone.Right2), sourcePanelId, layout.VisiblePanelIds);
        var left3Panels  = FilterZone(PanelsInZone(layout, DockZone.Left3),  sourcePanelId, layout.VisiblePanelIds);
        var right3Panels = FilterZone(PanelsInZone(layout, DockZone.Right3), sourcePanelId, layout.VisiblePanelIds);

        bool sourceInTop    = topPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInLeft   = leftPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInRight  = rightPanels.Any(p => Same(p, sourcePanelId));
        bool sourceInLeft2  = left2Panels.Any(p => Same(p, sourcePanelId));
        bool sourceInRight2 = right2Panels.Any(p => Same(p, sourcePanelId));
        bool sourceInLeft3  = left3Panels.Any(p => Same(p, sourcePanelId));
        bool sourceInRight3 = right3Panels.Any(p => Same(p, sourcePanelId));

        // Mirrors DockingMapBuilder suppression logic exactly:
        // Case 1: both outer and inner zones are empty — suppress the outer zone.
        // Case 2: source is the sole occupant of its zone AND the sibling is empty — no-op move.
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

        if (!suppressLeft3)
            AddZoneSlots(result, sourcePanelId, left3Panels, sourceInLeft3, DockZone.Left3);

        if (!suppressLeft2)
            AddZoneSlots(result, sourcePanelId, left2Panels, sourceInLeft2, DockZone.Left2);

        if (!suppressLeft)
            AddZoneSlots(result, sourcePanelId, leftPanels, sourceInLeft, DockZone.Left);

        AddZoneSlots(result, sourcePanelId, topPanels, sourceInTop, DockZone.Top);

        if (!suppressRight)
            AddZoneSlots(result, sourcePanelId, rightPanels, sourceInRight, DockZone.Right);

        if (!suppressRight2)
            AddZoneSlots(result, sourcePanelId, right2Panels, sourceInRight2, DockZone.Right2);

        if (!suppressRight3)
            AddZoneSlots(result, sourcePanelId, right3Panels, sourceInRight3, DockZone.Right3);

        return result;
    }

    private static void AddZoneSlots(
        List<SlotButtonInfo> result,
        string sourcePanelId,
        List<string> zonePanels,
        bool sourceInZone,
        DockZone zone)
    {
        if (zonePanels.Count == 0 && !sourceInZone)
        {
            // Rule D: one destination slot at order=0
            result.Add(new SlotButtonInfo(zone, 0));
            return;
        }

        // Rule B (sourceInZone) or Rule C (!sourceInZone)
        for (int i = 0; i < zonePanels.Count; i++)
        {
            string pid = zonePanels[i];
            if (!Same(pid, sourcePanelId))
                result.Add(new SlotButtonInfo(zone, i));
        }

        if (!sourceInZone)
        {
            // Rule C: append-at-end insertion slot
            result.Add(new SlotButtonInfo(zone, zonePanels.Count));
        }
    }

    // ── Move logic ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a new <see cref="PanelLayoutData"/> with the source panel moved
    /// to the target zone at the target order.  Pure data mutation — no WPF.
    /// </summary>
    public static PanelLayoutData ApplyMove(
        string sourcePanelId,
        DockZone targetZone,
        int targetOrder,
        PanelLayoutData layout)
    {
        var existing = layout.Slots.FirstOrDefault(s =>
            string.Equals(s.PanelId, sourcePanelId, StringComparison.OrdinalIgnoreCase));

        bool sameZone = existing is not null && existing.Zone == targetZone;

        List<PanelSlot> newSlots;

        if (sameZone)
        {
            if (targetOrder < 0)
                return layout;

            var zoneSlots = layout.Slots
                .Where(s => s.Zone == targetZone)
                .OrderBy(s => s.Order)
                .Select(s => s.PanelId)
                .ToList();

            int currentIdx = zoneSlots.FindIndex(id =>
                string.Equals(id, sourcePanelId, StringComparison.OrdinalIgnoreCase));
            int clampedTarget = Math.Clamp(targetOrder, 0, zoneSlots.Count - 1);

            if (currentIdx == clampedTarget)
                return layout;

            zoneSlots.RemoveAt(currentIdx);
            zoneSlots.Insert(Math.Clamp(clampedTarget, 0, zoneSlots.Count), sourcePanelId);

            newSlots = layout.Slots
                .Where(s => s.Zone != targetZone)
                .Concat(zoneSlots.Select((id, i) => new PanelSlot(id, targetZone, i)))
                .ToList();
        }
        else
        {
            var slots = layout.Slots
                .Where(s => !string.Equals(s.PanelId, sourcePanelId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var targetZoneSlots = slots
                .Where(s => s.Zone == targetZone)
                .OrderBy(s => s.Order)
                .ToList();

            int insertAt = targetOrder < 0
                ? targetZoneSlots.Count
                : Math.Clamp(targetOrder, 0, targetZoneSlots.Count);

            var targetIds = targetZoneSlots.Select(s => s.PanelId).ToList();
            targetIds.Insert(insertAt, sourcePanelId);

            slots = slots.Where(s => s.Zone != targetZone).ToList();
            slots.AddRange(targetIds.Select((id, i) => new PanelSlot(id, targetZone, i)));
            newSlots = slots;
        }

        return new PanelLayoutData
        {
            Slots = newSlots,
            VisiblePanelIds = layout.VisiblePanelIds,
        };
    }

    // ── Preview description ──────────────────────────────────────────────────

    /// <summary>
    /// Returns a human-readable description of where a panel would land if the
    /// user clicked the slot at (zone, targetOrder).
    /// Format: "Left 1, 2/3" (position / total slots after move).
    /// </summary>
    public static string GetNormalizedPreviewDescription(
        DockZone zone,
        int targetOrder,
        PanelLayoutData layout)
    {
        var zoneName = GetZoneDisplayName(zone);

        int visibleInZone = layout.Slots
            .Count(s => s.Zone == zone && layout.VisiblePanelIds.Contains(s.PanelId));

        int total    = visibleInZone + 1;
        int position = targetOrder + 1;

        return $"{zoneName}, {position}/{total}";
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static List<string> PanelsInZone(PanelLayoutData layout, DockZone zone) =>
        layout.Slots
              .Where(s => s.Zone == zone)
              .OrderBy(s => s.Order)
              .Select(s => s.PanelId)
              .ToList();

    private static List<string> FilterZone(
        List<string> panels,
        string sourcePanelId,
        IReadOnlySet<string> visiblePanelIds) =>
        panels.Where(p => visiblePanelIds.Contains(p) || Same(p, sourcePanelId)).ToList();

    private static bool Same(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
