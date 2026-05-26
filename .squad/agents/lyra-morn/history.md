# Lyra Morn — History & Learnings

## Core Context

**Project:** SquadUI — WPF dashboard for Squad CLI AI agent management  
**Stack:** C# / WPF / .NET 10, NUnit 4.4+, TypeScript SDK  
**Key paths:**
- `SquadDash/` — main application (WPF)
- `SquadDash.Tests/` — NUnit test suite
- `.squad/decisions.md` — architectural decision log

### Key Experience Areas

**WPF UI Architecture:**
- Grid layout management, GridSplitter interactions, ColumnDefinition/RowDefinition manipulation
- Theme system with DynamicResource bindings for light/dark mode support
- FlowDocument rendering, RichTextBox extensions, custom adorners for overlays
- Popup/adorner positioning patterns for UI feedback elements

**Documentation Panel System:**
- Full-height docs panel with TreeView navigation and WebBrowser markdown rendering
- State persistence (panel open/closed, tree expansion, selected topic, splitter widths) via ApplicationSettingsStore
- Live markdown link resolution and navigation, QR code generation for external features
- Theme-aware HTML generation with webkit scrollbar styling

**Text Editing & Revision:**
- TextPointer-based position tracking through document changes (superior to offset arithmetic)
- RevisionHighlightAdorner and RevisionPendingIndicator for "Revise with AI" visual feedback
- Custom paste handlers for plain-text enforcement in RichTextBox
- Diff parsing and hover preview popups for edit tool transcript entries

**Transcript & Panel Management:**
- Secondary transcript panels with auto-show/hide, countdown cancellation on interaction
- Agent card hover glow effects with pulsing animations
- Panel state preservation across layout rebuilds
- Voice activation (push-to-talk) with send-on-release logic

**Notification & Settings:**
- Notification settings UI with ntfy.sh integration
- QR code generation for mobile subscription
- Event toggle checkboxes for selective notifications

**Common Patterns:**
- ApplicationSettingsSnapshot record + ApplicationSettingsStore for persistence
- Theme resources (BodyText, LabelText, InputSurface, SubtleBorder, CardSurface, etc.)
- Helper methods for tree navigation (FindDocNodeByTag, CollectExpandedDocNodes)
- PlacementMode.Absolute for stable popup positioning (avoid flicker)
- IsHitTestVisible=false on popups to prevent MouseLeave loops

---

## Learnings

📌 Loop panel splitter layout fix (2026-04-27): Fixed three related issues with loop panel splitter and context menus.

**Issue 1 — Splitter moves loop panel left edge (BUG):** Original structure had LoopPanelBorder (col 3), LoopOutputSplitter (col 4), LoopOutputBorder (col 5) as siblings in outer grid with `ResizeBehavior="PreviousAndNext"`. Dragging splitter left would shrink loop controls panel. **Fix:** Wrapped all three elements in inner `LoopSectionGrid` (Grid.Column="3" in outer grid). Inner grid columns: col 0 (Auto, `LoopPanelInnerColDef`) = loop controls, col 1 (5px) = splitter, col 2 (280px) = output. Removed outer columns 4 and 5, decremented Grid.Column for TasksPanel (6→4), WatchPanel (7→5), ApprovalPanel (8→6), NotesPanel (9→7). Updated `SyncLoopOutputPane()` to freeze/unfreeze `LoopPanelInnerColDef` when output shown/hidden: when showing, `MaxWidth = ActualWidth` and `Width = GridLength(ActualWidth)` to lock loop controls width; when hiding, `MaxWidth = PositiveInfinity` and `Width = Auto` to restore. Splitter now only resizes output panel.

**Issue 2 — Hide Loop Output context menu:** Added `Separator` and "Hide Loop Output" `MenuItem` to `LoopOutputBorder.ContextMenu`. Added `LoopOutputHideMenuItem_Click` handler that sets `_loopOutputHasContent = false` and calls `SyncLoopOutputPane()`.

**Issue 3 — Show Loop Output context menu:** Added `Separator` and `x:Name="LoopPanelShowOutputMenuItem"` MenuItem "Show Loop Output" to `LoopPanelBorder.ContextMenu`. Added `LoopPanelShowOutputMenuItem_Click` handler that sets `_loopOutputHasContent = true` and calls `SyncLoopOutputPane()`. Updated `SyncLoopOutputPane()` to toggle menu item visibility: `Collapsed` when output visible, `Visible` when hidden.

**Gotcha:** The Popup (`_loopConfigFlyout`) is NOT a direct child of LoopPanelBorder — it lives in the outer grid between `LoopSectionGrid` and `TasksPanelBorder`. Popups use `PlacementTarget` binding, not Grid.Column.

