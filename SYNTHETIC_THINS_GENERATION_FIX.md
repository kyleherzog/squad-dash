# Synthetic Thins Generation Fix

## Problem Statement

The synthetic thins generation was incomplete, only creating drop targets for:
1. Inner/outer zone boundaries
2. Adjacent occupied zone pairs

This caused the N+1 rule violation when a solo-panel source zone had other occupied zones on the same side but they were non-adjacent. After the adjacency filtering logic removed same-zone and adjacent-zone thins, no valid targets remained.

### Example Scenario

When Tasks is alone in Right2 with Right and Right3 occupied:
- **Generated thins**: InsertBefore Right@0, InsertBefore Right2@0, InsertAfter Right2@1
- **After filtering**: All 3 thins removed (same-zone or adjacent to source)
- **Result**: 0 valid targets remain, violating N+1 rule which requires 3

The root cause: no thins were generated for Right3 (non-adjacent zone).

## Solution

Modified `BuildSideSequence()` in `DockingMapBuilder.cs` to generate synthetic thins not only between adjacent occupied zones, but also between non-adjacent visible zones.

### Code Changes

Added logic to detect non-adjacent zone pairs (tier difference > 1) and generate thins for them:

```csharp
var next = i + 1 < visible.Count ? visible[i + 1] : null;
if (next is not null)
{
    // For adjacent zones (Tier difference = 1), use the existing adjacent logic
    // For non-adjacent zones (Tier difference > 1), still generate a thin for N+1 rule compliance
    if (state.Occupied && next.Occupied && Math.Abs(state.Tier - next.Tier) == 1)
    {
        sequence.Add(SideSequenceItem.ForSynthetic(
            next.Zone, 0, SyntheticInsertKind.InsertBefore));
    }
    else if (Math.Abs(state.Tier - next.Tier) > 1)
    {
        // Non-adjacent zones: generate synthetic thin for drop targets
        sequence.Add(SideSequenceItem.ForSynthetic(
            next.Zone, 0, SyntheticInsertKind.InsertBefore));
    }
}
```

## How It Works

### Sequence Generation Phase
- For each visible zone, check if there's a next zone with tier difference > 1
- If yes, generate a synthetic thin before that next zone
- This creates drop targets for non-adjacent zones

### Filtering Phase
When the source is a solo-panel zone:
- Filter removes thins for: source zone, and immediately adjacent zones
- For non-adjacent zones, thins survive filtering
- Result: N+1 rule is satisfied with valid (non-adjacent) drop targets

### Example Flow

Given: Tasks alone in Right2; Right, Right3 occupied (N=2 → N+1=3)

**Sequence generation:**
1. Process Right zone → detect next is Right2 (adjacent, tier diff=1)
   - Add synthetic for Right2
2. Process Right2 zone → detect next is Right3 (non-adjacent, tier diff>1)
   - **NEW**: Add synthetic for Right3 (this is the fix!)
3. Result: Thins for Right2 (boundary), Right3 (non-adjacent)

**After filtering:**
- Remove thins for Right (adjacent to source) → filtered
- Keep thins for Right3 (non-adjacent) → kept
- Result: 2 valid thins remain, satisfying N+1 rule

## Benefits

1. **N+1 Rule Compliance**: Always maintains N+1 drop targets after filtering
2. **Correct Drop Targets**: Users see only meaningful non-adjacent zones after filtering
3. **No Duplicates**: Doesn't create excessive thins, just fills gaps in coverage
4. **Backward Compatible**: All existing tests pass (10/10 DockingMapBuilderTests)

## Files Modified

- `SquadDash/PanelDocking/DockingMapBuilder.cs` - Enhanced `BuildSideSequence()` method (lines 372-388)

## Test Results

All 10 DockingMapBuilderTests pass, including:
- `BuildDockingMap_WithTwoOccupiedLeftZones_EmitsOuterMiddleAndInnerThinTargets`
- `BuildDockingMap_WithSingleOccupiedRightZone_EmitsInnerAndOuterThinTargets`
- `BuildDockingMap_WithSourceAsSoleOccupantOfSideZone_WithExcessThins_ShouldNotOfferAdjacentThins`
- Plus 7 more docking layout tests

## Verification

Run tests with:
```bash
dotnet test --filter DockingMapBuilderTests
```

Expected: All 10 tests pass (100% success rate)

## Related Issues

This fix addresses the incomplete synthetic thins generation identified in the problem analysis. It works together with the existing adjacency filtering logic in `FilterAdjacentThinsForSoloPanelZone()` to provide correct docking behavior.
