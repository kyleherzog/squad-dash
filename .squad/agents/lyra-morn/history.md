# Lyra Morn — History & Learnings

## Core Context

**Project:** SquadUI — WPF dashboard for Squad CLI AI agent management  
**Stack:** C# / WPF / .NET 10, NUnit 4.4+, TypeScript SDK  
**Key paths:**
- `SquadDash/` — main application (WPF)
- `SquadDash.Tests/` — NUnit test suite
- `.squad/decisions.md` — architectural decision log

---

## Learnings

📌 Loop panel splitter layout fix (2026-04-27): Fixed three related issues with loop panel splitter and context menus.

**Issue 1 — Splitter moves loop panel left edge (BUG):** Original structure had LoopPanelBorder (col 3), LoopOutputSplitter (col 4), LoopOutputBorder (col 5) as siblings in outer grid with `ResizeBehavior="PreviousAndNext"`. Dragging splitter left would shrink loop controls panel. **Fix:** Wrapped all three elements in inner `LoopSectionGrid` (Grid.Column="3" in outer grid). Inner grid columns: col 0 (Auto, `LoopPanelInnerColDef`) = loop controls, col 1 (5px) = splitter, col 2 (280px) = output. Removed outer columns 4 and 5, decremented Grid.Column for TasksPanel (6→4), WatchPanel (7→5), ApprovalPanel (8→6), NotesPanel (9→7). Updated `SyncLoopOutputPane()` to freeze/unfreeze `LoopPanelInnerColDef` when output shown/hidden: when showing, `MaxWidth = ActualWidth` and `Width = GridLength(ActualWidth)` to lock loop controls width; when hiding, `MaxWidth = PositiveInfinity` and `Width = Auto` to restore. Splitter now only resizes output panel.

**Issue 2 — Hide Loop Output context menu:** Added `Separator` and "Hide Loop Output" `MenuItem` to `LoopOutputBorder.ContextMenu`. Added `LoopOutputHideMenuItem_Click` handler that sets `_loopOutputHasContent = false` and calls `SyncLoopOutputPane()`.

**Issue 3 — Show Loop Output context menu:** Added `Separator` and `x:Name="LoopPanelShowOutputMenuItem"` MenuItem "Show Loop Output" to `LoopPanelBorder.ContextMenu`. Added `LoopPanelShowOutputMenuItem_Click` handler that sets `_loopOutputHasContent = true` and calls `SyncLoopOutputPane()`. Updated `SyncLoopOutputPane()` to toggle menu item visibility: `Collapsed` when output visible, `Visible` when hidden.

**Gotcha:** The Popup (`_loopConfigFlyout`) is NOT a direct child of LoopPanelBorder — it lives in the outer grid between `LoopSectionGrid` and `TasksPanelBorder`. Popups use `PlacementTarget` binding, not Grid.Column.

**Files:** `MainWindow.xaml` (lines 647-662 outer columns, 755-936 inner grid structure, 771-781 loop context menu, 972-980 output context menu), `MainWindow.xaml.cs` (lines 3588-3616 `SyncLoopOutputPane`, new handlers after 3616). Commit: `9b9d756`. Build: 0 errors, 0 warnings. Tests: 1179/1180 passing (1 inconclusive pre-existing).

📌 Transcript link navigation fix (2026-04-26): Fixed transcript hyperlink click behavior to open secondary panels instead of replacing main transcript content.

**Root cause:** `TranscriptHyperlink_Click` (line 5775) called `OpenTranscriptThread`, which invoked `SelectTranscriptThread`. This method directly modified the main transcript's document/content, replacing the visible transcript instead of opening a separate panel.

**Files involved:** `MainWindow.xaml.cs` (line 4928-4943, `OpenTranscriptThread` method)

**Fix:** Modified `OpenTranscriptThread` to use `OpenSecondaryPanel` for agent threads (same code path used by agent card clicks). Coordinator transcript still switches main view correctly. Used `FindAgentCardForThread` helper to map thread → card before calling `OpenSecondaryPanel`.

**Outcome:** Clicking transcript links now opens new secondary panels. Main transcript remains unchanged. Build: 0 errors, 0 warnings. Tests: 724/726 passing (2 pre-existing failures). Commit: `6745aef`

📌 Team update (2026-04-26T14:35:05Z): Transcript link navigation pattern decision merged to decisions.md — decided by Lyra Morn

