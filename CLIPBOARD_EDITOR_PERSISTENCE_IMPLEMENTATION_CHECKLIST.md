# Clipboard Editor State Persistence — Implementation Checklist

**Created:** January 2025  
**Document:** Phase-by-phase implementation checklist  
**Status:** Pre-implementation

---

## Phase 1: Core Persistence Layer

### Create `ClipboardEditorSessionState.cs`
- [ ] Define DTO class for root state schema
- [ ] Include fields:
  - `Version` (int, = 1)
  - `EditorId` (GUID string)
  - `SessionId` (GUID string)
  - `WorkspaceFolder` (string)
  - `WindowGeometry` (nested object)
  - `AttachedPrompt` (nested object)
  - `ToolState` (nested object)
  - `AnnotationState` (reuse `ClipboardAnnotationState`)
  - `ImageMetadata` (nested object)
  - `SavedAt` (DateTime string, ISO 8601)
  - `AppVersion` (string)
- [ ] Add JSON serialization attributes (`[JsonPropertyName(...)]`)
- [ ] Add validation method: `Validate()` → throws on invalid state
- [ ] Add nested classes:
  - `WindowGeometryState` (x, y, width, height, isMaximized)
  - `AttachedPromptInfo` (type: "draft"/"queued", draftId, queueIndex, promptText)
  - `ToolState` (selectedTool, zoomLevel)
  - `ImageMetadata` (sourceImagePath, originalImageHash, width, height)

### Create `ClipboardEditorStateStore.cs`
- [ ] Inherit from or align with `JsonFileStorage` pattern (existing utility)
- [ ] Constructor: accepts `stateDirectory` (defaults to `SquadDashPaths.AppData`)
- [ ] Add method: `GetStateFilePath(editorId, isPending)` → returns file path
- [ ] Add method: `async Task SaveAsync(state, reason)`
  - Serialize state to JSON
  - Write to `clipboard-editor-{editorId}-pending.json`
  - On success, rename to `.active`
  - Add retry logic: backoff (1ms, 5ms, 10ms) on file locked
  - Log trace: `SquadDashTrace.Write("ClipboardPersist", $"Saved editor {editorId}: {reason}")`
  - Catch `IOException` → log warning, return gracefully
- [ ] Add method: `async Task<ClipboardEditorSessionState?> LoadAsync(editorId)`
  - Check for `.active` file (priority)
  - Fall back to `.pending` file
  - Deserialize JSON
  - Call `state.Validate()`
  - Return null if file missing or invalid
  - Log trace on error
- [ ] Add method: `async Task<List<(string editorId, ClipboardEditorSessionState state)>> GetAllPendingAsync()`
  - Glob for all `clipboard-editor-*-active.json` files
  - Load each
  - Return list of (editorId, state) tuples
  - Skip invalid files with logging
- [ ] Add method: `async Task DeleteAsync(editorId, isPending)`
  - Delete `.pending` or `.active` file
  - Silently ignore if missing
  - Log trace on deletion
- [ ] Add method: `async Task CleanupStaleFilesAsync(maxAgeHours = 168)` (7 days)
  - Find all `clipboard-editor-*.pending.json` files
  - If older than maxAgeHours → delete
  - Log trace: `"Cleaned up {count} stale editor states"`

### Unit Tests: `ClipboardEditorStateStoreTests.cs`
- [ ] Test: Save and load round-trip
  - Create state with all fields filled
  - Save to temp dir
  - Load → verify all fields match
- [ ] Test: Serialization with null/empty fields
  - Test minimal state (only required fields)
  - Test with all annotations empty
