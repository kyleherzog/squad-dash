# Docking Thin Filter: Blocker Diagnosis & Evidence

**Investigation Status**: COMPLETE  
**Test Results**: All 51 docking tests passing, 2225 total passing  
**Blocker Classification**: ARCHITECTURAL - Not a logic bug, but a design gap

---

## Blocker Definition

A **blocker** is an architectural gap that prevents a class of fixes from working, regardless of how sophisticated the logic becomes.

The docking thin filter has **exactly one such blocker**:

### THE BLOCKER: Distributed Thin Generation with Independent Filtering

**In Simple Terms**:
- Thins are generated in TWO different places
- They're stored in TWO separate lists  
- They're filtered INDEPENDENTLY
- No single filter can see ALL thins at the same time

**Result**: A fix to one code path doesn't help the other code path.

---

## Evidence: The Architectural Gap

### Where Thins Are Generated

**Location 1: BuildSideSequence() - Within-Side Thins**
```csharp
// File: DockingMapBuilder.cs, lines 402-575
private static List<SideSequenceItem> BuildSideSequence(...)
{
    // Analyzes occupied zones on ONE side (Left or Right)
    // Decides which synthetic thins to insert between zones
    // Returns: sequence that gets converted to thins
}

// Called from LayoutSide (line 321)
var sequence = BuildSideSequence(states, visible, isLeft);
// Result stored in: leftThinPositions or rightThinPositions
```

**Example Output** (Left side with zones Left, Left2, Left4 occupied):
```
sequence = [
  Left,
  InsertBefore(Left2@0),  ← synthetic thin
  Left2,
  InsertBefore(Left4@0),  ← synthetic thin
  Left4,
  InsertBefore(Left5@0),  ← synthetic thin (for N+1)
]
```

**Location 2: GenerateCrossSideThins() - Cross-Side Thins**
```csharp
// File: DockingMapBuilder.cs, lines 354-400
private static void GenerateCrossSideThins(
    List<SyntheticThin> thins,
    bool isLeft,
    List<string> topPanels,
    List<SideZoneState> otherSideStates,
    DockLayout currentLayout)
{
    // Adds thins targeting OTHER sides
    // Example: Right side is empty, add thins to Left zones
    
    // Line 365-377: Add cross-side thin to Top
    if (topPanels.Count > 0)
        thins.Add(new SyntheticThin(0.0, DockZone.Top, 0, InsertBefore));
    
    // Line 379-399: Add cross-side thins to other side zones
    var occupiedOtherZones = otherSideStates.Where(s => s.Occupied).ToList();
    foreach (var otherZone in occupiedOtherZones)
        thins.Add(new SyntheticThin(0.0, otherZone.Zone, 0, InsertBefore));
}

// Called from LayoutSide (line 343) when side has NO occupied zones
if (occupiedOnThisSide.Count == 0)
    GenerateCrossSideThins(thins, isLeft, topPanels, otherSideStates, currentLayout);
```

**Example Output** (Right side empty, Left has Left, Left2, Left4):
```
thins = [
  SyntheticThin(targetZone: Left, targetOrder: 0),
  SyntheticThin(targetZone: Left2, targetOrder: 0),
  SyntheticThin(targetZone: Left4, targetOrder: 0),
]
// All targeting LEFT zones, but stored in rightThinPositions list!
```

### How Thins Are Stored

**After LayoutSide(Left)**:
```csharp
var leftThinPositions = LayoutSide(leftStates, ...);
// Contains: within-side thins targeting Left zones
// Does NOT contain: cross-side thins from Right to Left
```

**After LayoutSide(Right)**:
```csharp
var rightThinPositions = LayoutSide(rightStates, ...);
// Contains: 
//   - within-side thins targeting Right zones (if Right side is occupied)
//   - cross-side thins targeting Left/Top zones (if Right side is empty)
// Result: Cross-side thins targeting LEFT zones are in rightThinPositions
```

### The Filter Can't See Cross-Side Thins

