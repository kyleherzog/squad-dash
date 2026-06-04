# Deep Trace Logging - BuildSideSequence Zone Iteration

## ✅ Logging Added Successfully

All requested trace logging has been successfully added to:
- **File**: `SquadDash/PanelDocking/DockingMapBuilder.cs`
- **Methods**: `LayoutSide()` and `BuildSideSequence()`
- **Trace Tag**: `[build-side-seq]`
- **Build Status**: ✅ Passes (0 errors, 2221/2221 tests pass)

## 1. Available Zones at Start

```
[build-side-seq] Available zones on {side}: [{zone1}, {zone2}, ...]
  Format: ZoneName(Tier=X, Occ=T/F, Supp=T/F, Panels=N)
  
[build-side-seq] Visible (non-suppressed) zones on {side}: [...] (count=N)
  Format: ZoneName(Tier=X, Occ=T/F)
```

**Shows:**
- ALL zones on this side before filtering
- Occupied status (Occ=T/F)
- Suppressed status (Supp=T/F) 
- Panel count for each zone
- Only visible zones (those not suppressed)

## 2. Zone Iteration Details

```
[build-side-seq] Zone iteration: checking zone {i} of {totalZones}
[build-side-seq]   Zone: {zoneName} (Tier={tier}, Occupied={T/F}, Panels={count})
[build-side-seq]   Included in loop? YES (reason) | NO (reason)
```

**Shows:**
- Each zone being visited during iteration
- Zone position (index i of total)
- Zone tier, occupation status, panel count
- Inclusion status with reason

## 3. Tier Range Logic

```
[build-side-seq] Tier range calculation:
[build-side-seq]   First occupied tier: {min} ({zoneName})
[build-side-seq]   Last occupied tier: {max} ({zoneName})
[build-side-seq]   Range for iteration: [tiers {min} to {max}]
[build-side-seq]   Total zones on {side}: {N}, zones in visible/iterable: {M}
```

**Shows:**
- Which tier is the first occupied zone (Tier=min)
- Which tier is the last occupied zone (Tier=max)
- The range used for iteration
- Total zone count vs. visible zone count

## 4. Adjacency Decisions with Detail

```
[build-side-seq] Adjacency between {zone1}@Tier{T1} and {zone2}@Tier{T2}:
[build-side-seq]   tierDiff = {diff}, occupied: both={T/F}, next={T/F}
[build-side-seq]   Condition check: tierDiff==1? {T/F}, both occupied? {T/F}, next empty? {T/F}
[build-side-seq]   Final decision: INCLUDE thin (reason) | SKIP thin (reason: explanation)
```

**Shows:**
- Zone pair being checked
- Their tiers
- Tier difference (absolute distance)
- Occupancy of both zones and next zone
- All condition checks:
  - Is tierDiff==1? (adjacent)
  - Both occupied?
  - Next zone empty?
- Final decision with reason:
  - INCLUDE: both occupied + adjacent
  - INCLUDE: non-adjacent zones (bridge)
  - SKIP: both empty
  - SKIP: current empty
  - SKIP: next empty

## 5. Synthetic Injection Logic

```
[build-side-seq] Synthetic injection logic:
[build-side-seq]   sourceIsOnlySidePanel={T/F}
[build-side-seq]   needsInnerSynthetic={T/F} (no empty inner zones...)
[build-side-seq]   needsOuterSynthetic={T/F} (no empty outer zones...)
```

**Shows:**
- Whether source is the only panel on this side
- Whether inner synthetic is needed
- Whether outer synthetic is needed

## 6. Execution Summary

```
[build-side-seq] === Starting BuildSideSequence for {side} side ===
[build-side-seq] === BuildSideSequence complete for {side}: {N} total items ({M} synthetic) ===
[build-side-seq]   Synthetic thins: [InsertBefore Right@0, InsertAfter Right2@1, ...]
```

**Shows:**
- Clear entry/exit markers
- Total items generated (zones + synthetics)
- Count of synthetic items
- List of all synthetic thins with their target zone and order

## Questions Answered By This Logging

### Q: How many zones are on the Right/Left side?
**A:** View: `"Available zones on {side}"` - Count the zones listed
```
Available zones on Right: [Right(...), Right2(...), Right3(...), Right4(...), Right5(...)]
→ Answer: 5 zones
```

### Q: Which zones are in the "tier range for iteration"?
**A:** View: `"Total zones on {side}: {N}, zones in visible/iterable: {M}"`
The visible zones are iterated, not all zones.
```
Total zones on Right: 5, zones in visible/iterable: 3
→ Only 3 of 5 zones are visible and iterated
```

### Q: Why aren't Right3, Right4, Right5 visited?
**A:** Compare "Available zones" with "Visible zones"
- If zone has `Supp=T`: it's **suppressed**
- If zone has `Occ=F` and missing from visible: it's **not in iteration range**
```
Available: Right3(Supp=F), Right4(Supp=T), Right5(Supp=T)
→ Right4 and Right5 are suppressed
→ Right3 might be out of range or next zone causes it to skip
```