📌 Team update (2026-04-25T18-05): Cursor overlay named-element anchor highlight implemented in `ScreenshotOverlayWindow.cs` and `ScreenshotAnnotationSidecar.cs` — implemented by Lyra Morn

`CursorAnnotation` extended with `AnchorName`/`AnchorBounds` (nullable, backward-compatible). `FindNamedAnchorForPoint` hit-tests the visual tree for the smallest named element under the cursor. `PlaceCursorAtPoint` enables placement anywhere in the capture region. Marching-ants highlight follows named controls live during drag. Build: 0 errors, 0 warnings.

📌 Session (2026-04-21):Fixed `HireAgentWindow` black background on light theme. Root cause: `Background = Brushes.Transparent` was overwriting `SetResourceReference(BackgroundProperty, "AppSurface")`. Fix: deleted the `Brushes.Transparent` assignment. One line removed; both themes now correct.

📌 Team update (2026-04-18T17-38): Lyra Morn owns two delegated tasks from Orion Vale's audit — decided by Orion Vale

**Task 1 — Remove markdown rendering duplication from MainWindow:**  
`MainWindow.xaml.cs` still contains duplicates of 11 markdown rendering methods that are canonical in `MarkdownDocumentRenderer.cs`. Replace all MainWindow call sites with calls through the renderer instance; promote methods from `private` to `internal` on the renderer as needed; delete the duplicates from MainWindow. Out of scope: `SquadTeamRosterLoader.ParseMarkdownRow`, `MarkdownHtmlBuilder`, `MarkdownFlowDocumentBuilder`. Acceptance: None of the 11 methods exist in `MainWindow.xaml.cs`; 0 build errors; 379/379 tests passing.

**Task 2 — Wire IWorkspacePaths (UI files):**  
Replace all `WorkspacePaths.*` static calls in UI layer (`App.xaml.cs`, `MainWindow`, `AgentInfoWindow`, `WorkspaceIssuePresentation`) with constructor-injected `IWorkspacePaths`. `App.xaml.cs` creates a `WorkspacePathsProvider` and passes it to `MainWindow`. Bundle with DEL-3 and DEL-5 work (move `_isPromptRunning` ownership to PEC; reduce `TranscriptConversationManager` leaky setters).

📌 Documentation Mode feature (2026-04-26): Implemented toggleable documentation viewer in MainWindow with three-column layout (transcript | splitter | docs panel). 

Documentation panel contains: TreeView with hierarchical topics (Getting Started, Agents, Workspace, Settings) and WebBrowser for markdown rendering using existing `MarkdownHtmlBuilder.Build()` infrastructure. View menu item shows checkmark when active. Docs panel hidden during full-screen transcript mode. Stub "Add Document" button for future expansion. Build: clean, 0 errors. All XAML uses `DynamicResource` for theme compatibility. GridSplitter enables resizable layout.

📌 Transcript UI fixes (2026-04-26): Implemented four focused improvements to secondary transcript panels in MainWindow.

**Fix 1 — Title format:** Changed `BuildSecondaryTranscriptTitle` from "Agent — from X ago" / "Agent's transcript" to "Agent - X ago" / "Agent" (cleaner, less verbose). **Fix 3 — Countdown cancellation:** Added `CountdownCancelled` flag to `SecondaryTranscriptEntry`; wired `MouseMove`, `PreviewMouseDown`, `PreviewMouseWheel` on panel border to permanently cancel auto-close timer on user interaction. **Fix 4 — Card hover glow:** Implemented `AgentCardBorder_MouseEnter`/`MouseLeave` handlers; when hovering agent card with an open secondary transcript, apply pulsing `DropShadowEffect` (accent color, opacity 0.4→1.0, infinite loop) to `PanelBorder`; remove on leave. Commit: `eb3836b`. Build: 0 errors, 724/726 tests passing (2 pre-existing `TranscriptSelectionTests` failures unrelated to changes).

📌 Four UI fixes (2026-04-26): Implemented comprehensive UI improvements addressing coordinator card hover, empty transcript windows, voice activation logic, and documentation panel state preservation.

