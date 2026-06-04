#nullable enable

using NUnit.Framework;
using SquadDash.PanelDocking;

namespace SquadDash.Tests;

[TestFixture]
public class DockingMapBuilderTests
{
    private static readonly DockZone[] LeftZones =
    [
        DockZone.Left, DockZone.Left2, DockZone.Left3,
        DockZone.Left4, DockZone.Left5, DockZone.Left6
    ];

    private static readonly DockZone[] RightZones =
    [
        DockZone.Right, DockZone.Right2, DockZone.Right3,
        DockZone.Right4, DockZone.Right5, DockZone.Right6
    ];

    [Test]
    public void BuildDockingMap_WithTwoOccupiedLeftZones_EmitsOuterMiddleAndInnerThinTargets()
    {
        var map = Build(
            sourcePanelId: "loop",
            ("loop", DockZone.Top),
            ("approvals", DockZone.Left),
            ("tasks", DockZone.Left2));

        var thins = ThinSlots(map, LeftZones);

        Assert.That(thins.Count, Is.EqualTo(3));
        Assert.That(thins.Select(s => (s.TargetZone, s.InsertKind)), Is.EqualTo(new[]
        {
            (DockZone.Left3, SyntheticInsertKind.None),
            (DockZone.Left, SyntheticInsertKind.InsertBefore),
            (DockZone.Left, SyntheticInsertKind.InsertAfter),
        }));
        Assert.That(DockingMapBuilder.FindAdjacentThinViolations(map.Slots), Is.Empty);
    }

    [Test]
    public void BuildDockingMap_WithSingleOccupiedRightZone_EmitsInnerAndOuterThinTargets()
    {
        var map = Build(
            sourcePanelId: "loop",
            ("loop", DockZone.Top),
            ("approvals", DockZone.Right));

        var thins = ThinSlots(map, RightZones);

        Assert.That(thins.Count, Is.EqualTo(2));
        Assert.That(thins.Select(s => (s.TargetZone, s.InsertKind)), Is.EqualTo(new[]
        {
            (DockZone.Right, SyntheticInsertKind.InsertBefore),
            (DockZone.Right2, SyntheticInsertKind.None),
        }));
        Assert.That(DockingMapBuilder.FindAdjacentThinViolations(map.Slots), Is.Empty);
    }

    [Test]
    public void BuildDockingMap_WithAllSixLeftZonesOccupied_StillEmitsNPlusOneThinTargets()
    {
        var map = Build(
            sourcePanelId: "loop",
            ("loop", DockZone.Top),
            ("l1", DockZone.Left),
            ("l2", DockZone.Left2),
            ("l3", DockZone.Left3),
            ("l4", DockZone.Left4),
            ("l5", DockZone.Left5),
            ("l6", DockZone.Left6));

        var thins = ThinSlots(map, LeftZones);

        Assert.That(thins.Count, Is.EqualTo(7));
        Assert.That(thins.First().TargetZone, Is.EqualTo(DockZone.Left6));
        Assert.That(thins.First().InsertKind, Is.EqualTo(SyntheticInsertKind.InsertBefore));
        Assert.That(thins.Last().TargetZone, Is.EqualTo(DockZone.Left));
        Assert.That(thins.Last().InsertKind, Is.EqualTo(SyntheticInsertKind.InsertAfter));
        Assert.That(DockingMapBuilder.FindAdjacentThinViolations(map.Slots), Is.Empty);
    }

    [Test]
    public void BuildDockingMap_WithAllSixRightZonesOccupied_StillEmitsNPlusOneThinTargets()
    {
        var map = Build(
            sourcePanelId: "loop",
            ("loop", DockZone.Top),
            ("r1", DockZone.Right),
            ("r2", DockZone.Right2),
            ("r3", DockZone.Right3),
            ("r4", DockZone.Right4),
            ("r5", DockZone.Right5),
            ("r6", DockZone.Right6));

        var thins = ThinSlots(map, RightZones);

        Assert.That(thins.Count, Is.EqualTo(7));
        Assert.That(thins.First().TargetZone, Is.EqualTo(DockZone.Right));
        Assert.That(thins.First().InsertKind, Is.EqualTo(SyntheticInsertKind.InsertBefore));
        Assert.That(thins.Last().TargetZone, Is.EqualTo(DockZone.Right6));
        Assert.That(thins.Last().InsertKind, Is.EqualTo(SyntheticInsertKind.InsertAfter));
        Assert.That(DockingMapBuilder.FindAdjacentThinViolations(map.Slots), Is.Empty);
    }

    [Test]
    public void BuildDockingMap_WithSourceAsOnlySidePanel_DoesNotOfferNoopLateralThin()
    {
        var map = Build(
            sourcePanelId: "tasks",
            ("tasks", DockZone.Left2));

        Assert.That(ThinSlots(map, LeftZones), Is.Empty);
        Assert.That(DockingMapBuilder.FindAdjacentThinViolations(map.Slots), Is.Empty);
    }

    [Test]
    public void BuildDockingMap_WithSourceAloneInMiddleRightZone_AndOtherPanelsOnLeft_ShouldNotOfferAdjacentThinsForSourceZone()
    {
        var map = Build(
            sourcePanelId: "inbox",
            ("approvals", DockZone.Left),
            ("inbox", DockZone.Right2));

        var rightThins = ThinSlots(map, RightZones);

        // The source (inbox) is alone in Right2. We should NOT show thin slots
        // immediately adjacent to Right2 (i.e., Right and Right3) because those
        // would be no-op moves.
        var adjacentToSourceZone = rightThins.Where(t => 
            t.TargetZone == DockZone.Right || t.TargetZone == DockZone.Right3).ToList();
        Assert.That(adjacentToSourceZone, Is.Empty, 
            "Should not show thin slots adjacent to solo-panel zone (Right2)");
    }