### Q: Is there logic that only iterates between first and last occupied zones?
**A:** Partially. View: `"Tier range calculation"` and `"zones in visible/iterable"`
- First occupied tier defines inner boundary
- Last occupied tier defines outer boundary
- Only visible (non-suppressed) zones in that range are iterated

### Q: Are cross-side thins generated separately?
**A:** View: `"Synthetic thins"` list
- Only shows synthetic items for this side
- No cross-side thins in BuildSideSequence
- Cross-side logic would be elsewhere

## Example Trace Output

```
[build-side-seq] === Starting BuildSideSequence for Right side ===
[build-side-seq] Available zones on Right: [Right(Tier=0,Occ=T,Supp=F,Panels=2), Right2(Tier=1,Occ=T,Supp=F,Panels=1), Right3(Tier=2,Occ=F,Supp=F,Panels=0), Right4(Tier=3,Occ=F,Supp=T,Panels=0), Right5(Tier=4,Occ=F,Supp=T,Panels=0)]
[build-side-seq] Visible (non-suppressed) zones on Right: [Right(Tier=0,Occ=T), Right2(Tier=1,Occ=T), Right3(Tier=2,Occ=F)] (count=3)
[build-side-seq] Occupied zones on Right: [Right(Tier=0,Panels=2), Right2(Tier=1,Panels=1)] (count=2)
[build-side-seq] Tier range calculation:
[build-side-seq]   First occupied tier: 0 (Right)
[build-side-seq]   Last occupied tier: 1 (Right2)
[build-side-seq]   Range for iteration: [tiers 0 to 1]
[build-side-seq]   Total zones on Right: 5, zones in visible/iterable: 3
[build-side-seq] Synthetic injection logic:
[build-side-seq]   sourceIsOnlySidePanel=false
[build-side-seq]   needsInnerSynthetic=true (no empty inner zones...)
[build-side-seq]   needsOuterSynthetic=true (no empty outer zones...)
[build-side-seq] Zone iteration: checking zone 0 of 3
[build-side-seq]   Zone: Right (Tier=0, Occupied=true, Panels=2)
[build-side-seq]   Included in loop? YES
[build-side-seq]   Adding regular zone item: Right
[build-side-seq] Adjacency between Right@Tier0 and Right2@Tier1:
[build-side-seq]   tierDiff = 1, occupied: both=true, next=true
[build-side-seq]   Condition check: tierDiff==1? true, both occupied? true, next empty? false
[build-side-seq]   Final decision: INCLUDE thin (both occupied + adjacent)
[build-side-seq]   Adding synthetic InsertBefore Right2@0
[build-side-seq] Zone iteration: checking zone 1 of 3
[build-side-seq]   Zone: Right2 (Tier=1, Occupied=true, Panels=1)
[build-side-seq]   Included in loop? YES
[build-side-seq]   Adding regular zone item: Right2
[build-side-seq] Adjacency between Right2@Tier1 and Right3@Tier2:
[build-side-seq]   tierDiff = 1, occupied: both=false, next=false
[build-side-seq]   Condition check: tierDiff==1? true, both occupied? false, next empty? true
[build-side-seq]   Final decision: SKIP thin (reason: next empty)
[build-side-seq] Zone iteration: checking zone 2 of 3
[build-side-seq]   Zone: Right3 (Tier=2, Occupied=false, Panels=0)
[build-side-seq]   Included in loop? YES
[build-side-seq]   Adding regular zone item: Right3
[build-side-seq] === BuildSideSequence complete for Right: 5 total items (3 synthetic) ===
[build-side-seq]   Synthetic thins: [InsertBefore Right2@0, InsertAfter Right2@1, InsertAfter Right3@0]
```

## How to Use This Logging

1. **Enable Docking tracing** in your trace/logging configuration
2. **Trigger the operation**: Right-click on a panel in the UI
3. **Examine trace output** for `[build-side-seq]` lines
4. **Use the patterns above** to answer your questions

## Files Modified

- `SquadDash/PanelDocking/DockingMapBuilder.cs`
  - Enhanced `LayoutSide()` method (lines 295-324)
  - Enhanced `BuildSideSequence()` method (lines 339-510)
  - Added 30+ detailed trace logging statements

## Build & Test Results

✅ **Build**: Succeeded (0 errors, 6 warnings - pre-existing)
✅ **Tests**: All 2221 tests pass
✅ **Compilation**: C# nullable reference types enabled
✅ **Trace Format**: Consistent with existing `[build-side-seq]` tag pattern

## Next Steps

1. **Rebuild** the application with these changes
2. **Run a trace** during docking operations
3. **Share the trace output** showing `[build-side-seq]` lines
4. **We can analyze** why Right3/Right4/Right5 and cross-side thins are missing