**Fix 1 — Coordinator card hover glow:** Extended `AgentCardBorder_MouseEnter`/`MouseLeave` to detect `agentCard.IsLeadAgent` and apply the same pulsing `DropShadowEffect` to `MainTranscriptBorder` (main transcript window) instead of secondary panels. Reused exact animation pattern from agent card hover (0.4→1.0 opacity, 600ms, accent color, infinite loop). **Fix 2 — Empty transcript windows:** Modified `TranscriptSelectionController.HandleShiftClick()` to no longer return early when `GetThread1(card)` is null. Added `CreateEmptyThread()` helper that creates a minimal `TranscriptThreadState` with `kind: Agent`, `title: "{Name} - just now"`, empty `Document`, and adds it to `card.Threads`. Window opens/closes normally with correct accent color. **Fix 3 — Voice always enabled, send-on-release logic:** Added `_voiceStartedWithSendEnabled` field to `PushToTalkController`. Removed `!_isPromptRunning()` guard from PTT activation (line 99-101). Captured `!_isPromptRunning()` at the moment PTT starts (line 99) and stored in `_voiceStartedWithSendEnabled`. Changed `StopPushToTalkAsync(send: true)` to `send: _voiceStartedWithSendEnabled` (line 151) so Ctrl-release only sends if Send button was enabled at PTT start. Voice input now always activates regardless of Send button state, but submission respects initial state. **Fix 4 — Documentation panel preservation:** Root cause: `RebuildTranscriptPanelsGrid()` (line 4472) called `TranscriptPanelsGrid.ColumnDefinitions.Clear()` which removed named `DocsSplitterColumn` and `DocsPanelColumn`, and `Children.Clear()` removed `DocsSplitter` and `DocsPanel` from the grid. Fix: Save `docsSplitterWidth` and `docsPanelWidth` before clearing (line 4475-4476). Use `Children.Cast<UIElement>().Where(c => c != DocsSplitter && c != DocsPanel)` to selectively remove only transcript panels (line 4479-4482). Re-add two docs columns at the end of `ColumnDefinitions` (line 4520-4522, 4532-4535). Re-position `DocsSplitter` and `DocsPanel` at correct column indices (line 4499-4502, 4541-4544). Documentation panel state now preserved across agent panel auto-show events. Commit: `0822ba2`. Build: 0 errors, 0 warnings.

📌 Six UI polish fixes (2026-04-27): Implemented comprehensive UI improvements addressing menu clarity, docs panel layout, theme support, and interaction feedback.

**Fix 1 — Menu rename:** Changed "View Documentation" menu item to "_Documentation" (cleaner, more concise). **Fix 2 — Docs panel full height:** Moved `DocsSplitter` and `DocsPanel` from `TranscriptPanelsGrid` (row 3 only) to root grid with `Grid.RowSpan="4"` (rows 1-4), spanning workspace issue panel, status panels, transcripts, AND prompt area. Added `ColumnDefinitions` to root grid; set `Grid.Column="0"` on all existing root-level children (TitlebarGrid, WorkspaceIssuePanelBorder, StatusPanelBorder, TranscriptPanelsGrid, PromptBorder). Removed `DocsSplitterColumn` and `DocsPanelColumn` from `TranscriptPanelsGrid`. Documentation panel now spans full window height from top to bottom (excluding titlebar). **Fix 3 — TreeView dark theme:** Added `ItemContainerStyle` to `DocTopicsTreeView` that sets `Foreground="{DynamicResource LabelText}"` on `TreeViewItem` elements. Fixed black text on dark background issue. **Fix 4 — Margin cleanup:** Removed `Margin="8,0,0,0"` from `DocsPanel`, changed to `Margin="0,0,0,0"`. Eliminated gap between topics panel and `GridSplitter`. **Fix 5 — Already-open window flash:** Modified `OpenSecondaryPanel` to detect duplicate window opens (line 4376-4381) and call `FlashGlowHighlight` on the existing `PanelBorder` using the agent's accent color. Users now get visual feedback when clicking a transcript button for an already-open panel (2-3 pulses, 200ms each, blur radius 0→24). **Fix 6 — Glow auto-fade timer:** Added `_glowFadeTimer` field (`DispatcherTimer?`). Modified `AgentCardBorder_MouseEnter` to start a 3-second timer that stops the glow animation automatically. Timer resets on each mouse enter, stops on mouse leave. Glow no longer pulses indefinitely — fades after 3 seconds of hover. Commit: `5fd250b`. Build: 0 errors (SquadDash.App.dll compiled successfully; launcher exe copy failed due to running process, expected).

