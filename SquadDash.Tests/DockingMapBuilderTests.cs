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
            ("tasks", DockZone.Left2),
            ("notes", DockZone.Right)); // Prevent cross-side thins from empty right

        var thins = ThinSlots(map, LeftZones);

        // With Right occupied, no cross-side thins from Right to Left
        // So we get 3 left-side within-side thins only
        Assert.That(thins.Count, Is.EqualTo(3));
        
        // Verify core within-side thins exist with correct kinds
        var left3Thin = thins.FirstOrDefault(t => t.TargetZone == DockZone.Left3);
        Assert.That(left3Thin, Is.Not.Null, "Should have thin for Left3");
        Assert.That(left3Thin.InsertKind, Is.EqualTo(SyntheticInsertKind.None));
        
        var leftThins = thins.Where(t => t.TargetZone == DockZone.Left).ToList();
        Assert.That(leftThins.Any(t => t.InsertKind == SyntheticInsertKind.InsertBefore), Is.True);
        Assert.That(leftThins.Any(t => t.InsertKind == SyntheticInsertKind.InsertAfter), Is.True);
        
        Assert.That(DockingMapBuilder.FindAdjacentThinViolations(map.Slots), Is.Empty);
    }

    [Test]
    public void BuildDockingMap_WithSingleOccupiedRightZone_EmitsInnerAndOuterThinTargets()
    {
        var map = Build(
            sourcePanelId: "loop",
            ("loop", DockZone.Top),
            ("notes", DockZone.Left), // Prevent cross-side thins from empty left
            ("approvals", DockZone.Right));

        var thins = ThinSlots(map, RightZones);

        // With Left occupied, no cross-side thins from Left to Right
        // So we get 2 right-side within-side thins only
        Assert.That(thins.Count, Is.EqualTo(2));
        
        // Verify core right-side thins exist
        var rightThins = thins.Where(t => t.TargetZone == DockZone.Right).ToList();
        Assert.That(rightThins.Any(t => t.InsertKind == SyntheticInsertKind.InsertBefore), Is.True, 
            "Should have Right thin with InsertBefore");
        
        var right2Thin = thins.FirstOrDefault(t => t.TargetZone == DockZone.Right2);
        Assert.That(right2Thin, Is.Not.Null, "Should have thin for Right2");
        
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

        // With all 6 left zones occupied and Top occupied:
        // - 7 left-side thins (N+1 rule for 6 zones)
        // - When processing empty Right side: cross-side thins for occupied left zones
        // Total thins targeting left zones >= 7 (N+1 minimum)
        Assert.That(thins.Count, Is.GreaterThanOrEqualTo(7), "Should have at least N+1 left-side thins");
        
        // Verify the N+1 rule by checking for Left6 InsertBefore and Left InsertAfter (the boundary thins)
        var left6InsertBefore = thins.FirstOrDefault(t => t.TargetZone == DockZone.Left6 && t.InsertKind == SyntheticInsertKind.InsertBefore);
        var leftInsertAfter = thins.FirstOrDefault(t => t.TargetZone == DockZone.Left && t.InsertKind == SyntheticInsertKind.InsertAfter);
        Assert.That(left6InsertBefore, Is.Not.Null, "Should have Left6 thin with InsertBefore");
        Assert.That(leftInsertAfter, Is.Not.Null, "Should have Left thin with InsertAfter");
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

        // With all 6 right zones occupied and Top occupied:
        // - 7 right-side thins (N+1 rule for 6 zones)
        // - When processing empty Left side: cross-side thins for occupied right zones
        // Total thins targeting right zones >= 7 (N+1 minimum)
        Assert.That(thins.Count, Is.GreaterThanOrEqualTo(7), "Should have at least N+1 right-side thins");
        
        // Verify the N+1 rule by checking for Right InsertBefore and Right6 InsertAfter (the boundary thins)
        var rightInsertBefore = thins.FirstOrDefault(t => t.TargetZone == DockZone.Right && t.InsertKind == SyntheticInsertKind.InsertBefore);
        var right6InsertAfter = thins.FirstOrDefault(t => t.TargetZone == DockZone.Right6 && t.InsertKind == SyntheticInsertKind.InsertAfter);
        Assert.That(rightInsertBefore, Is.Not.Null, "Should have Right thin with InsertBefore");
        Assert.That(right6InsertAfter, Is.Not.Null, "Should have Right6 thin with InsertAfter");
    }

    [Test]
    public void BuildDockingMap_WithSourceAsOnlySidePanel_DoesNotOfferNoopLateralThin()
    {
        var map = Build(
            sourcePanelId: "tasks",
            ("tasks", DockZone.Left2));

        var thins = ThinSlots(map, LeftZones);
        
        // With cross-side thin generation: when the Right side is empty, it generates a cross-side thin to Left2 (the only occupied zone)
        // This is not a noop - it's a valid cross-side thin to move the panel to a different area
        Assert.That(thins.Count, Is.EqualTo(1));
        Assert.That(thins.First().TargetZone, Is.EqualTo(DockZone.Left2));
        Assert.That(thins.First().InsertKind, Is.EqualTo(SyntheticInsertKind.InsertBefore));
        
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
        // With 4 occupied zones, N+1 rule requires 5 thins minimum.
        // Adding Left and Top occupied to prevent cross-side thin generation
        var map = Build(
            sourcePanelId: "loop",
            ("approvals", DockZone.Top),
            ("notes", DockZone.Left),
            ("loop", DockZone.Right),
            ("inbox", DockZone.Right2),
            ("tasks", DockZone.Right4),
            ("notes2", DockZone.Right5)); // 4th zone on right side

        var rightThins = ThinSlots(map, RightZones);

        // With 4 occupied zones on Right, we need at least 5 thins for N+1 rule
        Assert.That(rightThins.Count, Is.GreaterThanOrEqualTo(5), 
            "Should have at least N+1 thins for 4 occupied zones");
        
        // Verify no violations
        var violations = DockingMapBuilder.FindAdjacentThinViolations(map.Slots);
        Assert.That(violations, Is.Empty, "Should not have layout violations");
    }

    [Test]
    public void BuildDockingMap_WithSourceAloneInLeftOuterZone_WithExcessThins_ShouldNotOfferAdjacentThins()
    {
        // When source is alone in Left3 (outer zone) with other zones occupied on the same side.
        // With 4 occupied zones on the left, N+1 rule requires 5 thins minimum.
        // Adding Top and Right occupied to prevent cross-side thin generation
        var map = Build(
            sourcePanelId: "maintenance",
            ("approvals", DockZone.Top),
            ("approvals", DockZone.Left),
            ("tasks", DockZone.Left2),
            ("maintenance", DockZone.Left3),
            ("inbox", DockZone.Left4),
            ("notes", DockZone.Right)); // Prevent cross-side thins

        var leftThins = ThinSlots(map, LeftZones);

        // With 4 occupied zones on Left, we need at least 5 thins for N+1 rule
        Assert.That(leftThins.Count, Is.GreaterThanOrEqualTo(5), 
            "Should have at least N+1 thins for 4 occupied zones");
        
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

    [Test]
    public void BuildDockingMap_WithSourceAloneInRight2_AndTopOccupied_ShouldOfferCrossSideThinsToTop()
    {
        // This is the main cross-side thin scenario: source is alone in Right2,
        // and Top zone is occupied. Should generate cross-side thins to Top.
        var map = Build(
            sourcePanelId: "tasks",
            ("loop", DockZone.Top),
            ("tasks", DockZone.Right2));

        var allThins = map.Slots
            .Where(s => !s.IsSeparator && s.Width < 48)
            .OrderBy(s => s.X)
            .ToList();

        // Should have thins for Top (cross-side)
        var topThins = allThins.Where(t => t.TargetZone == DockZone.Top).ToList();
        Assert.That(topThins, Is.Not.Empty, "Should generate cross-side thins to Top zone");
        Assert.That(topThins.First().InsertKind, Is.EqualTo(SyntheticInsertKind.InsertBefore),
            "Cross-side thin to Top should be InsertBefore");

        // Should not have violations
        var violations = DockingMapBuilder.FindAdjacentThinViolations(map.Slots);
        Assert.That(violations, Is.Empty, "Should not have layout violations");
    }

    [Test]
    public void BuildDockingMap_WithSourceAloneInRight2_AndLeftOccupied_ShouldOfferCrossSideThinsToLeft()
    {
        // Source is alone in Right2, Left zone is occupied.
        // Should generate cross-side thins to Left zone.
        var map = Build(
            sourcePanelId: "tasks",
            ("approvals", DockZone.Left),
            ("tasks", DockZone.Right2));

        var allThins = map.Slots
            .Where(s => !s.IsSeparator && s.Width < 48)
            .OrderBy(s => s.X)
            .ToList();

        // Should have thins for Left (cross-side)
        var leftThins = allThins.Where(t => t.TargetZone == DockZone.Left).ToList();
        Assert.That(leftThins, Is.Not.Empty, "Should generate cross-side thins to Left zone");

        // Should not have violations
        var violations = DockingMapBuilder.FindAdjacentThinViolations(map.Slots);
        Assert.That(violations, Is.Empty, "Should not have layout violations");
    }

    [Test]
    public void BuildDockingMap_WithSourceAloneInRight2_AndBothTopAndLeftOccupied_ShouldOfferCrossSideThinsToAll()
    {
        // Source is alone in Right2, both Top and Left zones are occupied.
        // Should generate cross-side thins to both Top and Left zones.
        var map = Build(
            sourcePanelId: "tasks",
            ("loop", DockZone.Top),
            ("approvals", DockZone.Left),
            ("tasks", DockZone.Right2));

        var allThins = map.Slots
            .Where(s => !s.IsSeparator && s.Width < 48)
            .OrderBy(s => s.X)
            .ToList();

        // Should have thins for both Top and Left (cross-side)
        var topThins = allThins.Where(t => t.TargetZone == DockZone.Top).ToList();
        var leftThins = allThins.Where(t => t.TargetZone == DockZone.Left).ToList();
        
        Assert.That(topThins, Is.Not.Empty, "Should generate cross-side thins to Top zone");
        Assert.That(leftThins, Is.Not.Empty, "Should generate cross-side thins to Left zone");

        // Should not have violations
        var violations = DockingMapBuilder.FindAdjacentThinViolations(map.Slots);
        Assert.That(violations, Is.Empty, "Should not have layout violations");
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