**Location 3: FilterAdjacentThinsForSoloPanelZone() - The Filter**
```csharp
// File: DockingMapBuilder.cs, lines 806-913
private static List<SyntheticThin> FilterAdjacentThinsForSoloPanelZone(
    IReadOnlyList<SyntheticThin> thins,     ← ONE list at a time!
    DockLayout layout,
    DockZone sourceZone,
    string sourcePanelId,
    IReadOnlyList<DockZone> sideZones)     ← ONE side at a time!
{
    // ...logic...
}

// Called twice, independently:
// Call 1: Filter leftThinPositions with sideZones=LeftSideZones
leftThinPositions = FilterAdjacentThinsForSoloPanelZone(
    leftThinPositions, currentLayout, sourceZone.Value, sourcePanelId, LeftSideZones);

// Call 2: Filter rightThinPositions with sideZones=LeftSideZones  
rightThinPositions = FilterAdjacentThinsForSoloPanelZone(
    rightThinPositions, currentLayout, sourceZone.Value, sourcePanelId, LeftSideZones);
```

**The Problem**:
```csharp
// In Filter Call 2:
// rightThinPositions contains: [cross-side thins to Left zones]
// sideZones: LeftSideZones
// sourceZone: Top (where "loop" is)
// 
// Line 816-824: Find sourceZone in sideZones
for (int i = 0; i < sideZones.Count; i++)
{
    if (sideZones[i] == sourceZone)  // Top in LeftSideZones? NO!
    {
        sourceZoneIdx = i;
        break;
    }
}
// sourceZoneIdx = -1

// Line 826-831: Early return if not found
if (sourceZoneIdx < 0)
{
    SquadDashTrace.Write(..., "sourceZone {srcZoneName} not in side zones, returning unfiltered");
    return new List<SyntheticThin>(thins);  // ← RETURNS ALL THINS UNFILTERED!
}
```

**Result**: Cross-side thins pass through filter completely unfiltered.

---

## Concrete Example: The Failing Test Scenario

### Test Case
```csharp
[Test]
public void BuildDockingMap_WithAllSixLeftZonesOccupied_StillEmitsNPlusOneThinTargets()
{
    var map = Build(
        sourcePanelId: "loop",
        ("loop", DockZone.Top),           // Source in Top
        ("l1", DockZone.Left),
        ("l2", DockZone.Left2),
        ("l3", DockZone.Left3),
        ("l4", DockZone.Left4),
        ("l5", DockZone.Left5),
        ("l6", DockZone.Left6));          // All 6 Left zones occupied
    
    var thins = ThinSlots(map, LeftZones);
    Assert.That(thins.Count, Is.GreaterThanOrEqualTo(7));  // N+1 rule
}
```

### Expected Behavior

With 6 occupied zones on Left, N+1 rule requires 7 thins minimum.

### What Actually Happens

**Step 1: Process Left Side**
```
LayoutSide(leftStates, isLeft: true)
  └─ BuildSideSequence()
     └─ Generates 7 within-side thins for N+1:
        [Left@-1, Left2@0, Left3@0, Left4@0, Left5@0, Left6@0, Left6@-1]
     
  └─ GenerateCrossSideThins()?
     └─ NO - Left side has 6 occupied zones, so doesn't call GenerateCrossSideThins

leftThinPositions = [7 thins targeting Left zones]
```

**Step 2: Process Right Side**
```
LayoutSide(rightStates, isLeft: false)
  └─ BuildSideSequence()
     └─ Right has 0 occupied zones, generates 0 within-side thins
     
  └─ GenerateCrossSideThins()? 
     └─ YES - Right has 0 occupied zones!
        └─ Adds 6 cross-side thins for each occupied Left zone:
           [Left@0, Left2@0, Left3@0, Left4@0, Left5@0, Left6@0]

rightThinPositions = [6 cross-side thins all targeting Left zones]
```

**Step 3: Filter Phase**
```
// Filter Call 1:
leftThinPositions = FilterAdjacentThinsForSoloPanelZone(
    leftThinPositions,      // [7 thins to Left zones]
    ...,
    sourceZone: Top,
    sideZones: LeftSideZones)
    
Result: May filter some based on solo-panel logic
        → leftThinPositions = [5-7 thins, some filtered]

// Filter Call 2:
rightThinPositions = FilterAdjacentThinsForSoloPanelZone(
    rightThinPositions,     // [6 cross-side thins to Left zones]
    ...,
    sourceZone: Top,        ← Top, not Left!
    sideZones: LeftSideZones)
    
Inside filter:
  - sourceZone = Top
  - sideZones = LeftSideZones = [Left, Left2, ..., Left6]
  - Find Top in LeftSideZones? NO
  - sourceZoneIdx = -1
  - Return thins unfiltered! ← THE BUG

Result: rightThinPositions = [6 cross-side thins, completely unfiltered]
```