📌 Three critical UI fixes (2026-04-27): Fixed PTT voice input when Send disabled, TreeView recursive theming for nested nodes, and themed scrollbars in documentation viewer.

**Fix 1 — PTT activation with Send disabled:** Root cause: Duplicate PTT state machine logic in `MainWindow.xaml.cs` (lines 2460-2487) contained `if (!_isPromptRunning)` guard that blocked voice activation when agents are active. The fix in `PushToTalkController.cs` was correct but wasn't being used. Solution: Removed the `_isPromptRunning` guard from the activation block. Added `_voiceStartedWithSendEnabled` field (line 219) to capture Send button state at PTT start (line 2467). Modified Ctrl-release send logic (line 2552) to AND `_voiceStartedWithSendEnabled` with existing suppress flags. Voice input now activates regardless of Send state; submission respects state at activation time. **Fix 2 — TreeView nested theming:** Previous fix used `ItemContainerStyle` on `DocTopicsTreeView` (MainWindow.xaml lines 1019-1023) which only applied to direct children. WPF's `TreeViewItem` does not inherit `ItemContainerStyle` to nested levels. Solution: Created implicit `TreeViewItem` style in `App.xaml` (after line 382, no `x:Key`) with `Foreground="{DynamicResource LabelText}"`. Removed redundant `ItemContainerStyle` from TreeView XAML. All TreeViewItem instances at any depth now inherit theming automatically. **Fix 3 — Themed docs scrollbars:** The `WebBrowser` control (MainWindow.xaml line 1060) uses native IE/Edge WebView rendering, so WPF ScrollBar styles don't apply. Solution: Added webkit scrollbar CSS to `MarkdownHtmlBuilder.Build()` HTML template (MarkdownDocumentWindow.cs after line 598). Defined `::-webkit-scrollbar`, `::-webkit-scrollbar-track`, `::-webkit-scrollbar-thumb` (+ hover/active) with theme-aware colors matching WPF scrollbar opacity values. Parenthesized ternary operators in interpolated strings to avoid CS8361 errors. Commit: `da3bc95`. Build: 0 errors, 0 warnings.

📌 Three UI fixes (2026-04-27): Fixed transcript panel layout filling, agent name abbreviation, and hyperlink hover theming.

**Fix 1 — Transcript panels fill available space:** Root cause: `RebuildTranscriptPanelsGrid()` (line 4511) was still managing `DocsSplitterColumn` and `DocsPanelColumn` which had been moved to the root grid in a previous fix. The function saved their widths, cleared all columns, then re-added docs columns to `TranscriptPanelsGrid`, creating extra column definitions that prevented transcript panels from filling the available width when docs panel was open. Solution: Removed all docs panel column management logic. Now `TranscriptPanelsGrid.Children.Clear()` and `ColumnDefinitions.Clear()` run unconditionally; only transcript panel columns (star-sized) and splitters (8px) are added. Docs panel lives at root grid level and manages its own columns. Transcripts now properly expand to fill available space regardless of docs panel state. **Fix 2 — Abbreviate "General Purpose Agent" to "GPA":** Added `AbbreviateAgentName()` helper (line 4870) that performs case-insensitive replacement of "General Purpose Agent" with "GPA". Applied to `BuildSecondaryTranscriptTitle()` (secondary panel headers) and `UpdateTranscriptThreadBadge()` (main transcript title). Reduces visual clutter in transcript UI while maintaining clarity. **Fix 3 — Hyperlink hover uses standard theme color:** Added implicit `Hyperlink` style in `App.xaml` (after line 387) with `IsMouseOver` trigger setting `Background` to `{DynamicResource HoverSurface}`. Previously, hyperlinks (transcript links using `thread:` protocol) used WPF's default hover behavior which had poor contrast in dark theme. Now all hyperlinks use the same hover background as buttons (`HoverSurface` = `#252220` in dark, `#E8E0D4` in light), ensuring consistent theming and readability. Commit: `8a0ae77`. Build: 0 errors, 0 warnings.

📌 Documentation tree wiring (2027-01-02): Wired `DocTopicsTreeView` in documentation panel to read real content from `docs/` folder instead of hardcoded seed data.

