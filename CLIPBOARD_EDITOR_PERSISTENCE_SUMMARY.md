# Clipboard Editor State Persistence — Executive Summary

**Status:** Architectural Planning Complete  
**Total Deliverables:** 3 documents  
**Next Phase:** Implementation (ready to assign)

---

## What's Being Solved

**Problem:** Clipboard image editor being open blocks app restart/rebuild signals until user closes the editor.

**Solution:** Persist editor state to disk async, restore on startup → restart no longer blocked.

**Impact:** Users can restart/rebuild immediately; zero state loss; complete restoration on app restart.

---

## Key Architectural Decisions

| Decision | Recommendation | Rationale |
|----------|---|---|
| **Where to persist?** | Temporary JSON files in `%LOCALAPPDATA%\SquadDash` | Aligns with `RestartCoordinatorStateStore` pattern; simple cleanup; no DB schema changes |
| **File format?** | JSON (System.Text.Json) | Human-readable; existing `ClipboardAnnotationState` already JSON; extensible versioning |
| **When to save?** | Async on editor close + async on restart pending | Non-blocking; brief 500ms wait during graceful shutdown only |
| **When to restore?** | On app startup, before main window shown | Happens in background; no user wait |
| **Undo/redo stack?** | Not serialized (v1 limitation) | Complex; most users finish edits before restarting; future enhancement |
| **Multiple editors?** | Fully supported (unique ID per editor) | Each gets separate state file; no collisions |
| **Error handling?** | Log & continue (graceful degradation) | User gets blank editor if restore fails; no crash; all errors traced |

---

## What Gets Persisted

✅ **Image bitmap/data** (via source PNG sidecar file)  
✅ **All annotations** (text, arrows, rectangles, X marks, measure lines)  
✅ **Window position/size/state** (maximized/normal)  
✅ **Tool selection & zoom level**  
✅ **Crop selection** (pending and applied)  
✅ **Cursor overlay state**  
✅ **Attached prompt identity** (draft vs specific queue position)  

❌ **Undo/redo stack** (known v1 limitation; future enhancement)  

---

## How It Works

### Restart Scenario (Current vs. Proposed)

**BEFORE (Current)**
```
[User edits in clipboard editor]
   ↓
[Build finishes, restart requested]
   ↓
[Restart deferred: "waiting for image editor"]
   ↓
[User manually closes editor]
   ↓
[Restart proceeds]
   ↓
[App restarts, editor state lost]
```

**AFTER (Proposed)**
```
[User edits in clipboard editor]
   ↓
[Build finishes, restart requested]
   ↓
[Async save signal fired (non-blocking)]
   ↓
[Restart proceeds immediately ← KEY CHANGE]
   ↓
[App restarts, editor auto-restored]
   ↓
[User sees editor exactly as left it]
```

### File Structure

```
%LOCALAPPDATA%\SquadDash\
├── clipboard-editor-{uuid}-pending.json    (being written)
├── clipboard-editor-{uuid}-active.json     (ready to restore)
├── clipboard-editor-{uuid}-source.png      (original image)
├── restart-{hash}.json
└── settings.json
```

---

## Lifecycle & Coordination

### Before: Restart blocked if any editor open
```csharp
// RestartDeferralPolicy.cs
if (isClipboardEditorOpen)
    return RestartDeferralReason.ClipboardEditor;  // ← BLOCKS
```

### After: Async save, restart proceeds
```csharp
// RestartDeferralPolicy.cs (updated)
// ← ClipboardEditor check removed; no block

// MainWindow.xaml.cs (updated)
if (restartPending) {
    _ = SaveAllClipboardEditorsAsync(reason);  // Fire-and-forget
    return false;  // ← DOESN'T BLOCK
}
```

---

## Implementation Phases

| Phase | Component | Owner | Effort | Dependencies |
|-------|-----------|-------|--------|--------------|
| **1** | Core persistence (save/load/cleanup) | Backend | 2-3 hrs | None |
| **2** | Editor state capture/restore | UI/Editor | 3-4 hrs | Phase 1 |
| **3** | Restart coordination | Core/MainWindow | 3-4 hrs | Phase 1-2 |
| **4** | Startup restoration | Launcher | 2-3 hrs | Phase 1-2 |
| **5** | Prompt attachment tracking | UI/Workflow | 2 hrs | Phase 2-3 |
| **6** | Testing & integration | QA | 2-3 hrs | All phases |
| | **TOTAL** | **Cross-team** | **14-17 hrs** | **Sequential** |

---

## Files to Create/Modify

### New Files
- `SquadDash/ClipboardEditorSessionState.cs` (DTO)
- `SquadDash/ClipboardEditorStateStore.cs` (persistence layer)
- `SquadDash.Tests/ClipboardEditorStateStoreTests.cs` (unit tests)
- `SquadDash.Tests/ClipboardImageEditorWindowTests.cs` (integration tests)

### Modified Files
- `SquadDash/ClipboardImageEditorWindow.cs` (capture/restore methods)
- `SquadDash/MainWindow.xaml.cs` (async save coordination)
- `SquadDash/RestartDeferralPolicy.cs` (remove clipboard check)
- `SquadDashLauncher/Program.cs` (startup restoration)

---

## Success Criteria

**Functional**
- ✅ Clipboard editor open does NOT block restart
- ✅ Restart proceeds immediately (visible in UI)
- ✅ Editor restored with 100% state preservation
- ✅ Multiple editors supported
- ✅ Zero silent failures (all errors logged)

