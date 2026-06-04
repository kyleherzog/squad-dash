# Docking Thin Filter: Executive Summary for Lyra

**Prepared for**: Implementation handoff  
**Analysis Date**: Current Session  
**Status**: ✅ All tests passing (51 docking, 2225 total)  
**Blocker Type**: ARCHITECTURAL (not logical)

---

## TL;DR: What's the Problem?

The docking thin filter works, but the **architecture is fragile**. The system has:

1. **Two independent thin generators** (within-side + cross-side)
2. **Per-side filtering** (can't see cross-side interactions)
3. **Post-hoc N+1 validation** (finds violations but doesn't prevent them)

This causes a **5-6 attempt failure cycle** because fixes to one code path don't see problems in the other.

---

## Visual: How Thins Are Generated Today

```
┌─ LayoutSide(Left) ─────────────────────┐
│ BuildSideSequence()                     │
│ ├─ Analyze: Left, Left2, Left4 occupied │
│ └─ Generate: 5 within-side thins        │
└─────────────────────────────────────────┘
                    ↓
         [leftThinPositions list]
                    ↓
      ┌─ FilterAdjacentThins ─┐
      │ (sees left thins only)  │
      └────────────────────────┘
                    ↓
         [filtered leftThinPositions]


┌─ LayoutSide(Right) ────────────────────┐
│ Right has NO occupied zones             │
│ ↓                                        │
│ BuildSideSequence()                     │
│ └─ Generate: 0 within-side thins        │
│                                          │
│ GenerateCrossSideThins()  ←────── EXTRA! │
│ └─ Generate: 4 cross-side thins to Left │
└─────────────────────────────────────────┘
                    ↓
         [rightThinPositions = 4 cross-side thins to LEFT zones]
                    ↓
      ┌─ FilterAdjacentThins ─────────┐
      │ Tries to filter rightThins     │
      │ But sourceZone=Top            │
      │ sideZones=LeftSideZones       │
      │ sourceZoneIdx = -1 ❌         │
      │ → Returns unfiltered!         │
      └───────────────────────────────┘
                    ↓
         [rightThinPositions = 4 unfiltered cross-side thins]


┌─ AddSyntheticThinSlots ─────┐
│ Add leftThinPositions       │
│ Add rightThinPositions  ←─── PROBLEM!
│                              │
│ Total thins to LEFT zones:   │
│ • 5 within-side (filtered)   │
│ • 4 cross-side (unfiltered)  │
│ = 9 total                    │
│ Expected: 5 (N+1 for 4 zones)│
│ EXTRA: 4 (80% excess!)       │
└─────────────────────────────┘
```

---

## The Root Cause: Three Critical Gaps

### Gap #1: Cross-Side Thins Bypass the Filter

**What Happens**:
- GenerateCrossSideThins adds thins to `rightThinPositions`
- These thins target LEFT zones
- Filter called on `rightThinPositions` with `sideZones=LeftSideZones`
- But source is on TOP, not LEFT
- Filter detects sourceZoneIdx = -1 and returns unfiltered!

**Why It Fails**:
- Filter assumes source is on the same side being evaluated
- Doesn't account for cross-side thins generated on OTHER side

**Code Location**:
```csharp
// Line 826-831 in FilterAdjacentThinsForSoloPanelZone
if (sourceZoneIdx < 0)
{
    // source zone not in sideZones, return unfiltered
    return new List<SyntheticThin>(thins);  // ← PROBLEM: Includes cross-side thins!
}
```

### Gap #2: Thin Generation Scattered Across Two Functions

**Within-Side Thins**: `BuildSideSequence()` (lines 402-575)
- Complex logic for analyzing tier gaps
- Decides which synthetic thins are needed

**Cross-Side Thins**: `GenerateCrossSideThins()` (lines 354-400)
- Separate decision logic
- Runs AFTER BuildSideSequence
- Doesn't know about already-generated thins

**Problem**: No unified view of "what thins should be here?"

### Gap #3: N+1 Rule Is Descriptive, Not Prescriptive

**When It Runs**: `FindAdjacentThinViolations()` (line 618)
- Runs AFTER all thins added to allSlots
- Finds violations
- **Logs them as warnings only**
- No correction applied

**Problem**: Violations found too late to prevent them

---

## Why Fixes Keep Failing

### The Pattern (Attempts 1-6)

Each previous fix modified the filter logic, but:

| Attempt | Approach | Result |
|---------|----------|--------|
| 1-2 | Filter by thin count before filtering | Cross-side thins still bypass |
| 3-4 | Add more aggressive filter logic | Removes correct thins, keeps wrong ones |
| 5-6 | Apply N+1 rule inside filter | Can't see cross-side thins in same list |

### Why They ALL Failed

The filter cannot fix what it cannot see.

- `leftThinPositions` list doesn't include cross-side thins from Right
- `rightThinPositions` list includes cross-side thins targeting Left, but filter doesn't know how to evaluate them
- No single list contains all thins at filtering time

---

## Recommended Fix Architecture

### OPTION A: Unified Thin Collection (BEST)

**Principle**: Collect ALL thins into ONE list BEFORE filtering

```csharp
// Step 1: Generate all thins without filtering
var allThins = new List<SyntheticThin>();

// Within-side thins
allThins.AddRange(GenerateWithinSideThins_Left(...));
allThins.AddRange(GenerateWithinSideThins_Right(...));

// Cross-side thins (extracted from LayoutSide)
allThins.AddRange(GenerateAllCrossSideThins(...));

// Step 2: Filter ALL thins at once with complete visibility
var filtered = FilterAllThinsForNoOps(allThins, currentLayout, sourcePanelId);

// Step 3: Validate and fix N+1 rule
var fixed = EnforceNPlusOneRule(filtered);

// Step 4: Add to slots
AddSyntheticThinSlots(allSlots, fixed);
```

**Advantages**:
- ✅ Complete visibility into all thin types
- ✅ Single filter pass
- ✅ Clear source of truth for "what should be here"
- ✅ Preventive N+1 enforcement

**Challenges**:
- Requires extracting GenerateCrossSideThins out of LayoutSide
- More refactoring needed

### OPTION B: Multi-Pass Cross-Side Filtering

**Principle**: Filter cross-side thins in a second pass

```csharp
var leftThins = LayoutSide(leftStates, ...);
var rightThins = LayoutSide(rightStates, ...);

// First pass: filter within-side thins
leftThins = FilterAdjacentThinsForSoloPanelZone(leftThins, ...);
rightThins = FilterAdjacentThinsForSoloPanelZone(rightThins, ...);

// NEW Second pass: filter cross-side no-ops
// If source alone in Left2, remove Right→Left2 cross-side thins that are no-ops
leftThins = FilterCrossSideThinsForSoloPanel(leftThins, rightThins, ...);
rightThins = FilterCrossSideThinsForSoloPanel(rightThins, leftThins, ...);

AddSyntheticThinSlots(allSlots, leftThins);
AddSyntheticThinSlots(allSlots, rightThins);
```

**Advantages**:
- ✅ Minimal changes to existing code
- ✅ Incremental fix

**Challenges**:
- ✗ Still fragile (two separate lists)
- ✗ Cross-side thins still partially "invisible" to main filter
- ✗ Could fail again on new scenarios

---

## Implementation Notes for Lyra

### If Proceeding with OPTION B (safer near-term fix):

1. **Add FilterCrossSideThinsForSoloPanel() function** that:
   - Takes both leftThins AND rightThins as parameters
   - Checks if source is alone in a zone
   - Removes cross-side thins from the OTHER side that target adjacent zones
   - Returns modified thin lists

2. **Key Logic**:
   ```csharp
   // If source is alone in Left2:
   // - Remove any thins in rightThins that target Left2
   // - Remove any thins in rightThins that target Left or Left3 (adjacent)
   // 
   // If source is alone in Right:
   // - Remove any thins in leftThins that target Right
   // - Remove any thins in leftThins that target Right2 (adjacent)
   ```

3. **Test Scenarios**:
   - Source alone in Left2, Right empty → remove Right→Left2 cross-side thin
   - Source alone in Right, Left empty → remove Left→Right cross-side thin
   - Source alone in Right2, Top occupied → remove Top→Right2 cross-side thin
   - Multiple cross-side thins, N+1 rule still maintained after filtering

### If Proceeding with OPTION A (better long-term):

1. **Extract GenerateCrossSideThins from LayoutSide**
   - Move to separate function that's called AFTER LayoutSide returns
   - Takes output from both LayoutSide calls
   - Returns complete cross-side thin list

2. **Consolidate thin generation**
   - Call LayoutSide for both sides
   - Generate cross-side thins
   - Merge all into single list

3. **Single-pass filtering**
   - Filter all thins together
   - Much simpler logic

---

## Files to Watch

- **DockingMapBuilder.cs** (1079 lines)
  - `BuildDockingMapFromSideStates()` (lines 78-220) - orchestrates everything
  - `LayoutSide()` (lines 295-347) - generates within-side + cross-side thins
  - `BuildSideSequence()` (lines 402-575) - complex within-side logic
  - `GenerateCrossSideThins()` (lines 354-400) - needs to move
  - `FilterAdjacentThinsForSoloPanelZone()` (lines 806-913) - current filter

- **DockingMapBuilderTests.cs**
  - 51 tests covering all scenarios
  - All currently passing ✅

---

## Verification

Before declaring fix complete, verify:

1. ✅ All 51 docking tests pass
2. ✅ All 2225 total tests pass
3. ✅ No N+1 violations in trace logs
4. ✅ No adjacent thin violations
5. ✅ Cross-side thins still generated when appropriate
6. ✅ Solo-panel no-op thins removed when appropriate

---

## Next Steps

1. **Confirm this analysis** - Does it match what you observed?
2. **Choose approach** - Option A (unified) or Option B (multi-pass)?
3. **Implement** - Follow the pattern outlined above
4. **Test** - Run full test suite + manual docking map testing
5. **Verify** - Check trace logs for any remaining violations

---

## Key Insight

The current code works (tests passing), but the architecture is **inherently accident-prone** because it distributes thin generation and filtering logic across multiple code paths that don't have complete visibility into each other.

A real, lasting fix consolidates these into a unified flow with single-pass filtering and preventive (not post-hoc) N+1 validation.