**Implementation:** Created `DocTopicsLoader.cs` static class with `LoadTopics()` method that parses `docs/SUMMARY.md` (GitBook-style format: `* [Title](path)` with indentation for nesting). Walks up from `AppDomain.CurrentDomain.BaseDirectory` to find repository root (looks for `docs/` or `.git`). Falls back to folder scan if SUMMARY.md missing (extracts first `# Heading` from .md files as title). Auto-expands first top-level item and auto-selects first child. Replaced `PopulateDocumentationTopics()` (MainWindow.xaml.cs line 3760) to call loader and wire `SelectedItemChanged` handler. Added `DocTopicsTreeView_SelectedItemChanged()` handler (after line 3781) that reads markdown from `Tag` property (full file path), calls `MarkdownHtmlBuilder.Build()` with `filePath` parameter for image resolution via `<base href>` tag, and navigates `DocMarkdownViewer` (WebBrowser). Images in markdown (`images/screenshot.png`) now resolve correctly relative to each doc's directory. Commit: `63db33f`. Build: 0 errors, 0 warnings.

📌 Documentation panel state persistence (2027-01-02): Implemented persistence of documentation panel state across app restarts using existing ApplicationSettingsStore/ApplicationSettingsSnapshot/JsonFileStorage pattern.

**Implementation:** Added three new properties to `ApplicationSettingsSnapshot` record in `ApplicationSettingsStore.cs`: `DocsPanelOpen` (bool?, null/true = open default, false = explicitly closed), `DocsExpandedNodes` (string[] of Tag paths or Header strings, null = expand all), `DocsSelectedTopic` (string Tag path, null = first item). Added `SaveDocsPanelClosed()` and `SaveDocsPanelOpen()` public methods to `ApplicationSettingsStore`. Modified `SetDocumentationMode()` in `MainWindow.xaml.cs` to accept optional `persistChange` parameter (default true) and save state on toggle: closed state saves expansion/topic immediately, open state sets `DocsPanelOpen = null` while preserving expansion/topic. Added `_pendingDocsPanelState` field to capture state during `MainWindow_Closing` when panel is open (closed state already persisted on toggle). Added `Task.Run` in `MainWindow_Closed` to write docs panel state during async shutdown. Created four helper methods: `ExpandAllDocNodes()`, `ApplyDocNodeExpansion()`, `FindDocNodeByTag()`, `CollectExpandedDocNodes()` for tree manipulation. Modified `PopulateDocumentationTopics()` to restore expansion and selection from saved state. Added `RestoreDocsPanelState()` called from `RestoreUtilityWindowVisibility()` to restore panel on startup (only if not explicitly closed). State saves to `%LOCALAPPDATA%\SquadDash\settings.json`. Commit: `915ced0`. Build: 0 errors, 0 warnings.


📌 Team update (2026-04-26T16:19:37Z): docs/ scaffold created with 13 markdown files + .gitkeep — decided by Mira Quill

Real and useful documentation for SquadUI users and contributors. Serves as both project documentation and template for repos using SquadUI docs panel feature.

📌 Theme switching UI fixes (2027-01-02): Fixed two theme-related issues in MainWindow for documentation panel and View menu.

**Fix 1 — DocMarkdownViewer theme refresh:** Root cause: When user toggled theme via View menu while documentation panel was open, the WebBrowser displayed stale HTML generated with old theme's CSS colors. `ApplyTheme()` refreshed MarkdownDocumentWindow instances but did not refresh the built-in docs viewer. Solution: Added `RefreshDocumentationViewer()` method (after line 9447) that checks if `DocsPanel` is visible, reads current `DocTopicsTreeView.SelectedItem`, re-reads the markdown file, calls `MarkdownHtmlBuilder.Build()` with current `AgentStatusCard.IsDarkTheme`, and navigates `DocMarkdownViewer` to regenerated HTML. Wired into `ApplyTheme()` after `MarkdownDocumentWindow.RefreshAllOpenWindows()`. Documentation panel now updates theme immediately on toggle. **Fix 2 — Theme menu text simplification:** Changed theme toggle menu item from "Switch to Light Theme" / "Switch to Dark Theme" to "_Light Theme" / "_Dark Theme" (removed "Switch to " prefix). Updated `UpdateThemeMenuState()` (line 9448-9454) and default XAML `Header` (MainWindow.xaml line 372). Cleaner, more concise menu text matching modern UI patterns. Commit: `4e197ae`. Build: 0 errors, 0 warnings.

📌 Docs panel improvements (2027-01-03): Implemented three improvements to documentation panel: verified topic persistence, added splitter width persistence, and enabled clickable links in markdown viewer.

