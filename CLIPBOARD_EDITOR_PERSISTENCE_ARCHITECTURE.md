# Clipboard Image Editor State Persistence Architecture

**Date:** January 2025  
**Status:** Architectural Planning (Pre-implementation)  
**Scope:** Enable non-blocking editor state persistence across app restarts/rebuilds

---

## Executive Summary

The clipboard image editor currently **blocks app restarts/rebuilds** while open. This design proposes **asynchronous persistence** of editor state (image, annotations, undo stack, window position) so restarts proceed immediately without data loss. On app startup, the editor window is restored exactly as it was, including its attachment to the active draft or queue position.

**Key outcome:** Restart signals are no longer deferred by open clipboard editors; complete state restoration is guaranteed.

---

## Current State Analysis

### Blocking Mechanism
- **Location:** `MainWindow.xaml.cs`, `RestartDeferralPolicy.cs`
- **Flag:** `_clipboardEditorOpen` (boolean; set when editor opens, cleared when it closes)
- **Effect:** When true, `DeferPendingRestartIfBlocked("reason")` returns true, deferring restart
- **User message:** "Build finished. Restart will happen after the image editor closes."

### Deferral Policy Logic
```csharp
// RestartDeferralPolicy.cs
if (isClipboardEditorOpen)
    return RestartDeferralReason.ClipboardEditor;  // Blocks restart
```

### Current Editor Lifecycle
1. **Open:** `OpenEditorModeless(ClipboardImageEditorWindow)` → sets `_clipboardEditorOpen = true`
2. **Operate:** User crops, annotates, may re-open with prior state
3. **Close:** Editor window closes → sets `_clipboardEditorOpen = false`
4. **Complete restart:** Only after editor fully closes

### What's Already Captured (Reusable)
- **`ClipboardAnnotationState`** (v2): Fully serializable snapshot of editor state
  - Crop selection (pending and applied)
  - All annotations (arrows, rectangles, text labels, measure lines, X marks)
  - Cursor overlay state
  - Version field for future compatibility
- **Sidecar files:** Editor already saves `.annotation.json` alongside `.source.png` for draft attachments
- **`RestartCoordinatorStateStore`**: Proven pattern for persisting restart-related state to JSON files in `%LOCALAPPDATA%\SquadDash`
- **`ApplicationSettingsStore`**: Pattern for cross-session persistence with mutex-protected access

### What's Missing
- **Window geometry** (position, size, maximized/normal state)
- **Undo/redo stack** (currently not serializable; lost on restart)
- **Prompt attachment identity** (which draft/queue item the image is attached to)
- **Async save hook** (graceful shutdown vs. pending restart)
- **Restore logic** (loading persisted state into a new editor window)
- **Non-blocking coordination** (save without blocking restart)

---

## Architectural Decision Framework

### 1. Persistence Backend (Recommended: Hybrid JSON + Fallback)

