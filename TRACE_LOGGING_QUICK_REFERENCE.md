# Quick Trace Logging Reference

## What Was Added
Added comprehensive **deep trace logging** to `DockingMapBuilder.cs` BuildSideSequence zone iteration algorithm.

**Location**: `SquadDash/PanelDocking/DockingMapBuilder.cs`
**Methods**: `LayoutSide()` and `BuildSideSequence()`
**Trace Tag**: `[build-side-seq]`

## Build Status
✅ Compiles successfully
✅ All 2221 tests pass
✅ Ready for testing

## Where to Look in Trace Output

| What You Want to Know | Trace Line to Look For | Info It Shows |
|---|---|---|
| How many zones exist? | `Available zones on {side}` | All zones (suppressed/occupied) |
| Which zones are skipped? | Compare "Available" vs "Visible" | Suppressed zones won't be in "Visible" |
| Why aren't Right3-5 visited? | `Visible...zones on Right:` | Only zones listed here are iterated |
| What's the tier range? | `Range for iteration: [tiers X to Y]` | First to last occupied tier |
| Why skip a thin? | `Final decision: SKIP thin` | Reason explanation |
| How many thins generated? | `total items ({M} synthetic)` | Count of synthetic items |

## Trace Examples to Find

### Example 1: Understanding Zone Filtering
```
Available zones on Right: [Right(Supp=F), Right2(Supp=F), Right3(Supp=F), Right4(Supp=T), Right5(Supp=T)]
↓
Visible (non-suppressed) zones on Right: [Right, Right2, Right3] (count=3)
↓
→ Right4 and Right5 are SUPPRESSED (won't be visited)
```

### Example 2: Understanding Adjacency
```
Adjacency between Right@Tier0 and Right2@Tier1:
  tierDiff = 1, occupied: both=true
  Final decision: INCLUDE thin (both occupied + adjacent)
  ✓ This thin IS created

Adjacency between Right2@Tier1 and Right3@Tier2:
  tierDiff = 1, occupied: both=false (Right2=T, Right3=F)
  Final decision: SKIP thin (reason: next empty)
  ✗ This thin is NOT created
```

### Example 3: Understanding Synthetic Count
```
[build-side-seq] === BuildSideSequence complete for Right: 5 total items (3 synthetic) ===
[build-side-seq]   Synthetic thins: [InsertBefore Right@0, InsertAfter Right@1, InsertBefore Right2@0]
↓
→ 5 items total = 2 zones + 3 synthetic thins
```

## Key Questions + Where to Find Answers

**Q: Why are only 3 thins generated between Right/Right2?**
A: Look at:
1. `Available zones on Right:` - are there really more zones?
2. `Visible zones on Right:` - are they suppressed?
3. `Tier range calculation:` - is iteration limited?
4. `Adjacency...Final decision: SKIP` - are other pairs skipped?

**Q: Are Right3, Right4, Right5 suppressed or just not visible?**
A: Look at:
1. `Available zones:` check `Supp=T/F` for each
2. `Visible zones:` if zone isn't here and `Supp=F`, it's not in range

**Q: Are cross-side thins generated?**
A: Look at:
1. `Synthetic thins:` list shows target zones
2. If you see Top or Left zones in this list = cross-side
3. If only Right zones = no cross-side thins yet

## How to Enable Tracing

1. Build the solution (includes logging)
2. Enable Docking trace category in your trace config
3. Right-click on a panel to trigger docking map
4. View trace output
5. Search for `[build-side-seq]`

## Debug Workflow

1. **Identify issue**: "Only 3 thins, missing Right3-5 and cross-side"
2. **Collect trace** with new logging
3. **Find in trace**:
   - `Available zones on Right:` → How many exist?
   - `Visible zones on Right:` → Which are skipped?
   - `Tier range:` → Is iteration limited?
   - `Final decision:` → Which thins are generated vs skipped?
4. **Determine root cause** from the trace
5. **Fix the algorithm**

## What To Expect In Output

Example for Right side with Right(occupied), Right2(occupied), Right3(empty):

```
[build-side-seq] === Starting BuildSideSequence for Right side ===
[build-side-seq] Available zones on Right: [Right(Tier=0,Occ=T,Supp=F,Panels=2), Right2(Tier=1,Occ=T,Supp=F,Panels=1), Right3(Tier=2,Occ=F,Supp=F,Panels=0)]
[build-side-seq] Visible (non-suppressed) zones on Right: [Right(Tier=0,Occ=T), Right2(Tier=1,Occ=T), Right3(Tier=2,Occ=F)] (count=3)
[build-side-seq] Occupied zones on Right: [Right(Tier=0,Panels=2), Right2(Tier=1,Panels=1)] (count=2)
[build-side-seq] Tier range calculation:
[build-side-seq]   First occupied tier: 0 (Right)
[build-side-seq]   Last occupied tier: 1 (Right2)
[build-side-seq]   Range for iteration: [tiers 0 to 1]
[build-side-seq]   Total zones on Right: 3, zones in visible/iterable: 3
...
[build-side-seq] === BuildSideSequence complete for Right: 5 total items (3 synthetic) ===
```

## Files

- **Code**: `SquadDash/PanelDocking/DockingMapBuilder.cs`
- **Docs**: `TRACE_LOGGING_ADDED.md` (detailed reference)
- **Quick Ref**: This file

---

**Next**: Build, test, collect trace output, and share results!