**Files:** `MainWindow.xaml` (lines 647-662 outer columns, 755-936 inner grid structure, 771-781 loop context menu, 972-980 output context menu), `MainWindow.xaml.cs` (lines 3588-3616 `SyncLoopOutputPane`, new handlers after 3616). Commit: `9b9d756`. Build: 0 errors, 0 warnings. Tests: 1179/1180 passing (1 inconclusive pre-existing).

📌 Six UI polish fixes (2026-04-27): Implemented comprehensive UI improvements addressing menu clarity, docs panel layout, theme support, and interaction feedback.

**Fix 1 — Menu rename:** Changed "View Documentation" menu item to "_Documentation" (cleaner, more concise). **Fix 2 — Docs panel full height:** Moved `DocsSplitter` and `DocsPanel` from `TranscriptPanelsGrid` (row 3 only) to root grid with `Grid.RowSpan="4"` (rows 1-4), spanning workspace issue panel, status panels, transcripts, AND prompt area. Added `ColumnDefinitions` to root grid; set `Grid.Column="0"` on all existing root-level children (TitlebarGrid, WorkspaceIssuePanelBorder, StatusPanelBorder, TranscriptPanelsGrid, PromptBorder). Removed `DocsSplitterColumn` and `DocsPanelColumn` from `TranscriptPanelsGrid`. Documentation panel now spans full window height from top to bottom (excluding titlebar). **Fix 3 — TreeView dark theme:** Added `ItemContainerStyle` to `DocTopicsTreeView` that sets `Foreground="{DynamicResource LabelText}"` on `TreeViewItem` elements. Fixed black text on dark background issue. **Fix 4 — Margin cleanup:** Removed `Margin="8,0,0,0"` from `DocsPanel`, changed to `Margin="0,0,0,0"`. Eliminated gap between topics panel and `GridSplitter`. **Fix 5 — Already-open window flash:** Modified `OpenSecondaryPanel` to detect duplicate window opens (line 4376-4381) and call `FlashGlowHighlight` on the existing `PanelBorder` using the agent's accent color. Users now get visual feedback when clicking a transcript button for an already-open panel (2-3 pulses, 200ms each, blur radius 0→24). **Fix 6 — Glow auto-fade timer:** Added `_glowFadeTimer` field (`DispatcherTimer?`). Modified `AgentCardBorder_MouseEnter` to start a 3-second timer that stops the glow animation automatically. Timer resets on each mouse enter, stops on mouse leave. Glow no longer pulses indefinitely — fades after 3 seconds of hover. Commit: `5fd250b`. Build: 0 errors (SquadDash.App.dll compiled successfully; launcher exe copy failed due to running process, expected).

📌 Three critical UI fixes (2026-04-27): Fixed PTT voice input when Send disabled, TreeView recursive theming for nested nodes, and themed scrollbars in documentation viewer.

**Fix 1 — PTT activation with Send disabled:** Root cause: Duplicate PTT state machine logic in `MainWindow.xaml.cs` (lines 2460-2487) contained `if (!_isPromptRunning)` guard that blocked voice activation when agents are active. The fix in `PushToTalkController.cs` was correct but wasn't being used. Solution: Removed the `_isPromptRunning` guard from the activation block. Added `_voiceStartedWithSendEnabled` field (line 219) to capture Send button state at PTT start (line 2467). Modified Ctrl-release send logic (line 2552) to AND `_voiceStartedWithSendEnabled` with existing suppress flags. Voice input now activates regardless of Send state; submission respects state at activation time. **Fix 2 — TreeView nested theming:** Previous fix used `ItemContainerStyle` on `DocTopicsTreeView` (MainWindow.xaml lines 1019-1023) which only applied to direct children. WPF's `TreeViewItem` does not inherit `ItemContainerStyle` to nested levels. Solution: Created implicit `TreeViewItem` style in `App.xaml` (after line 382, no `x:Key`) with `Foreground="{DynamicResource LabelText}"`. Removed redundant `ItemContainerStyle` from TreeView XAML. All TreeViewItem instances at any depth now inherit theming automatically. **Fix 3 — Themed docs scrollbars:** The `WebBrowser` control (MainWindow.xaml line 1060) uses native IE/Edge WebView rendering, so WPF ScrollBar styles don't apply. Solution: Added webkit scrollbar CSS to `MarkdownHtmlBuilder.Build()` HTML template (MarkdownDocumentWindow.cs after line 598). Defined `::-webkit-scrollbar`, `::-webkit-scrollbar-track`, `::-webkit-scrollbar-thumb` (+ hover/active) with theme-aware colors matching WPF scrollbar opacity values. Parenthesized ternary operators in interpolated strings to avoid CS8361 errors. Commit: `da3bc95`. Build: 0 errors, 0 warnings.