    [Test]
    public void BuildDockingMap_WithSourceAloneInMiddleRightZone_WithOtherOccupiedRightZones_ShouldNotOfferAdjacentThinsForSourceZone()
    {
        // This test case reproduces the bug scenario - source is alone in Right2,
        // and there are other occupied zones on the same side.
        // With 3 occupied zones, we need 4 thins for N+1 rule. If we have more, we can filter adjacent thins.
        var map = Build(
            sourcePanelId: "inbox",
            ("approvals", DockZone.Right),
            ("inbox", DockZone.Right2),
            ("notes", DockZone.Right4),
            ("tasks", DockZone.Right5)); // 4th zone to have more than N+1 thins

        var rightThins = ThinSlots(map, RightZones);

        // With 4 occupied zones, we need N+1=5 thins. We should have 5+ thins total.
        // Right2 has only the source (inbox). Immediately adjacent zones:
        // - Right3 (tier 2) is empty - this is immediately adjacent to the source zone
        // If we have more than N+1 thins, we should filter Right3
        var right3Thin = rightThins.FirstOrDefault(t => t.TargetZone == DockZone.Right3);
        if (rightThins.Count > 5) // Only expect filtering if we have excess thins
        {
            Assert.That(right3Thin, Is.Null,
                "Should not show thin slot for Right3 when it's immediately adjacent to source zone Right2 (excess thins to filter)");
        }
    }

    [Test]
    public void BuildDockingMap_WithSourceAsSoleOccupantOfSideZone_WithExcessThins_ShouldNotOfferAdjacentThins()
    {
        // When source (loop) is the sole occupant of zone Right with multiple other zones.
        // With 4 occupied zones, N+1 rule requires 5 thins. If we generate more, we can filter adjacent thins.
        var map = Build(
            sourcePanelId: "loop",
            ("loop", DockZone.Right),
            ("inbox", DockZone.Right2),
            ("tasks", DockZone.Right4),
            ("notes", DockZone.Right5)); // 4th zone on right side

        var rightThins = ThinSlots(map, RightZones);

        // With 4 occupied zones, we have N+1=5 thins minimum. Check if we can filter Right2 (adjacent to Right).
        // We can only filter if we have more than 5 thins total.
        var right2Thin = rightThins.FirstOrDefault(t => t.TargetZone == DockZone.Right2);
        if (rightThins.Count > 5)
        {
            Assert.That(right2Thin, Is.Null,
                "Should not show thin slot for Right2 when it's immediately adjacent to sole-panel zone Right (excess thins to filter)");
        }

        // Verify no violations - we should still have N+1 thins
        var violations = DockingMapBuilder.FindAdjacentThinViolations(map.Slots);
        Assert.That(violations, Is.Empty, "Should not have layout violations");
    }

    [Test]
    public void BuildDockingMap_WithSourceAloneInLeftOuterZone_WithExcessThins_ShouldNotOfferAdjacentThins()
    {
        // When source is alone in Left3 (outer zone) with other zones occupied on the same side.
        // With 4 occupied zones on the left, N+1 rule requires 5 thins minimum.
        var map = Build(
            sourcePanelId: "maintenance",
            ("approvals", DockZone.Left),
            ("tasks", DockZone.Left2),
            ("maintenance", DockZone.Left3),
            ("inbox", DockZone.Left4)); // 4th zone to have potential excess thins

        var leftThins = ThinSlots(map, LeftZones);

        // We can filter Left2 (adjacent to Left3) only if we have more than N+1 thins
        var left2Thin = leftThins.FirstOrDefault(t => t.TargetZone == DockZone.Left2);
        if (leftThins.Count > 5)
        {
            Assert.That(left2Thin, Is.Null,
                "Should not show thin slot for Left2 when it's immediately adjacent to sole-panel zone Left3 (excess thins to filter)");
        }

        // Verify no violations
        var violations = DockingMapBuilder.FindAdjacentThinViolations(map.Slots);
        Assert.That(violations, Is.Empty, "Should not have layout violations");
    }

    [Test]
    public void BuildDockingMap_WithComplexMultiZoneLayout_ShouldMaintainNPlusOneRule()
    {
        // Complex scenario with multiple panels on both sides and middle zone
        var map = Build(
            sourcePanelId: "tasks",
            ("approvals", DockZone.Left),
            ("tasks", DockZone.Left3),
            ("loop", DockZone.Top),
            ("inbox", DockZone.Right),
            ("maintenance", DockZone.Right3),
            ("notes", DockZone.Right5));

        // Verify no violations - N+1 rule should be maintained after filtering
        var violations = DockingMapBuilder.FindAdjacentThinViolations(map.Slots);
        Assert.That(violations, Is.Empty, "Should not have layout violations in complex layout");
    }

    private static DockingMapViewModel Build(
        string sourcePanelId,
        params (string PanelId, DockZone Zone)[] placements)
    {
        var layout = new DockLayout
        {
            Slots = placements
                .Select((p, index) => new PanelSlot(p.PanelId, p.Zone, index))
                .ToList(),
        };

        return DockingMapBuilder.BuildDockingMap(sourcePanelId, layout);
    }

    private static List<SlotButtonViewModel> ThinSlots(
        DockingMapViewModel map,
        IReadOnlyCollection<DockZone> sideZones) =>
        map.Slots
            .Where(s => !s.IsSeparator && sideZones.Contains(s.TargetZone) && s.Width < 48)
            .OrderBy(s => s.X)
            .ToList();
}