**Option A: Temporary Workspace File (Recommended)**
- **Location:** `{LOCALAPPDATA}\SquadDash\clipboard-editor-{sessionId}.json`
- **Pros:**
  - Aligns with existing `RestartCoordinatorStateStore` pattern
  - Per-session isolation (multiple restarts don't collide)
  - Simple cleanup on successful restore
  - No database schema changes
  - Fast file I/O, stateless (no locks needed)
- **Cons:**
  - File system I/O under time pressure (restart pending)
  - Cleanup race conditions possible (unlikely)
- **Recommendation:** ✅ **Use this**

**Option B: In-Memory Cache (Session ID keyed)**
- **Pros:**
  - Fastest persistence (no I/O)
  - Atomic save
- **Cons:**
  - Lost if process crash before normal shutdown
  - Shared environment risk (test isolation)
- **Recommendation:** ❌ Not suitable; restart often involves process replacement

**Option C: Database/Store**
- **Pros:**
  - Structured queries, transactions
- **Cons:**
  - Overkill for single blob; not used elsewhere in SquadDash
  - Adds dependency
- **Recommendation:** ❌ Over-engineered

**Decision:** **Temporary JSON file** in `SquadDashPaths.AppData`, keyed by workspace folder hash + editor window ID.

---

### 2. Serialization Format (Recommended: JSON)

**Option A: JSON (Recommended)**
- **Pros:**
  - Human-readable debugging
  - Aligns with `ClipboardAnnotationState` already JSON
  - No serialization library additions needed (`System.Text.Json` already used)
  - Easy incremental versioning
- **Cons:**
  - Slightly larger file size (image bitmap not included; see below)
- **Recommendation:** ✅ **Use this**

**Option B: Binary**
- **Pros:**
  - Smaller file size
- **Cons:**
  - Harder to debug, version, extend
- **Recommendation:** ❌ Not needed

**Format:** See **Serialization Schema** section below.

---

### 3. Lifecycle Hooks (Recommended: Async Save + Restore on Init)

**When to Save**
- **Trigger 1 (Graceful):** Editor Closing → save on `Closing` event
  - Async `Task`, fire-and-forget (non-blocking)
  - Save to `{stateDir}/clipboard-editor-{editorId}-pending.json`
- **Trigger 2 (Emergency):** Restart pending → save on demand
  - Called from `DeferPendingRestartIfBlocked()` path
  - Async task, but **await briefly** to maximize chance of write
  - Rename `.pending` → `.active` on success

**When to Restore**
- **On app startup (before main window shown):**
  - Check for `clipboard-editor-*.active.json` files in state dir
  - For each: deserialize → create new editor window
  - Load `InitialState` parameter
  - No blocking (restore happens in background, editor shown on top)

**Cleanup**
- **On successful restore:** Delete `.active` file
- **On failed restore:** Keep file (user can retry) or log cleanup task
- **On startup if file older than 7 days:** Delete (stale session)

---

### 4. Coordination with Restart Logic (Recommended: Async Interlock)

**Current Flow**
```
RestartPending → DeferPendingRestartIfBlocked() → 
  if (ClipboardEditorOpen) return true (deferred)
```

**New Flow**
```
RestartPending → DeferPendingRestartIfBlocked() → 
  if (ClipboardEditorOpen) {
    // Fire save signal (async, non-blocking)
    FireClipboardEditorSaveSignal(reason: "restart-pending");
    // Allow restart to proceed *immediately*
    return false;  // Not deferred!
  }
```

**Implementation Details**
- Add method: `Task SaveAllClipboardEditorsAsync(string reason)`
  - Iterate all open `ClipboardImageEditorWindow` instances
  - Call `window.SaveStateAsync()` on each
  - Fire-and-forget (don't await in UI thread)
- Update `RestartDeferralPolicy.GetDeferralReason()`:
  - Remove `isClipboardEditorOpen` parameter from deferral check
  - Editor closing/saving no longer blocks restart

---

### 5. Error Handling & Graceful Degradation (Recommended: Log + Continue)

**Persistence Failures**
- **Disk I/O error:** Log warning, continue (editor still open, no state loss)
- **Serialization error:** Log error, skip (not critical)
- **File locked:** Retry with backoff (1ms, 5ms, 10ms), then skip

**Restoration Failures**
- **File not found:** Skip (first run, or cleanup already happened)
- **Deserialization error:** Log error + file path, discard file, open blank editor
- **Image file missing:** Log warning, open blank editor (user starts fresh)
- **Annotation version mismatch:** Attempt migration, fallback to blank editor

**No silent failures:** All errors logged to trace system (SquadDashTrace.Write).

---

## Serialization Schema

### Root: `ClipboardEditorSessionState.json`

```json
{
  "version": 1,
  "editorId": "clipboard-editor-{guid}",
  "sessionId": "{guid}",
  "workspaceFolder": "C:\\path\\to\\workspace",
  "windowGeometry": {
    "x": 100,
    "y": 200,
    "width": 800,
    "height": 600,
    "isMaximized": false
  },
  "attachedPrompt": {
    "type": "draft",
    "queueIndex": null
  },
  "toolState": {
    "selectedTool": "arrow",
    "zoomLevel": 1.0
  },
  "annotationState": {
    "version": 2,
    "canvasScaleX": 1.0,
    "canvasScaleY": 1.0,
    "hasCrop": true,
    "cropX": 10.5,
    "cropY": 20.5,
    "cropW": 500.0,
    "cropH": 300.0,
    "hasAppliedCrop": false,
    "appliedCropX": 0.0,
    "appliedCropY": 0.0,
    "appliedCropW": 0.0,
    "appliedCropH": 0.0,
    "cursorEnabled": false,
    "cursorX": 0.0,
    "cursorY": 0.0,
    "arrows": [
      {
        "targetName": "method-name",
        "targetX": 50.0,
        "targetY": 60.0,
        "targetW": 100.0,
        "targetH": 20.0,
        "angleDeg": 45.0,
        "arrowLength": 80.0,
        "tailLength": 120.0,
        "userTailLength": 120.0,
        "color": "#FF7814",
        "centerX": 100.0,
        "centerY": 70.0,
        "offsetX": 50.0,
        "offsetY": 10.0
      }
    ],
    "rects": [],
    "texts": [],
    "measureLines": [],
    "xs": []
  },
  "imageMetadata": {
    "sourceImagePath": "C:\\temp\\clipboard-source-{guid}.png",
    "originalImageHash": "sha256:abcd1234...",
    "originalImageWidth": 1920,
    "originalImageHeight": 1080
  },
  "savedAt": "2025-01-15T10:30:45.123Z",
  "appVersion": "1.0.0.1234"
}
```

### Attached Prompt Reference

```json
"attachedPrompt": {
  "type": "draft",           // "draft" | "queued"
  "draftId": "active-draft", // If type=draft
  "queueIndex": null,        // If type=queued, the index in _promptQueue
  "promptText": "..."        // Snapshot of prompt text for context
}
```

### File Naming Convention

- **Pending save:** `clipboard-editor-{editorId}-pending.json`
- **Active (after restart detected):** `clipboard-editor-{editorId}-active.json`
- **Cleaned up:** Deleted after successful restore or 7+ days old

---

## Implementation Plan

### Phase 1: Core Persistence Layer (2-3 hours)

**Owner:** Backend/Infrastructure Developer

**Deliverables:**
1. `ClipboardEditorSessionState` class (DTO for root schema)
2. `ClipboardEditorStateStore` class (save/load/delete)
   - `SaveAsync(state, reason)` → file I/O with backoff retry
   - `LoadAsync(editorId)` → deserialize
   - `GetPendingEditorsAsync()` → find all `.active` files
3. Unit tests (file I/O, serialization, error cases)

**Files to create:**
- `SquadDash/ClipboardEditorSessionState.cs`
- `SquadDash/ClipboardEditorStateStore.cs`
- `SquadDash.Tests/ClipboardEditorStateStoreTests.cs`

---

### Phase 2: Editor UI Capture & Restoration (3-4 hours)

**Owner:** UI/Editor Developer

**Deliverables:**
1. Add to `ClipboardImageEditorWindow.cs`:
   - `CaptureWindowGeometry()` → returns geometry snapshot
   - `CaptureToolState()` → current tool, zoom
   - `CaptureSessionState()` → full snapshot for persistence
   - `RestoreFromSessionStateAsync(state)` → load geometry, annotations, image
   - `SaveStateAsync()` → async save (called on closing or restart pending)

2. Update constructor/initialization:
   - Accept optional `ClipboardEditorSessionState` parameter
   - Apply `RestoreFromSessionStateAsync()` if provided
   - Handle missing image file gracefully (blank editor)

3. Add test cases for state capture/restore round-trip

**Files to modify:**
- `SquadDash/ClipboardImageEditorWindow.cs`
- `SquadDash.Tests/ClipboardImageEditorWindowTests.cs` (new/updated)

---

### Phase 3: Lifecycle Coordination (3-4 hours)

**Owner:** Core/MainWindow Developer

**Deliverables:**
1. Update `RestartDeferralPolicy.GetDeferralReason()`:
   - Remove `isClipboardEditorOpen` parameter
   - Don't defer on clipboard editor

2. Add to `MainWindow.xaml.cs`:
   - `SaveAllClipboardEditorsAsync(reason)` → calls `SaveStateAsync()` on each window
   - Inject `ClipboardEditorStateStore` into MainWindow
   - Update deferral check calls to not include clipboard flag

3. Update `DeferPendingRestartIfBlocked()`:
   - Before returning false (not deferred), fire `SaveAllClipboardEditorsAsync()`
   - Log: "Clipboard editors saved async; restart proceeding"

**Files to modify:**
- `SquadDash/RestartDeferralPolicy.cs`
- `SquadDash/MainWindow.xaml.cs`
- `SquadDash/SquadDashLauncher/Program.cs` (startup restore call)

---

### Phase 4: Startup Restoration (2-3 hours)

**Owner:** Launcher/Initialization Developer

**Deliverables:**
1. Update `SquadDashLauncher/Program.cs`:
   - On app startup, before showing main window, check for `.active` files
   - For each: deserialize → create editor window
   - Handle failures gracefully (log, continue)

2. Add helper: `RestoreClipboardEditorsFromDiskAsync(stateStore)`
   - Iterate `{stateDir}/clipboard-editor-*.active.json`
   - Parse each
   - Create + restore editor window
   - On success: delete `.active` file
   - On failure: keep file (retry next launch) or log cleanup task

3. Test: Verify editor appears on startup (visual test)

**Files to modify:**
- `SquadDashLauncher/Program.cs`
- `SquadDash/MainWindow.xaml.cs` (add call to restore)

---

### Phase 5: Prompt Attachment Tracking (2 hours)

**Owner:** UI/Workflow Developer

**Deliverables:**
1. Add to `MainWindow.xaml.cs`:
   - When creating clipboard editor (prompt mode), capture:
     - Current draft (active tab)
     - Or queue index if in queue
   - Pass to editor constructor as `AttachedPromptInfo`

2. Update `ClipboardImageEditorWindow`:
   - Store `AttachedPromptInfo` field
   - Include in `CaptureSessionState()`

3. On restore:
   - Attempt to locate same draft/queue position
   - If queue item gone: default to draft
   - Update editor UI to show attachment

**Files to modify:**
- `SquadDash/ClipboardImageEditorWindow.cs`
- `SquadDash/MainWindow.xaml.cs`

---

### Phase 6: Testing & Integration (2-3 hours)

**Owner:** QA + Integration Developer

**Deliverables:**
1. **Manual tests:**
   - Open editor → restart build → verify restart proceeds immediately
   - Editor should be visible post-restart, fully restored
   - Restart again while editor still visible → verify no duplicate windows
   - Close app with editor open → verify restore on reopen

2. **Edge cases:**
   - Edit image → restart → verify edits present
   - Crash simulation (kill process) → reopen → verify restoration attempt
   - File permissions issue (read-only state dir) → verify graceful failure
   - Stale files (>7 days old) → verify cleanup

3. **Regression:**
   - Verify all existing restart deferral reasons still work (prompt, loop, voice, doc revision)
   - Verify non-clipboard restarts are unaffected
   - Performance: no perceptible lag on startup

---

## Risk Assessment

### High Risk

**1. Async Save Race Condition**
- **Scenario:** User closes editor, app force-exits before async save completes
- **Impact:** State lost; user reopens to blank editor
- **Mitigation:**
  - Implement graceful shutdown sequencing: wait briefly for pending saves before exit
  - Use `Application.Shutdown()` only after save signal has been fired
  - In `MainWindow.Closing`, call `SaveAllClipboardEditorsAsync()` and await with timeout (500ms)

**2. Image File Path Invalidation**
- **Scenario:** User moves workspace or deletes image file between sessions
- **Impact:** Editor restored but image missing
- **Mitigation:**
  - Store image as base64 blob in state JSON (increases file size ~33%)
  - OR: Copy image to temp location on first capture, reference that
  - Fallback: Open blank editor if source image missing (acceptable)

**3. Multiple Editor Instances (Same State)**
- **Scenario:** Two editors open with same image; both save state; unclear which wins
- **Impact:** Ambiguous restoration
- **Mitigation:**
  - Assign unique `editorId` (GUID) to each instance
  - File naming: `clipboard-editor-{editorId}-pending.json` (per-editor isolation)
  - No collision

---

### Medium Risk

**4. Undo/Redo Stack Not Serialized**
- **Scenario:** User edits, doesn't complete action, restarts
- **Impact:** Undo stack lost (but annotations preserved)
- **Mitigation:**
  - Document as known limitation (v1 behavior)
  - Future enhancement: capture undo/redo stack if needed
  - Most users complete edits before restarting

**5. Stale File Cleanup**
- **Scenario:** User never restarts app; `.pending` files accumulate
- **Impact:** Disk space (minimal; JSON is small)
- **Mitigation:**
  - Auto-delete `.pending` files >24h old on app startup
  - Document cleanup policy in trace log

**6. Concurrent Process Access**
- **Scenario:** Multiple SquadDash instances restart simultaneously
- **Impact:** File contention, lost writes
- **Mitigation:**
  - Use workspace-folder hash in filename (one set of files per workspace)
  - Add timestamp/nonce to detect stale files
  - If conflict detected, skip restoration for that editor

---

### Low Risk

**7. Serialization Version Mismatch**
- **Scenario:** App upgraded; new version tries to load old state schema
- **Impact:** Deserialization fails
- **Mitigation:**
  - Add `version: 1` field to schema
  - Migration logic: `LoadAsync()` checks version, applies transformations
  - Fallback: Skip restore if version unsupported

**8. Window Geometry Off-Screen**
- **Scenario:** Restored window positioned outside current monitor bounds
- **Impact:** Window invisible
- **Mitigation:**
  - Add validation: clamp restored position to primary monitor bounds
  - If off-screen, center on primary monitor

---

## Scope Decisions

### Question 1: Full App Restarts vs. Build-Triggered Restarts?
**Answer:** Both. Persist on any shutdown, restore on any startup (via flag in state JSON).
- `isRebuildRestart: true` → record this for future triggering of restart analytics

### Question 2: Multiple Simultaneous Editors?
**Answer:** Yes, support fully.
- Each gets unique `editorId` (GUID)
- Each saved to separate file
- On restore, all are recreated as modeless windows

### Question 3: Stale Cleanup Policy?
**Answer:** Auto-delete files >7 days old on app startup.
- Rationale: Unlikely user will restart after >7 days without needing fresh state
- Configurable via constant in `ClipboardEditorStateStore`

### Question 4: Image Blob Inline or External?
**Answer:** Store as separate PNG file (external reference).
- Rationale: Keeps state JSON human-readable, avoids base64 bloat
- Naming: `clipboard-editor-{editorId}-source.png` (alongside state JSON)
- Fallback: If missing, open blank editor

---

## Ownership & Responsibilities

| Component | Owner | Effort | Weeks |
|-----------|-------|--------|-------|
| **Core Persistence Layer** | Backend | 2-3 hrs | 0.5 |
| **Editor State Capture/Restore** | UI/Editor | 3-4 hrs | 0.75 |
| **Lifecycle Coordination** | Core/MainWindow | 3-4 hrs | 0.75 |
| **Startup Restoration** | Launcher | 2-3 hrs | 0.5 |
| **Prompt Attachment Tracking** | UI/Workflow | 2 hrs | 0.5 |
| **Testing & Integration** | QA + Integration | 2-3 hrs | 0.75 |
| **TOTAL** | Cross-team | **14-17 hrs** | **3.5-4** |

---

## Success Criteria

### Functional
- ✅ Clipboard editor open does NOT block restart/rebuild (user visible: restart proceeds immediately)
- ✅ On restart, editor window is restored with all annotations intact
- ✅ Window position, size, zoom, selected tool are preserved
- ✅ Attached prompt identity (draft vs. queue) is preserved
- ✅ Error handling: no silent failures; all issues logged to trace

### Non-Functional
- ✅ Persistence overhead <100ms (async, non-blocking)
- ✅ Startup restore overhead <500ms (parallel to main window init)
- ✅ State file size <1MB per editor (typical: 50-200KB)
- ✅ No performance regression on app startup

### Testing
- ✅ Unit tests for `ClipboardEditorStateStore` (100% coverage)
- ✅ Integration tests for state capture/restore round-trip
- ✅ Manual tests for all error scenarios
- ✅ Regression tests: existing deferral reasons still work

---

## Future Enhancements (Out of Scope v1)

1. **Undo/Redo Stack Serialization**
   - Capture action history (`List<EditorAction>`)
   - Serialize to state JSON
   - Restore on reopen

2. **Multi-Image Batch Editor**
   - Support multiple images in single editor session
   - Persist all annotations simultaneously

3. **Cloud Sync (if workspace shared)**
   - Sync state JSON to workspace `.squad/` directory
   - Automatic sharing across machines

4. **State History/Audit**
   - Log all state transitions
   - Allow rollback to prior annotation versions

---

## Recommended Reading

- `RestartCoordinatorStateStore.cs` — Proven pattern for restart state persistence
- `ApplicationSettingsStore.cs` — Mutex-protected JSON storage pattern
- `ClipboardAnnotationState.cs` — Existing serialization schema (reusable)
- `MainWindow.xaml.cs` (`DeferPendingRestartIfBlocked`, `TryCompletePendingRestart`) — Restart coordination
- `SquadDashLauncher/Program.cs` — Startup initialization point

---

## Glossary

| Term | Definition |
|------|-----------|
| **State** | Complete editor condition: image, annotations, window geometry, undo stack |
| **Persistence** | Saving state to disk for restoration across app restarts |
| **Async Save** | Non-blocking state write (fire-and-forget, or await briefly) |
| **Deferral** | Delaying restart until all blockers (prompts, loops, editors) are done |
| **Restoration** | Loading persisted state into a new editor on app startup |
| **Sidecar File** | Companion file alongside main data (e.g., `.json` alongside `.png`) |
| **Stale** | Persisted state older than retention window (default: 7 days) |
| **Attachment** | Association between editor and active draft or queue item |