**Task 1 — DocsSelectedTopic persistence verification:** No changes needed. Feature already correctly implemented. Selected topic is saved in both `SaveDocsPanelClosed` (when panel closes) and `_pendingDocsPanelState` (when window closes with panel open). Restored in `PopulateDocumentationTopics` which finds the TreeViewItem by Tag and selects it. **Task 2 — Splitter width persistence:** Added `DocsPanelWidth` and `DocsTopicsWidth` (nullable doubles) to `ApplicationSettingsSnapshot` record (ApplicationSettingsStore.cs). Updated `SaveDocsPanelClosed` and `SaveDocsPanelOpen` to accept optional width parameters. Modified `SetDocumentationMode` to capture `DocsPanelColumn.ActualWidth` and `DocsTopicsColumn.ActualWidth` when closing panel (only if > 0). Modified `ApplyViewMode` to restore widths from snapshot when opening panel (defaults: 600px for panel, 220px for topics). Updated `_pendingDocsPanelState` tuple to include width fields; `MainWindow_Closing` captures widths, `MainWindow_Closed` passes them to save method. Added `x:Name="DocsTopicsColumn"` to first ColumnDefinition in DocsPanel XAML. **Task 3 — Clickable links in markdown viewer:** Added `_currentDocPath` field (string?) to track currently displayed doc for relative link resolution. Modified `DocTopicsTreeView_SelectedItemChanged` to store `filePath` in `_currentDocPath`. Added `DocMarkdownViewer_Navigating` event handler that cancels all navigation and handles it manually: external links (http/https) open in system browser via `Process.Start`; internal .md links resolve relative to current doc directory and call `NavigateToDocByPath` helper; all other navigation (about:blank, anchors, javascript) is ignored. Added `NavigateToDocByPath(string path)` helper that calls `FindDocNodeByTag`, selects the item, and scrolls into view. Wired `Navigating` event in `PopulateDocumentationTopics`. Added `using System.Windows.Navigation` for `NavigatingCancelEventArgs`. Commit: `11cb5e2`. Build: 0 errors, 0 warnings.

📌 Team update (2026-04-27T23:03:43Z): Docs panel improvements — verified DocsSelectedTopic persistence, added DocsPanelWidth + DocsTopicsWidth persistence, enabled clickable links in markdown viewer — decided by Lyra Morn

📌 Notifications Settings UI (2027-01-02): Implemented Phase 1 notification settings UI in PreferencesWindow with QR code generation for ntfy.sh topic subscription.

**Changes:** Added QRCoder NuGet package (1.6.0). Created simplified `PushNotificationService.cs` stub taking ApplicationSettingsSnapshot constructor parameter. Extended `ApplicationSettingsSnapshot` record with 9 new boolean notification fields (NotificationsEnabled, NotificationProvider = "ntfy", NotificationTopic, NotifyOnAiTurnComplete/GitCommit/LoopIterationComplete/LoopStopped/RemoteConnectionEstablished/RemoteConnectionDropped). Added `SaveNotificationSettings(9 parameters)` method to ApplicationSettingsStore. Extended PreferencesWindow with scrollable form (500x700), ntfy.sh topic input with auto-generation, live QR code rendering using QRCoder library, 6 event toggle checkboxes, and Test Notification button. Wired PushNotificationService lifecycle through MainWindow constructor and Preferences callback. Removed ExtractNotificationJson and ResponseText references from existing notification call sites.

**UI patterns:** All controls use `SetResourceReference` for theme compatibility (BodyText, LabelText, InputSurface, SubtleBorder). ScrollViewer wraps main form for overflow handling. GenerateRandomTopic creates squad-dash-{username}-{guid} format. UpdateQrCode renders BitmapByteQRCode on TextChanged. TestButton sends fire-and-forget HTTP POST to ntfy.sh with current UI state. Commit: `8c2d397`. Build: clean (launcher copy error expected when app running).

📌 Diff hover popup on edit tool entries (2027-01-02): Implemented hover-to-preview feature for edit tool transcript entries showing a floating diff viewer with syntax-highlighted added/removed/context lines.