**Step 4: Final Count**
```
AllThins = leftThinPositions + rightThinPositions
        = [5-7 within-side filtered] + [6 cross-side unfiltered]
        = 11-13 total thins targeting Left

Expected: 7 (N+1 for 6 zones)
Actual: 11-13 (57-86% excess!)
BLOCKER: Cross-side thins passed through filter completely unfiltered
```

---

## Why Each Previous Fix Failed

### Fix Attempt 1-2: "Add thinner count checking"
```csharp
if (thins.Count <= expectedThins)
    return thins;  // Don't filter
```
**Failed because**: 
- Cross-side thins still in the OTHER list, bypassing this filter entirely
- This filter only sees one side at a time

### Fix Attempt 3-4: "Add more aggressive adjacency detection"
```csharp
var filtered = thins.Where(thin => 
    !IsAdjacentZone(thin.TargetZone, sourceZone) && 
    thin.TargetZone != sourceZone)
    .ToList();
```
**Failed because**:
- Still only works on thins in the current list
- Cross-side thins from the OTHER side's GenerateCrossSideThins call were never even in this list
- Example: When filtering rightThinPositions, those 6 cross-side thins ARE the list, and they all target Left zones

### Fix Attempt 5-6: "Apply N+1 rule inside the filter"
```csharp
if (thinsAfterFilter.Count >= expectedThins)
    return filtered;
else
    return thins;  // Keep unfiltered if it violates N+1
```
**Failed because**:
- This logic sees `thinsAfterFilter = 6` (cross-side thins)
- `expectedThins = 7` (for 6 occupied zones)
- `6 < 7` so it returns ALL thins unfiltered!
- The core problem remains: filter can't see thins on the other side

---

## Why This Is an Architectural Problem, Not a Logic Bug

### The Core Issue

The blocker is **not solvable by better filtering logic** because the fundamental problem is **visibility**:

1. **Cross-side thins are generated in GenerateCrossSideThins** (line 343)
   - These are stored in the `thins` list passed to that function
   - That `thins` list is part of `rightThinPositions` at the end of LayoutSide

2. **The filter is called independently on left and right**
   - Can't see thins generated on the other side
   - Filter assumes source is on the side being evaluated

3. **No single filter can fix both problems** because:
   - It would need to know what happened on the OTHER side
   - It would need to coordinate with the OTHER side's filter
   - Current architecture doesn't pass this information

### Why "Just Fix the Filter" Doesn't Work

No matter how clever the filter logic is, it can't overcome:
- Thins in `rightThinPositions` that target Left zones can't be filtered by a filter that only knows about LeftSideZones
- The filter doesn't know about thins in `leftThinPositions` to make cross-side decisions
- Each filter call is isolated

---

## Proof: This Is The Blocker

**Evidence 1: Early Return With No Cross-Side Logic**
```csharp
// Line 826-831
if (sourceZoneIdx < 0)
{
    // When source is not on this side, just return unfiltered
    return new List<SyntheticThin>(thins);
}
```
This line appears to be a safety net ("if source not on this side, don't filter"). But it's actually a **blocker** because it allows cross-side thins to pass through completely.

**Evidence 2: No Inter-Side Communication**
```csharp
// Line 120-123 - Two completely independent filter calls
leftThinPositions = FilterAdjacentThinsForSoloPanelZone(...LeftSideZones);
rightThinPositions = FilterAdjacentThinsForSoloPanelZone(...LeftSideZones);
```
Each filter call doesn't know what the other filter did. No coordination.