**Non-Functional**
- ✅ Persistence overhead <100ms (async)
- ✅ Startup restore overhead <500ms
- ✅ State file size <1MB per editor
- ✅ No performance regression

---

## Risk Profile

### High Risks (Mitigated)
1. **Async save race condition** (user closes app before save completes)
   - Mitigation: Graceful shutdown sequencing + brief timeout wait

2. **Image file path invalidation** (workspace moved, file deleted)
   - Mitigation: Copy image to temp location OR inline base64 OR fallback to blank editor

### Medium Risks (Mitigated)
3. **Undo/redo stack lost** (most users complete edits before restart)
   - Mitigation: Document as v1 limitation; future enhancement

4. **Stale file accumulation** (user never restarts)
   - Mitigation: Auto-cleanup >7 days old on startup

### Low Risks (Mitigated)
5. **Schema version mismatch** (app upgraded)
   - Mitigation: Version field + migration logic in loader

6. **Window off-screen** (multi-monitor scenarios)
   - Mitigation: Validate/clamp restored geometry to screen bounds

---

## Testing Strategy

### Unit Tests
- Persistence layer: save/load/cleanup, serialization, error cases
- State capture/restore round-trip
- Window geometry validation

### Integration Tests
- Full cycle: open → edit → restart → restore
- Multiple editors simultaneously
- Error scenarios (missing image, corrupted file, permissions)

### Manual Tests
- Normal restart while editor open
- App close + reopen
- Force exit (task kill) recovery
- Stale file cleanup
- Performance benchmarking

### Regression Tests
- All existing restart deferral reasons still work
- Non-clipboard restarts unaffected
- Startup time baseline ±10ms

---

## How to Use These Documents

### For Architecture Review
👉 **Read:** `CLIPBOARD_EDITOR_PERSISTENCE_ARCHITECTURE.md`
- Decisions, rationale, schema, risk assessment
- Good for stakeholder alignment
- ~15 min read

### For Implementation
👉 **Read:** `CLIPBOARD_EDITOR_PERSISTENCE_IMPLEMENTATION_CHECKLIST.md`
- Phase-by-phase task breakdown
- Per-file changes, test cases
- Good for assigning work to developers
- ~30 min to plan; 14+ hours to execute

### For Status Updates
👉 **Read:** This document (SUMMARY)
- Quick overview, decisions, timeline
- Good for standups and progress tracking
- ~5 min read

---

## Quick Reference: Class Responsibilities

| Class | Responsibility | Location |
|-------|-----------------|----------|
| **ClipboardEditorSessionState** | DTO for serializable state | `SquadDash/ClipboardEditorSessionState.cs` (new) |
| **ClipboardEditorStateStore** | Save/load/cleanup persistence | `SquadDash/ClipboardEditorStateStore.cs` (new) |
| **ClipboardImageEditorWindow** | Capture/restore UI state | `SquadDash/ClipboardImageEditorWindow.cs` (modify) |
| **MainWindow** | Coordinate async saves on restart | `SquadDash/MainWindow.xaml.cs` (modify) |
| **RestartDeferralPolicy** | Determine if restart should defer | `SquadDash/RestartDeferralPolicy.cs` (modify) |
| **SquadDashLauncher.Program** | Restore editors on startup | `SquadDashLauncher/Program.cs` (modify) |

---

## Next Steps

1. **Review** this architecture (stakeholder + tech lead sign-off)
2. **Assign** Phase 1 (persistence layer) to backend developer
3. **Implement** phases sequentially (1 → 2 → 3 → 4 → 5 → 6)
4. **Test** each phase (unit + integration + manual)
5. **Merge** via PR with code review
6. **Deploy** to SquadDash build

---

## Questions & Answers

**Q: Will the editor block restarts?**  
A: No. Restart proceeds immediately; save happens async in background.

**Q: What if the app crashes before save completes?**  
A: The state file won't be created (no `.active` file), and the editor starts blank on reopen. Acceptable trade-off for non-blocking restart.

**Q: Can I undo changes after restart?**  
A: Undo stack is not persisted in v1. Annotations are preserved, but undo history is reset. Future enhancement possible.

**Q: What if I move my workspace folder?**  
A: Image source path will be invalid; editor opens blank. Mitigation: Copy image to temp location or inline as base64.

**Q: How much disk space does it use?**  
A: Typical state file 50-200KB (JSON is small). Image source PNG varies. Auto-cleanup removes >7 days old. Negligible footprint.

**Q: Can I have multiple editors open?**  
A: Yes, fully supported. Each gets unique ID and separate state file. All restored on reopen.

**Q: Does this affect non-clipboard workflows?**  
A: No. Other restart deferral reasons (prompt running, loop, voice, doc revision) are unchanged.

---

## Glossary (Quick Ref)

- **State**: Image + annotations + window geometry + tool selection
- **Persistence**: Async save to disk (non-blocking)
- **Restoration**: Load state from disk on app startup
- **Async**: Fire-and-forget (doesn't block UI or restart)
- **Sidecar**: Companion file (e.g., `.json` + `.png`)
- **Stale**: Persisted state older than retention window (7 days)
- **Graceful degradation**: Open blank editor if restore fails (no crash)

---

**Document Version:** 1.0  
**Last Updated:** January 2025  
**Status:** Ready for implementation  
**Estimated Timeline:** 3.5-4 weeks (cross-team, sequential phases)