**Implementation:** Created DiffHoverPopup.cs (new file) with DiffLineKind enum (Added/Removed/Context/Header), DiffLine class, and DiffHoverPopup class extending Popup. ParseDiff() static method parses unified diff format using prefix-based detection: + = added (blue tint), - = removed (red tint), +++/---/@@/diff /index  = header (dimmed), space/other = context. ShowDiff() builds UI with ScrollViewer + StackPanel of TextBlocks using theme resources: DiffAddedText foreground with ~15% opacity blue background (#266BAED6), DiffRemovedText foreground with ~15% opacity red background (#26E07070), monospace Consolas font 12px, max 40 lines displayed (truncates with "… N more lines" message), max height 300px scrollable. Border uses CardSurface background and LineColor border. Popup positioned at mouse with 12px offset.

**Wiring in MainWindow.xaml.cs:** Modified CreateToolEntry() (after line 14947) to add hover logic for edit tool entries only (checks descriptor.ToolName == "edit"). On headerPanel.MouseEnter: verifies entry is completed and has OutputText, parses diff, creates popup with PlacementTarget = headerPanel, and shows. On headerPanel.MouseLeave: closes and disposes popup. Popup only appears for completed edit tool entries with non-empty diff content.

**Theme resources:** Uses existing DiffAddedText (#6BAED6 dark / #4682B4 light), DiffRemovedText (#E07070 dark / #CD5C5C light), CardSurface, LineColor, SubtleText, and LabelText from Dark.xaml/Light.xaml. Follows same visual pattern as DocRevisePopup and RevisionWorkingOverlay for consistency.

Commit: 52a9735. Build: clean (DLL compiled successfully; launcher copy failed due to app running — expected).

📌 DiffHoverPopup stability and readability fixes (2027-01-02): Fixed four bugs in diff hover popup for edit tool entries in transcript.

**Bug 1 — Flickering:** Root cause: PlacementMode.Mouse continuously updated popup position every 2-3 seconds as mouse moved, triggering redraw. Fix: Changed to PlacementMode.Absolute with static HorizontalOffset/VerticalOffset captured once via PointToScreen() on MouseEnter (MainWindow.xaml.cs line 14958). Popup position now locked on first display.

**Bug 2 — Mouse following:** Related to Bug 1. Popup followed mouse movement because PlacementMode.Mouse tracked cursor. Fix: Set IsHitTestVisible = false (DiffHoverPopup.cs line 38) to prevent popup from intercepting mouse events and avoid MouseLeave loop (popup covers header → MouseLeave fires → popup closes → header exposed → MouseEnter fires → loop). Popup now stays stationary.

**Bug 3 — Font size:** Increased FontSize from 12px to 13px (DiffHoverPopup.cs line 53) for better readability on standard displays.

**Bug 4 — Header lines shown:** Root cause: ParseDiff() classified header lines (+++, ---, @@, diff, index) as DiffLineKind.Header and displayed them with reduced opacity. Fix: Modified ParseDiff() to skip header lines entirely (lines 120-138). Only display lines starting with + (added), - (removed), or space/empty (context). Also skip \
new
file\, \deleted
file\, and backslash (no-newline markers). Clean diff preview without metadata noise.

**Files:** DiffHoverPopup.cs (lines 35-38, 53, 118-149), MainWindow.xaml.cs (lines 14949-14972). Also added RevisionHighlight theme colors to Dark.xaml (#1A3A5C) and Light.xaml (#D0E4F7) for future revision adorner work (not wired yet — requires TextBox-compatible implementation as MarkdownDocumentWindow uses TextBox not RichTextBox). Commit: bab3da3. Build: 0 errors, 0 warnings.


📌 MarkdownDocumentWindow TextBox → RichTextBox conversion (2026-04-28): Converted the markdown source editor from TextBox to RichTextBox to enable revision highlighting adorner and inline pending indicator during "Revise with AI" operations.

**Key changes:**
- Changed MarkdownDocumentTabState.EditorTextBox from TextBox to RichTextBox (line 1585)
- Replaced all TextBox-specific API calls with RichTextBoxExtensions methods:
  - .Text → .GetPlainText() / .SetPlainText()
  - .SelectionStart / .SelectionLength → .GetSelectionStart() / .GetSelectionLength()
  - .CaretIndex → .GetCaretOffset() / .SetCaretOffset()
  - .SelectedText → .GetSelectedText()
  - .GetRectFromCharacterIndex() → .GetRectFromOffset()
- Added plain-text paste handler using DataObject.AddPastingHandler to force Unicode text paste (prevents rich formatting)
- Wired RevisionHighlightAdorner.Attach() and RevisionPendingIndicator.Insert() in TriggerReviseWithAi() method
- Adorner displays semi-transparent highlight over selected text during AI revision
- Indicator shows animated spinner inline at selection end
- Both are removed when revision completes or fails

**Pattern:** The conversion preserved all existing behavior — RichTextBox is used purely as a plain-text editor with adorner support. RevisionHighlightAdorner follows the SearchHighlightAdorner pattern (scroll tracking, multi-line rect rendering). RevisionPendingIndicator is an InlineUIContainer that's intentionally excluded from GetPlainText() output.

**Files:** MarkdownDocumentWindow.cs (28 call sites converted). RevisionHighlightAdorner.cs and RevisionPendingIndicator.cs already existed from prior work. Build: 0 errors, 0 warnings. Commit: 35c9b5



📌 RichTextBox conversion task completed (2026-05-07T12:15:43Z): MarkdownDocumentWindow editor converted from TextBox → RichTextBox, 28 call sites updated, RevisionHighlightAdorner and RevisionPendingIndicator integrated. Commit: e35c9b5. Build: clean. Tests: passing.

📌 Revise with AI fixes (2026-05-07): Implemented three coordinated fixes for "Revise with AI" feature to improve visual feedback and tracking reliability.

**Fix 1 — Light theme highlight color:** Changed `RevisionHighlight` from blue (#D0E4F7) to soft green (#D4EDDA) in Light.xaml. Added `RevisionHighlightText` resource (#2D3B2D dark gray-green for light theme, #C8E6C9 light green for dark theme). Updated `RevisionHighlightAdorner.Attach()` to apply text color via `TextRange.ApplyPropertyValue(TextElement.ForegroundProperty, ...)` and store original foreground. On `Remove()`, restore original color or unset if none. Color now readable on both themes.

**Fix 2 — Dynamic offset tracking with TextPointers:** Previous implementation used integer char offsets captured at selection time. If user edited text above the revision while AI was working, offsets became stale → "original paragraph has changed" failure. **Solution:** Store **live** (unfrozen) TextPointer anchors instead. In `TriggerReviseWithAi()`: capture `startPointer = tb.Document.ContentStart.GetPositionAtOffset(selStart, LogicalDirection.Forward)` and `endPointer` (Backward). TextPointers automatically track position through document edits. In completion callback: extract current text via `new TextRange(startPointer, endPointer).Text` and compare to `selectedText`. If intact, replace via `replaceRange.Text = revised`. **Updated `RevisionHighlightAdorner.Attach()`** to accept TextPointers directly instead of int offsets, so adorner rendering positions stay correct as document changes. Also updated `MainWindow.xaml.cs` `ShowDocRevisePopup()` with identical pattern.

**Fix 3 — RevisionPendingIndicator as Adorner:** Old implementation inserted `InlineUIContainer` into FlowDocument → appeared in undo stack as editable element. Ctrl+Z would show static spinner artifact. **Solution:** Converted `RevisionPendingIndicator` from FlowDocument insertion to `Adorner`-based overlay. New class extends `Adorner`, positioned at `endPointer.GetCharacterRect(LogicalDirection.Forward)`. Renders three pulsing dots via `DrawingContext.DrawEllipse` in `OnRender()`. Animation uses `DispatcherTimer` (180ms interval) cycling opacity values for wave effect. Factory method: `Attach(RichTextBox tb, TextPointer position)` adds to `AdornerLayer`. `Detach()` removes from layer and stops timers. Never touches FlowDocument, so never enters undo stack. Updated call sites to use `Attach/Detach` instead of `Insert/Remove`.

**Files:** `Themes/Light.xaml` (line 155-156), `Themes/Dark.xaml` (line 154-155), `RevisionHighlightAdorner.cs` (lines 14, 51-75, 77-95, 148-151), `RevisionPendingIndicator.cs` (full rewrite), `MarkdownDocumentWindow.cs` (lines 466-517), `MainWindow.xaml.cs` (lines 9886-9961). Task marked done in `.squad/tasks.md`. Commit: `4f016c5`. Build: 0 errors, 0 warnings.

**Architecture note:** TextPointer live tracking is superior to delta-based adjustment (proposed in original task spec) because WPF maintains the pointer positions automatically across complex edits (insertions, deletions, paragraph restructuring). No event listener or manual offset arithmetic required.