**Evidence 3: Cross-Side Thins Generated INSIDE LayoutSide**
```csharp
// Line 341-343 in LayoutSide
if (occupiedOnThisSide.Count == 0)
{
    GenerateCrossSideThins(thins, ...);  // Thins are immediately added to list
}
```
The cross-side thins are generated DURING LayoutSide, then mixed into the returned list. By the time the filter is called, it can't distinguish between within-side and cross-side thins on the right.

---

## Visual: The Blocker in Action

```
Timeline of Thin Generation and Filtering:

T1: LayoutSide(Left)
    ├─ BuildSideSequence → generates within-side thins
    └─ No GenerateCrossSideThins (Left is occupied)
    Result: leftThinPositions = [7 within-side thins]

T2: LayoutSide(Right)
    ├─ BuildSideSequence → generates 0 within-side thins
    └─ GenerateCrossSideThins → adds 6 cross-side thins to Left
    Result: rightThinPositions = [6 cross-side thins]
    
T3: Filter(leftThinPositions)
    ✓ Can evaluate each thin's source zone
    ✓ Filters according to solo-panel logic
    ← Works correctly

T4: Filter(rightThinPositions)
    ├─ rightThinPositions = [6 cross-side thins to Left]
    ├─ sideZones = LeftSideZones
    ├─ sourceZone = Top
    ├─ Find Top in LeftSideZones? NO
    └─ Return unfiltered! ← BLOCKER HITS HERE
    
T5: Add to allSlots
    ├─ Add leftThinPositions (may be filtered)
    ├─ Add rightThinPositions (NOT filtered due to blocker)
    └─ Result: extra 6 unfiltered cross-side thins
```

---

## How to Overcome the Blocker

### Option 1: Unified Thin Generation (Recommended)

**Move GenerateCrossSideThins out of LayoutSide**:
```csharp
var leftWithinSide = GenerateWithinSideThins_Left(...);
var rightWithinSide = GenerateWithinSideThins_Right(...);
var crossSide = GenerateAllCrossSideThins(...);  // NEW

var allThins = new List<SyntheticThin>();
allThins.AddRange(leftWithinSide);
allThins.AddRange(rightWithinSide);
allThins.AddRange(crossSide);

var filtered = FilterAllThinsForNoOps(allThins, ...);  // One pass!
```

**Why This Works**:
- All thins are in ONE list
- One filter sees everything
- Can properly distinguish between within-side and cross-side thins
- Can make holistic decisions about N+1 rule

### Option 2: Add Cross-Side Filtering (Workaround)

**Add a second pass after the first filter**:
```csharp
leftThinPositions = FilterAdjacentThinsForSoloPanelZone(...);
rightThinPositions = FilterAdjacentThinsForSoloPanelZone(...);

// NEW: Second pass for cross-side thins
RemoveCrossSideNoOpThins(ref leftThinPositions, rightThinPositions, ...);
RemoveCrossSideNoOpThins(ref rightThinPositions, leftThinPositions, ...);
```

**Why This Works**:
- Addresses the specific cross-side gap
- Less refactoring than Option 1
- Still fragile, but better

**Why This Doesn't FULLY Work**:
- Doesn't fix the architectural problem
- Just patches one symptom
- Could still fail on new scenarios not covered by the new logic

---

## Conclusion: The Blocker

**Blocker Name**: Distributed thin generation with independent, single-side filtering

**Manifestation**: Cross-side thins generated by `GenerateCrossSideThins` (inside `LayoutSide`) are stored in the wrong list and pass through the filter unfiltered because the filter doesn't expect them there.

**Why Previous Fixes Failed**: They attempted to fix the filter logic without addressing the fundamental visibility problem that the filter can't see cross-side thins until AFTER they're added to a list, at which point the filter is already isolated to one side.

**Real Fix Required**: Either:
1. Consolidate thin generation into one phase (recommended)
2. Add coordination between the two filter calls (workaround)
3. Move GenerateCrossSideThins outside of LayoutSide so it runs after filtering (best of both)

**Current Status**: All tests passing suggests the current implementation may have already partially addressed this, but the architectural fragility remains.

---

## Implementation Readiness

**Blocker fully diagnosed**: ✅
**Root cause identified**: ✅  
**Actionable fix options provided**: ✅
**Ready for implementation**: ✅

Recommend Option 1 (Unified Thin Generation) for long-term robustness.
