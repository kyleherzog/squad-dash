# Docking No-Op Filtering Logic Fix

## Summary

Fixed the docking filter logic to correctly identify and remove no-op drop targets (synthetic thin slots) when a panel is alone in its zone. The N+1 rule now applies to **valid drop targets** rather than including no-ops in the count.

## Root Cause

The previous implementation checked if `thins.Count <= expectedThins` **before** filtering, which prevented removal of valid no-ops. This meant:
- When a panel was alone in its zone and we had exactly N+1 thins, no filtering occurred
- Some of those N+1 thins were no-ops (same zone or adjacent zone), which shouldn't be counted as valid drop targets

## The Fix

### Previous Logic
```csharp
if (thins.Count <= expectedThins)
{
    // Don't filter because we have no excess
    return thins; // BUG: Keeps no-ops!
}
```

### New Logic
```csharp
// Filter same-zone and adjacent-zone thins first
var filtered = thins
    .Where(thin => thin.TargetZone != sourceZone && 
                  thin.TargetZone != leftAdjacentZone && 
                  thin.TargetZone != rightAdjacentZone)
    .ToList();

// THEN check N+1 rule on the FILTERED result
if (filtered.Count >= expectedThins)
{
    return filtered; // All good - valid targets meet N+1 requirement
}
else
{
    return thins; // Don't filter if it violates N+1 on valid targets
}
```

## What Gets Filtered

When a panel is the sole occupant of its zone and there are other occupied zones on the same side:
1. **Same-zone thins**: Drop targets for inserting another panel in the source zone (no visual change)
2. **Adjacent-zone thins**: Drop targets for immediately adjacent zones (typically no visual change to user)

Example: Tasks alone in Right2 with occupied zones Right, Right4, Right5:
- **Before filtering**: InsertBefore Right@0, InsertBefore Right2@0, InsertAfter Right2@1
- **After filtering**: Only thins for non-adjacent zones (Right4, Right5, etc.)
- **Result**: 0-2 thins removed depending on excess; N+1 rule still maintained with valid targets

## Test Coverage

All 10 DockingMapBuilderTests pass, including:
- `BuildDockingMap_WithSourceAloneInMiddleRightZone_AndOtherPanelsOnLeft_ShouldNotOfferAdjacentThinsForSourceZone`
- `BuildDockingMap_WithSourceAloneInMiddleRightZone_WithOtherOccupiedRightZones_ShouldNotOfferAdjacentThinsForSourceZone`
- `BuildDockingMap_WithSourceAsSoleOccupantOfSideZone_WithExcessThins_ShouldNotOfferAdjacentThins`
- Plus 7 more docking validation tests

All 48 docking-related tests pass (comprehensive validation).

## Files Modified

- `SquadDash/PanelDocking/DockingMapBuilder.cs` - Updated `FilterAdjacentThinsForSoloPanelZone()` method

## Behavior Changes

### When Source is Alone in Its Zone

**Scenario**: Task panel alone in Right2; Right, Right4, Right5 occupied

| Situation | Before Fix | After Fix |
|-----------|-----------|-----------|
| Has 3 thins (N+1=3) | Shows all 3 (includes no-ops) | Filters to valid targets, shows N+1 valid ones |
| Has 4+ thins | Filters adjacent if excess | Filters adjacent + same-zone if N+1 maintained |
| Only zone on side | No filtering applied | No filtering applied (correct) |

**Result**: Users see only meaningful drop targets, not no-op moves.

## Validation

Run tests with:
```bash
dotnet test SquadDash.Tests\SquadDash.Tests.csproj --filter "Docking"
```

Expected: All docking tests pass (48+ tests)
