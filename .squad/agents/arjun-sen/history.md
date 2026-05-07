# Arjun Sen — History & Learnings

## Core Context

**Project:** SquadUI — WPF dashboard for Squad CLI AI agent management  
**Stack:** C# / WPF / .NET 10, NUnit 4.4+, TypeScript SDK  
**Key paths:**
- `SquadDash/` — main application (backend services, stores)
- `SquadDash.Tests/` — NUnit test suite
- `.squad/decisions.md` — architectural decision log

---

## Learnings

📌 Team update (2026-04-18T17-38): Arjun Sen owns three delegated tasks from Orion Vale's audit — decided by Orion Vale

**Task 1 — Implement JsonFileStorage atomic-write helper:**  
5 store classes (`ApplicationSettingsStore`, `PromptHistoryStore`, `RestartCoordinatorStateStore`, `RuntimeSlotStateStore`, `WorkspaceConversationStore`) each duplicate a ~10-line atomic temp-file write pattern. Create `SquadDash\JsonFileStorage.cs` with `JsonFileStorage.AtomicWrite<T>(string path, T payload, JsonSerializerOptions? options = null)` and replace all 5 duplicates. Tests: `JsonFileStorageTests.cs` — new file (Move path), existing file (Copy/Delete path), partial-write safety, round-trip. Acceptance: all 5 stores delegate to `JsonFileStorage.AtomicWrite`; 379 original + new tests passing.

**Task 2 — Seal `AgentThreadRegistry` collections (DEL-2):**  
Replace `internal Dictionary<string, TranscriptThreadState> ThreadsByKey` (and other three exposed collections) with read-only facade accessors (`IReadOnlyDictionary`, `IReadOnlyList`). All mutation must go through `AgentThreadRegistry` methods. Add `ContainsThread`, `TryGetByToolCallId`, `AllThreads` accessors as needed. Callers: MainWindow (6 sites), BackgroundTaskPresenter (3 sites), TranscriptConversationManager (4 sites).

**Task 3 — Encapsulate `_toolEntries` in `AgentThreadRegistry` (DEL-4, bundle with DEL-2):**  
Move `_toolEntries: Dictionary<string, ToolTranscriptEntry>` from MainWindow into `AgentThreadRegistry`. Expose typed accessors: `GetOrAddToolEntry`, `TryGetToolEntry`, `AllIncompleteToolEntries`, `ClearAll`. Removes 8 raw-dict mutations from MainWindow.

**Task 4 — Wire IWorkspacePaths (backend files):**  
Replace all `WorkspacePaths.*` static calls in backend services (`SquadCliAdapter`, `SquadSdkProcess`, `SquadDashRuntimeStamp`, `PromptExecutionController`) with constructor-injected `IWorkspacePaths`. Coordinate with Lyra Morn (UI files) and Jae Min Kade (launcher). Delete `WorkspacePaths.cs` only after all call sites are migrated. Tests: `WorkspacePathsProviderTests.cs` — `Discover()`, constructor normalisation, all 4 properties non-empty, empty-string rejection.

**Task 5 — P2 Fixture Loaders: ✅ COMPLETE (commit `b3a6f88`, 2026-04-25)**  
Delivered `BackgroundTaskFixtureLoader` (domain `"backgroundTask"`) and `QuickReplyFixtureLoader` (domain `"quickReplies"`) in `SquadDash/Screenshots/Fixtures/`. Both registered in `MainWindow.RegisterFixtureLoaders()` at positions 6 and 7. Fixture JSON files added to `docs/screenshots/fixtures/`. Build: 0 errors · Tests: 659/659 passing. Vesper Knox writing unit tests for both loaders (in progress).

**Task 6 — Condensed loop iteration transcript display: ✅ COMPLETE (commit `b74b807`)**  
Added optional `displayPrompt` parameter to `PromptExecutionController.ExecutePromptAsync` to allow separate visible transcript text from AI prompt. MainWindow loop delegate now passes short "🔁 Loop · Iteration N [View loop.md](app://open-loop-md:...)" indicator instead of full loop instructions. Added link handler for `app://open-loop-md:` scheme in MarkdownDocumentRenderer callback to open loop.md in system editor. Full loop instructions still sent to AI — only transcript bubble is condensed. Build: 0 errors · Tests: 1179/1180 passing (1 expected skip). Pattern: optional display override parameters maintain backward compatibility while enabling specialized UI behavior for automation workflows.