- [ ] Test: File not found → returns null (don't throw)
- [ ] Test: Invalid JSON → returns null, logs error
- [ ] Test: File locked → retry backoff → eventually succeeds
- [ ] Test: Stale file cleanup
  - Create files with old timestamps
  - Call cleanup → verify deleted
- [ ] Test: Multiple editors (different editorIds) → no collision
- [ ] Test: `.pending` → `.active` rename on save success

---

## Phase 2: Editor UI Capture & Restoration

### Update `ClipboardImageEditorWindow.cs`

#### Add State Capture Methods
- [ ] Method: `ClipboardEditorSessionState CaptureSessionState()`
  - Returns complete snapshot of current editor state
  - Called before save
  - Includes:
    - Current window geometry (via `this.Left`, `this.Top`, `this.Width`, `this.Height`, `this.WindowState == WindowState.Maximized`)
    - Tool selection (current selected tool)
    - Zoom level (from canvas transform)
    - Current `AnnotationState` (already captured by `CaptureAnnotationState()`)
    - Attached prompt info (if known)
    - Source image path (stored on editor)
    - Image metadata (width, height, hash if computed)
  - Create `ClipboardEditorSessionState` instance and return

- [ ] Method: `(double x, double y, double w, double h, bool isMaximized) CaptureWindowGeometry()`
  - Return current window position and size
  - Handle DPI considerations

- [ ] Method: `(string tool, double zoom) CaptureToolState()`
  - Return current selected tool name (from UI)
  - Return canvas zoom level

#### Add State Restoration Methods
- [ ] Method: `async Task RestoreFromSessionStateAsync(ClipboardEditorSessionState state)`
  - Call after window created but before shown
  - Restore window geometry:
    - Set `this.Left`, `this.Top`, `this.Width`, `this.Height`
    - Set `this.WindowState` if `isMaximized`
    - Validate: clamp to screen bounds (no off-screen windows)
  - Restore image:
    - Load source image from `state.ImageMetadata.SourceImagePath`
    - Verify hash matches (early corruption detection)
    - If missing/corrupted → log error, open blank editor
  - Restore annotations:
    - Call `RestoreAnnotationState(state.AnnotationState)` (existing code)
  - Restore tool state:
    - Set selected tool from `state.ToolState.SelectedTool`
    - Set zoom level

- [ ] Method: `void RestoreWindowGeometry(double x, double y, double w, double h, bool isMaximized)`
  - Set window position/size
  - Validate bounds (not off-screen)
  - Handle negative/zero dimensions

#### Add Async Save Method
- [ ] Method: `async Task SaveStateAsync(string reason)`
  - Called on `Closing` event (async, non-blocking)
  - Called when restart pending (async, fire-and-forget)
  - Calls `CaptureSessionState()` to get current state
  - Calls `_stateStore.SaveAsync(state, reason)`
  - Log trace: `"Editor {EditorId} saved: {reason}"`
  - Catch exceptions → log only (don't throw to UI thread)

#### Update Constructor
- [ ] Add optional parameter: `ClipboardEditorSessionState? initialState`
- [ ] Add optional parameter: `bool isUpdateMode` (true if re-opening for editing, not first open)
- [ ] If `initialState` provided:
  - Store in field (for later save)
  - Call `RestoreFromSessionStateAsync(initialState)` on loaded
  - Show restored state UI

#### Update `Closing` Event Handler
- [ ] Add async save call (fire-and-forget):
  ```csharp
  this.Closing += async (s, e) => {
      _ = SaveStateAsync("editor-closing");  // Fire and forget
  };
  ```

#### Update `ImageAccepted` Event Handler
- [ ] Ensure editor still fires `ImageAccepted` event on "Insert Image" click
- [ ] No changes needed (existing behavior preserved)

### Unit/Integration Tests: `ClipboardImageEditorWindowTests.cs`
- [ ] Test: Capture → Restore round-trip
  - Open editor, set geometry, create annotations
  - Capture state
  - Create new editor with `initialState`
  - Verify all state matches
- [ ] Test: Window geometry validation
  - Restore with off-screen coordinates → should clamp to screen
- [ ] Test: Missing image on restore → opens blank editor (no crash)
- [ ] Test: Corrupted image (hash mismatch) → opens blank editor
- [ ] Test: Annotations survive capture/restore
- [ ] Test: Async save (verify callback fires, no UI freeze)

---

## Phase 3: Lifecycle Coordination

### Update `RestartDeferralPolicy.cs`
- [ ] Update `GetDeferralReason()` method signature:
  - **Remove parameter:** `bool isClipboardEditorOpen`
  - No longer check clipboard editor status
- [ ] Update logic: delete the line:
  ```csharp
  if (isClipboardEditorOpen)
      return RestartDeferralReason.ClipboardEditor;
  ```
- [ ] Update `BuildStatusMessage()`: remove the `ClipboardEditor` case
- [ ] Update `RestartDeferralReason` enum: remove `ClipboardEditor` value (or keep for logging, but don't return)

### Update `MainWindow.xaml.cs`

#### Dependency Injection
- [ ] Add field: `private readonly ClipboardEditorStateStore _clipboardEditorStateStore;`
- [ ] In constructor, inject from service provider:
  ```csharp
  _clipboardEditorStateStore = serviceProvider?.GetRequiredService<ClipboardEditorStateStore>() 
      ?? new ClipboardEditorStateStore();
  ```

#### Add Async Save Method
- [ ] Add method: `private async Task SaveAllClipboardEditorsAsync(string reason)`
  - Iterate all open `ClipboardImageEditorWindow` instances (from `Application.Current.Windows`)
  - For each: call `window.SaveStateAsync(reason)` without awaiting (fire-and-forget)
  - Log trace: `"SaveAllClipboardEditorsAsync: reason={reason}"`

#### Update Deferral Check
- [ ] Update calls to `RestartDeferralPolicy.GetDeferralReason()`:
  - Remove argument: `isClipboardEditorOpen: _clipboardEditorOpen`
  - Example: change
    ```csharp
    RestartDeferralPolicy.GetDeferralReason(
        _isPromptRunning, IsLoopRunning, _backgroundTaskPresenter.HasRestartBlockingBackgroundWork(),
        HasPendingDirectQuickReplyAgentFollowUp(), _pttState != PttState.Idle || _pttDraining,
        MarkdownDocumentWindow.AnyRevisionInFlight, _clipboardEditorOpen)
    ```
    to
    ```csharp
    RestartDeferralPolicy.GetDeferralReason(
        _isPromptRunning, IsLoopRunning, _backgroundTaskPresenter.HasRestartBlockingBackgroundWork(),
        HasPendingDirectQuickReplyAgentFollowUp(), _pttState != PttState.Idle || _pttDraining,
        MarkdownDocumentWindow.AnyRevisionInFlight)
    ```

#### Update `DeferPendingRestartIfBlocked()` Method
- [ ] Before returning false (when no blockers), add:
  ```csharp
  _ = SaveAllClipboardEditorsAsync(reason);  // Fire-and-forget async save
  ```
- [ ] Example refactored code:
  ```csharp
  private bool DeferPendingRestartIfBlocked(string reason) {
      var deferralReason = RestartDeferralPolicy.GetDeferralReason(
          _isPromptRunning, IsLoopRunning, ...);
      
      if (deferralReason != RestartDeferralReason.None) {
          SquadDashTrace.Write("Shutdown", 
              $"Deferred: {RestartDeferralPolicy.BuildStatusMessage(deferralReason)}");
          return true;  // Deferred
      }
      
      // Save all clipboard editors async (non-blocking)
      _ = SaveAllClipboardEditorsAsync(reason);
      return false;  // Not deferred; proceed with restart
  }
  ```

#### Update `TryCompletePendingRestart()` Method
- [ ] Before `Application.Current.Shutdown()`, add brief wait for async saves:
  ```csharp
  if (TryCompletePendingRestart(reason, emergencySaveBeforeClose: true)) {
      // Wait briefly for clipboard editor saves to complete
      var saveTask = SaveAllClipboardEditorsAsync(reason);
      // Don't await indefinitely; give 500ms max
      if (!saveTask.IsCompleted) {
          _ = saveTask.ConfigureAwait(false);  // Let it finish in background
      }
      // Proceed with shutdown
      Application.Current.Shutdown();
  }
  ```

#### Update Trace Logging
- [ ] Existing trace line that logs all deferral blockers should be updated:
  - Remove `clipboard={_clipboardEditorOpen}` from the output
  - Example: change
    ```csharp
    $"Restart deferred reason={reason} blocker={blocker} ... clipboard={_clipboardEditorOpen}"
    ```
    to
    ```csharp
    $"Restart deferred reason={reason} blocker={blocker} ..."
    ```

#### Update `OpenEditorModeless()` Method
- [ ] Keep existing: `_clipboardEditorOpen = true` (for logging/diagnostics)
- [ ] Keep existing: subscription to editor closing event
- [ ] Can optionally pass `AttachedPromptInfo` to editor constructor

### No Changes to Restart Request Watcher
- [ ] The `_restartRequestWatcher` and related logic remain unchanged
- [ ] Restart request files still saved/loaded via `RestartCoordinatorStateStore`
- [ ] Only the deferral policy changes

---

## Phase 4: Startup Restoration

### Update `SquadDashLauncher/Program.cs`
- [ ] In `Main()` method, after app initialized but before `MainWindow.Show()`:
  - Add call: `await RestoreClipboardEditorsFromDiskAsync(stateStore);`
  - Location: after app is constructed but before window shown to user

- [ ] Add static method: `async Task RestoreClipboardEditorsFromDiskAsync(ClipboardEditorStateStore stateStore)`
  - Call `stateStore.GetAllPendingAsync()` → get list of (editorId, state) tuples
  - For each tuple:
    - Try to deserialize image from `state.ImageMetadata.SourceImagePath`
    - Create new `ClipboardImageEditorWindow` with `initialState: state`
    - Call `window.Show()` (modeless)
    - On success: call `stateStore.DeleteAsync(editorId, isPending: false)` to remove `.active` file
    - On error: log error, keep file (retry on next launch), continue
  - Log trace: `"Restored {count} clipboard editors from disk"`

- [ ] Error handling:
  - File not found → skip (already cleaned)
  - Deserialization fails → log, skip
  - Image missing → log, still create blank editor window
  - No `.active` files → silent (normal case)

### Update `MainWindow.xaml.cs` (if restoration not in launcher)
- [ ] Alternative: if restoration should happen in main window:
  - Add to `MainWindow` constructor after UI initialized:
    ```csharp
    _ = RestoreClipboardEditorsAsync();  // Fire-and-forget
    ```
  - Add method: `private async Task RestoreClipboardEditorsAsync()`
    - Similar logic to launcher version
    - Call `_clipboardEditorStateStore.GetAllPendingAsync()`
    - Create + restore editors

### Manual Testing
- [ ] Verify: Editor appears on startup (visually)
- [ ] Verify: Trace logs show "Restored {count} clipboard editors"
- [ ] Verify: `.active` files are deleted after successful restore

---

## Phase 5: Prompt Attachment Tracking

### Update `ClipboardImageEditorWindow.cs`
- [ ] Add field: `private AttachedPromptInfo? _attachedPrompt;`
- [ ] Add to constructor parameter: `AttachedPromptInfo? attachedPrompt = null`
- [ ] In constructor: store `_attachedPrompt = attachedPrompt;`
- [ ] Update `CaptureSessionState()`:
  - Include `_attachedPrompt` in returned state
  - If null, set `type: "draft"` (default)

### Update `MainWindow.xaml.cs`

#### When Creating Clipboard Editor (Prompt Mode)
- [ ] Find the code that creates `ClipboardImageEditorWindow` in prompt context:
  ```csharp
  var editor = new ClipboardImageEditorWindow(this, bitmap, isPromptMode: true);
  ```
- [ ] Capture current draft/queue info:
  ```csharp
  var attachedPrompt = new AttachedPromptInfo {
      Type = "draft",
      DraftId = "active-draft",  // Or extract from current active tab
      QueueIndex = null,
      PromptText = PromptTextBox.Text
  };
  ```
- [ ] Pass to constructor:
  ```csharp
  var editor = new ClipboardImageEditorWindow(this, bitmap, 
      isPromptMode: true, attachedPrompt: attachedPrompt);
  ```

#### When Creating Clipboard Editor (Doc Mode)
- [ ] Similar logic for doc-attached editors (if applicable)

### Update Editor UI (Optional for v1)
- [ ] Display attachment info in editor title bar or status
  - e.g., "Image Editor — attached to Draft [0 queued]"
  - Or skip for v1 (attach info is internal, doesn't affect functionality)

### Verification
- [ ] Manual: Create editor from prompt → close app → reopen → verify attachment info preserved in state file
- [ ] Unit test: Capture → Restore → verify attachment matches

---

## Phase 6: Testing & Integration

### Unit Tests (Already Covered Above)
- [x] `ClipboardEditorStateStoreTests` (Phase 1)
- [x] `ClipboardImageEditorWindowTests` (Phase 2)
- [x] `RestartDeferralPolicyTests` (Phase 3, update existing)

### Integration Tests
- [ ] Test: Full cycle — open editor → restart triggered → verify restart proceeds → verify restore
- [ ] Test: Multiple editors — two open → restart → both restored
- [ ] Test: Editor with annotations — create complex annotation → restart → verify intact
- [ ] Test: Zoom + tool state — set zoom level + select tool → restart → verify restored

### Manual Tests
- [ ] **Scenario 1: Normal restart while editor open**
  - Open app
  - Open clipboard editor, make edits
  - Trigger build (rebuild)
  - Verify: restart proceeds immediately (no "waiting for editor" message)
  - Verify: editor restored with edits intact

- [ ] **Scenario 2: App close with editor open**
  - Open app
  - Open clipboard editor
  - Close app (Ctrl+Q or X button)
  - Verify: app closes gracefully
  - Reopen app
  - Verify: editor restored

- [ ] **Scenario 3: Force exit**
  - Open editor
  - Kill process (Task Manager)
  - Reopen app
  - Verify: restoration attempted (or gracefully skipped if no `.active` file)

- [ ] **Scenario 4: Multiple editors**
  - Open 3 clipboard editors simultaneously
  - Edit each differently
  - Restart
  - Verify: all 3 restored with correct state

- [ ] **Scenario 5: Error case — missing image**
  - Create state file with invalid image path
  - Restart
  - Verify: editor opens blank (no crash), error logged

- [ ] **Scenario 6: Stale file cleanup**
  - Manually create old `.pending` file (timestamp >7 days)
  - Restart app
  - Verify: file deleted, trace logged

- [ ] **Scenario 7: File permissions**
  - Make state dir read-only
  - Open editor, trigger restart
  - Verify: graceful degradation (no crash, error logged)
  - Restore write permissions
  - Verify: next restart saves successfully

### Regression Tests
- [ ] All existing restart deferral reasons still work:
  - [ ] Prompt running → deferred
  - [ ] Loop running → deferred
  - [ ] Voice input → deferred
  - [ ] Doc revision → deferred
  - [ ] Background work → deferred
- [ ] Non-clipboard restarts unaffected (same behavior as before)
- [ ] Performance: startup time unchanged (±10ms tolerance)

### Performance Tests
- [ ] Measure: Time to save clipboard editor state
  - Expected: <50ms (async, non-blocking)
- [ ] Measure: Time to restore all clipboard editors on startup
  - Expected: <500ms (parallel, background task)
- [ ] Measure: App startup time overhead
  - Expected: <100ms additional (acceptable)

---

## Checklist Metadata

**Total Tasks:** 80+  
**Estimated Effort:** 14-17 hours  
**Risk Level:** Medium (async race conditions, file I/O)  
**Rollback Plan:** If issues arise, revert to blocking clipboard editor (existing behavior)

---

## Notes for Implementers

1. **Logging:** Use `SquadDashTrace.Write("ClipboardPersist", message)` for all clipboard-related trace output
2. **Exceptions:** Catch and log, don't propagate to UI thread
3. **Async:** Use `async Task` or `async void`, but prefer `async Task` and fire-and-forget with `_ = method()`
4. **File I/O:** Always check for `FileNotFoundException`, `IOException`, `UnauthorizedAccessException`
5. **JSON:** Use `System.Text.Json` (already in codebase), add `[JsonPropertyName(...)]` attributes
6. **Testing:** Write unit tests first (TDD), then integration tests
7. **Documentation:** Update code comments and inline docs as you implement
8. **Git Commits:** One commit per phase, descriptive message with Co-authored-by trailer