📌 Three UI fixes (2026-04-27): Fixed transcript panel layout filling, agent name abbreviation, and hyperlink hover theming.

**Fix 1 — Transcript panels fill available space:** Root cause: `RebuildTranscriptPanelsGrid()` (line 4511) was still managing `DocsSplitterColumn` and `DocsPanelColumn` which had been moved to the root grid in a previous fix. The function saved their widths, cleared all columns, then re-added docs columns to `TranscriptPanelsGrid`, creating extra column definitions that prevented transcript panels from filling the available width when docs panel was open. Solution: Removed all docs panel column management logic. Now `TranscriptPanelsGrid.Children.Clear()` and `ColumnDefinitions.Clear()` run unconditionally; only transcript panel columns (star-sized) and splitters (8px) are added. Docs panel lives at root grid level and manages its own columns. Transcripts now properly expand to fill available space regardless of docs panel state. **Fix 2 — Abbreviate "General Purpose Agent" to "GPA":** Added `AbbreviateAgentName()` helper (line 4870) that performs case-insensitive replacement of "General Purpose Agent" with "GPA". Applied to `BuildSecondaryTranscriptTitle()` (secondary panel headers) and `UpdateTranscriptThreadBadge()` (main transcript title). Reduces visual clutter in transcript UI while maintaining clarity. **Fix 3 — Hyperlink hover uses standard theme color:** Added implicit `Hyperlink` style in `App.xaml` (after line 387) with `IsMouseOver` trigger setting `Background` to `{DynamicResource HoverSurface}`. Previously, hyperlinks (transcript links using `thread:` protocol) used WPF's default hover behavior which had poor contrast in dark theme. Now all hyperlinks use the same hover background as buttons (`HoverSurface` = `#252220` in dark, `#E8E0D4` in light), ensuring consistent theming and readability. Commit: `8a0ae77`. Build: 0 errors, 0 warnings.

📌 Team update (2026-04-27T23:03:43Z): Docs panel improvements — verified DocsSelectedTopic persistence, added DocsPanelWidth + DocsTopicsWidth persistence, enabled clickable links in markdown viewer — decided by Lyra Morn

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

📌 Inbox message viewer copy bug fix (2026-05-26): Fixed clipboard copy behavior to preserve code samples.

**Issue:** When selecting text that includes code blocks in the inbox message viewer and right-clicking to copy, code samples were stripped from the clipboard content.

**Root cause:** WPF's FlowDocument default copy handler can strip Paragraph elements with custom backgrounds (used for code blocks). Even though code blocks are rendered as flow Paragraph elements to participate in selection, the default serialization skips them.

**Fix:** Added DataObject.AddCopyingHandler to InboxMessageWindow constructor (line 126). The handler intercepts copy events, extracts plain text via selection.Text (which properly includes all Paragraph content), sets it to clipboard, and cancels the default operation.

**Files:** InboxMessageWindow.cs (added import line 7, handler registration line 126, new method lines 138-163). Build: 0 errors, 0 warnings.


📌 Team update (2026-05-26T15:31:00Z): Clipboard copy bug fixed in inbox message viewer — decided by Lyra Morn

📌 Attachment text styling consistency fix (2026-05-11): Fixed approval attachments to use consistent text styling with all other attachment types.

**Issue:** Approval attachments were using `SubtleText` for their description text while all other attachment types (inbox-message, image, inbox-excerpt, task-ref, note, transcript quote) used `LabelText`. This created a visual inconsistency where approval attachment text appeared too subtle/low-contrast compared to other attachments.

**Fix:** Changed line 27666 in `MainWindow.xaml.cs` (`AppendCommitFollowUpInlines` method) from `suffix.SetResourceReference(Run.ForegroundProperty, "SubtleText")` to `suffix.SetResourceReference(Run.ForegroundProperty, "LabelText")`.

**Audit results:** Verified all attachment types now consistently use `LabelText` for description text:
- inbox-message (line 27555) ✅
- image (line 27567) ✅
- inbox-excerpt (lines 27585, 27598) ✅
- task-ref/topic-ref/file/text/url (line 27598) ✅
- note (line 27585) ✅
- transcript quote (line 27626) ✅
- approval (line 27666) ✅ FIXED

**Pattern:** The UI maintains visual hierarchy with:
- `ImportantText` (#53371E light / #E5D5C0 dark) - Highest priority elements (titles)
- `LabelText` (#3C2B1E light / #D8C8B0 dark) - Standard labels & attachment descriptions
- `BodyText` - Lower emphasis text
- `SubtleText` - Lowest emphasis (icons, prefixes like "↩")

**Files:** MainWindow.xaml.cs (line 27666). Commit: `4b39b04`. Build: 0 errors, 0 warnings.