**Task 7 — Persist loop dequeue-pause state: ✅ COMPLETE (commit `7afb020`)**  
Added `LoopQueuedToDequeue` nullable bool field to `WorkspaceConversationState` record and wired through `WorkspaceConversationStore` (serialize/deserialize in same pattern as `QueueRightmostHeld`). Extended `TranscriptConversationManager.UpdateQueuedPromptsState` with optional `loopQueuedToDequeue` parameter (default `false`). MainWindow now persists `_loopQueued` flag when set to `true` (2 sites: OnNativeLoopStopped, StartLoopButton_Click) and clears it to `false` when resuming (2 sites: MaybeFireQueuedLoopAsync, HandleLoopStarted). On workspace load, `_loopQueued` restores from `ConversationState.LoopQueuedToDequeue` instead of defaulting to `false`. Auto-resume logic added after queue restoration: if `_loopQueued` and no ready items, immediately invoke `MaybeFireQueuedLoopAsync`; if ready items exist, `DrainQueueIfNeededAsync` already calls `MaybeFireQueuedLoopAsync` after draining. Status label text updated from "⏸ Loop queued — waiting for coordinator" to "⏸ Paused — dequeuing prompts". Build: 0 errors · Tests: 1179/1180 passing. Pattern: nullable bool state fields for transient UI coordination flags that need cross-session persistence; separate display state from persisted state updates.

**Task 8 — Fix Squad agent commit SHA detection in approvals panel: ✅ COMPLETE (commit `deb8d76`, 2026-04-28)**  
Extended `PushNotificationService.ExtractGitCommitSha()` to detect commit SHAs in both git native output (`[branch abc1234] message`) and Squad agent prose (`Committed as \`abc1234\``, `Commit: \`abc1234\``, `**Commit \`abc1234\`**`). Method now accepts optional `agentResponse` parameter for full agent response text. Updated call site in MainWindow.xaml.cs (line 3225) to pass both `ToolEntries.OutputText` and `rawResponse` from `doneCurrentTurn?.ResponseTextBuilder.ToString()`. Pattern tried: git native first (tool outputs), agent pattern on response text second, agent pattern on tool outputs as fallback. Regex: `(?:commit(?:ted)?)\s*(?:as|:)?\s*[*]*\s*\x60([0-9a-f]{7,40})\x60` (case-insensitive). Build: 0 errors. Key learning: Agents report commits in plain prose, not tool output text — always scan full `ResponseText` for semantic extraction tasks. Pattern: dual-source extraction methods accept both structured (tool output) and unstructured (response text) inputs with prioritized pattern matching.

**Task 9 — Extract commit SHA and message from spawned agent tool transcripts: ✅ COMPLETE (commit `deb8d76`, 2026-04-28)**  
Created `GitCommitInfo(CommitSha, CommitMessage)` record in `PushNotificationService.cs` to hold extracted git commit data. Implemented `ExtractGitCommitInfo()` with 4-tier priority: (1) git native with message `\[[\w/.-]+\s+([0-9a-f]{7,40})\]\s+(.+)` in tool outputs (captures both SHA and commit message), (2) agent prose backtick pattern in response text (SHA only), (3) git native SHA-only pattern in tool outputs, (4) agent prose pattern in tool outputs as final fallback. Updated MainWindow "done" event handler (lines 3225-3267) to collect tool outputs from **all agent threads** (main session + spawned agents via `_agentThreadRegistry.ThreadOrder`), iterating both `SavedTurns` and `CurrentTurn.ToolEntries`. Approval description now prioritizes git's commit message when available, falling back to `BuildApprovalDescription(notifSummary, prompt)`. Build: 0 errors. Key architecture insight: spawned agent tool outputs (e.g., `task` tool's child git commits) are NOT in `doneCurrentTurn.ToolEntries` — they live in separate `TranscriptThreadState` objects in `AgentThreadRegistry`. When extracting cross-agent data, always traverse `_agentThreadRegistry.ThreadOrder` and collect from both `SavedTurns.Tools` and `CurrentTurn.ToolEntries`. Pattern: aggregate data sources from distributed transcript state before extraction; use record types for multi-value extraction results; priority-ordered pattern matching with early exit on richest match.

