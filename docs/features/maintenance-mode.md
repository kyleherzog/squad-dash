---
title: Maintenance Mode
nav_order: 10
parent: Features
---

# Maintenance Mode

Maintenance Mode lets SquadDash run autonomous housekeeping tasks — tests, refactors, reports, and more — during idle windows while you're away from the keyboard. Tasks are defined in `.squad/maintenance.md`, controlled by a global safety floor, and each run produces a timestamped report.

---

## How it works

1. SquadDash monitors keyboard/mouse activity via `IdleDetectionService`.
2. After `idle_timeout` minutes of inactivity, it loads `.squad/maintenance.md`, evaluates which tasks are eligible (based on frequency and run history), and executes them in order via the normal agent prompt pipeline.
3. Each completed session writes a report to `.squad/maintenance-reports/`.
4. Activity (a keypress, mouse movement) interrupts the session cleanly between tasks.

---

## `maintenance.md` — structure

The file lives at `.squad/maintenance.md` and has two parts: a YAML frontmatter block followed by task blocks.

### Frontmatter

```yaml
---
configured: true          # Required — SquadDash ignores the file without this
idle_timeout: 15          # Minutes of inactivity before a window triggers (default: 15)
max_tasks_per_session: 5  # Max tasks to run per window; stops after this many (default: 5)
safety: branch            # Global safety floor (see Safety Model below)
---
```

`configured: true` is a deliberate opt-in gate. SquadDash will not load the file (and will not enter Maintenance Mode) if this key is absent or set to `false`.

### Task blocks

Each task follows a `## task-slug` heading inside the body of the file:

```markdown
## run-tests

enabled: false
frequency: daily
safety: branch
title: Run Tests
instructions: |
  Run all tests in the repository using the appropriate test runner
  (e.g. `dotnet test`, `npm test`, `go test ./...`).
  Report any failures.
```

All tasks in the default `.squad/maintenance.md` ship with `enabled: false` — you opt in to exactly the tasks you want.

| Field          | Required | Default  | Description                                            |
|----------------|----------|----------|--------------------------------------------------------|
| `enabled`      | yes      | `false`  | Whether this task can run                              |
| `frequency`    | yes      | `daily`  | How often to run (see Frequency below)                 |
| `safety`       | no       | global   | Per-task safety level; cannot exceed global floor      |
| `title`        | no       | task ID  | Human-readable label shown in the Maintenance panel    |
| `instructions` | yes      | —        | Prompt text injected when the task runs                |

Tasks may also include an `options:` block (same format as `loop.md`) to let users configure runtime behaviour via the panel UI.

---

## Enabling and disabling tasks

### In the file

Set `enabled: true` or `enabled: false` on the relevant task block in `.squad/maintenance.md`:

```diff
## run-tests

-enabled: false
+enabled: true
 frequency: daily
 safety: branch
```

### In the Maintenance panel

The **Maintenance panel** in SquadDash displays each task as a row with a checkbox. Checking or unchecking the box:

1. Reads `.squad/maintenance.md` from disk.
2. Flips `enabled: false ↔ true` for the target task ID in place, preserving all other content.
3. Writes the file back atomically.
4. Reloads the panel to reflect the new state.

Both methods are equivalent — the file is the single source of truth.

---

## Frequency values

| Value           | Behaviour                                                                                          |
|-----------------|----------------------------------------------------------------------------------------------------|
| `daily`         | Runs at most once per UTC calendar day; skipped on subsequent windows the same day                 |
| `weekly`        | Runs at most once per Monday–Sunday calendar week (UTC); skipped on subsequent windows the same week |
| `monthly`       | Runs at most once per calendar month; skipped on subsequent windows the same month                 |
| `after-commits` | Runs once per unique HEAD commit SHA; re-runs automatically when new commits land                  |
| `per-commit`    | Backward-compat alias for `after-commits`; identical behaviour; prefer `after-commits` in new configs |
| `always`        | Runs every maintenance window regardless of when it last ran                                       |

> **`after-commits` fallback:** If git is unavailable or HEAD cannot be resolved, `after-commits` tasks (and the `per-commit` alias) fall back to `daily` behaviour and a trace entry is written to the SquadDash trace log.

---

## Safety model

Every task runs with an *effective safety level* determined by the stricter of the global floor and the task's own setting.

| Level         | What the AI may do                                                              |
|---------------|---------------------------------------------------------------------------------|
| `report-only` | No file changes. Findings are written to the session transcript only.           |
| `branch`      | Creates branch `maintenance/YYYYMMDD-<task-slug>` before any edits. Commits go to that branch; the current branch is never touched. **Recommended default.**<br><br>⚠ **Multi-task sessions:** Each task receives a "create branch from the current HEAD" instruction. If a prior task in the same session committed to its branch, the next task branches from that branch — not from your main/default branch. A fix to inject an explicit base-branch checkout is tracked in the backlog. For now, verify multi-task `branch`-safety session outputs before merging. |
| `direct`      | Commits directly to the current branch. Use only for tasks that are safe by design (e.g. writing only to `tasks.md`). |

**Safety floor rule:** the global `safety:` value is a *floor*. A per-task setting cannot be less safe than the global value.

```
Rank:  report-only (2)  >  branch (1)  >  direct (0)
```

Examples:

| Global safety | Task safety   | Effective safety |
|---------------|---------------|------------------|
| `branch`      | `report-only` | `report-only`    |
| `branch`      | `branch`      | `branch`         |
| `branch`      | `direct`      | `branch` ⚠ promoted |
| `report-only` | `branch`      | `report-only` ⚠ promoted |
| `direct`      | `direct`      | `direct`         |

When a task's safety is promoted by the floor, a `⚠ Safety downgraded from '…' to '…' by global floor.` note appears in the maintenance report for that task.

To allow `direct` on any task, you must set the **global** `safety: direct`.

---

## Reports

After every maintenance window SquadDash writes a Markdown report to:

```
.squad/maintenance-reports/YYYYMMDD-HHmmss.md
```

The filename uses local time. Reports are automatically pruned to the **30 most recent** files.

### Report format

```markdown
# Maintenance Report — 2026-05-20 03:14

**Session duration:** 4m 32s

## Tasks Run

- ✅ Run Tests (run-tests) — 2m 11s
- ✅ TODO / FIXME / HACK Scanner (todo-fixme-scan) — 47s
- ❌ Code Smell Cleanup (code-smells) — error during execution
  ⚠ Safety downgraded from 'direct' to 'branch' by global floor.

## Branches Created

- maintenance/20260520-code-smells

## Files Changed

- src/Utils.cs
- src/Parser.cs
```

Outcome icons: `✅` completed · `⏭` skipped · `❌` error · `⏸` interrupted.

---

## Inbox messages and deferred actions

Maintenance tasks can send messages to your **Inbox panel** using the `INBOX_MESSAGE_JSON` block format. This is especially useful for `report-only` tasks that find something worth your attention.

### Sending an inbox message

At the end of its output, any maintenance agent can include:

```
INBOX_MESSAGE_JSON:
{
  "subject": "Brief subject line",
  "from": "argus-weld",
  "body": "Full Markdown report body",
  "attachments": [],
  "actions": [
    {
      "label": "Fix this",
      "routeMode": "start_named_agent",
      "targetAgent": "arjun-sen",
      "prompt": "Arjun: during maintenance on 2026-05-22 I found X in src/Foo.cs:42. Please fix it."
    },
    {
      "label": "Add to backlog",
      "routeMode": "start_coordinator",
      "prompt": "Add a mid-priority task: [description of finding from maintenance on 2026-05-22]"
    },
    {
      "label": "Dismiss",
      "routeMode": "done"
    }
  ]
}
```

### Deferred action buttons

The `actions` array renders as clickable buttons in the Inbox viewer. Because maintenance runs while you are away, these buttons let you defer a decision until you return:

- **`start_named_agent`** — routes to a specific specialist when clicked.
- **`start_coordinator`** — routes to the coordinator when clicked.
- **`done`** — dismisses the message with no follow-up.

> **Self-contained prompts:** Each `prompt` must include all the context needed to act on the finding — file paths, line numbers, symptoms, relevant background. There is no conversation history available when the button is clicked; the prompt is the entire briefing.

### Why not `QUICK_REPLIES_JSON`?

`QUICK_REPLIES_JSON` is a live quick-reply mechanism that pauses and waits for the user to click. Maintenance runs while you are away, so using `QUICK_REPLIES_JSON` in a maintenance task would leave the session waiting indefinitely. Use `actions` in inbox messages instead — they are fully deferred and safe to compose at any time.

---

## State store — `maintenance-state.json`

SquadDash tracks per-task run history in:

```
<workspace-root>/maintenance-state.json
```

This file is **automatically added to `.gitignore`** the first time a maintenance window runs (via `SquadInstallerService.EnsureMaintenanceStateInGitIgnore`). You will never accidentally commit it.

### File format

```json
{
  "tasks": {
    "run-tests": {
      "lastRunAt": "2026-05-20T03:14:22.0000000Z",
      "lastCommitSha": "a1b2c3d4e5f6..."
    },
    "todo-fixme-scan": {
      "lastRunAt": "2026-05-20T03:16:09.0000000Z",
      "lastCommitSha": "a1b2c3d4e5f6..."
    }
  }
}
```

### Resetting state

To force all tasks to be eligible on the next idle window, delete the file:

```powershell
Remove-Item maintenance-state.json
```

To reset a single task, open the file and delete its entry from the `tasks` object, then save.

---

## Testing the idle trigger locally

Use the built-in SquadDash command `trigger_idle_cycle` to force a maintenance window immediately without waiting for the idle timeout:

1. Open the prompt box in SquadDash.
2. Type: `trigger_idle_cycle`
3. Press **Enter**.

SquadDash will wait for any currently-running prompt or Loop iteration to finish, then start the maintenance cycle exactly as it would after a real idle timeout.

> This command is **silent** — it fires and returns immediately. Watch the Maintenance panel for status updates.

### Typical local test workflow

```text
1. Set idle_timeout: 1 in maintenance.md  (optional — trigger_idle_cycle skips the wait anyway)
2. Enable at least one task:  enabled: true
3. Run:  trigger_idle_cycle
4. Watch the Maintenance panel — the banner changes to "Running: <task title>"
5. Check .squad/maintenance-reports/ for the generated report
6. Inspect maintenance-state.json to confirm lastRunAt was updated
```

---

## Quick reference

| Topic                | Detail                                            |
|----------------------|---------------------------------------------------|
| Config file          | `.squad/maintenance.md`                           |
| Opt-in gate          | `configured: true` in frontmatter                 |
| Enable a task        | `enabled: true` in task block, or panel checkbox  |
| Default safety       | `branch` (creates `maintenance/YYYYMMDD-<slug>`)  |
| Reports location     | `.squad/maintenance-reports/YYYYMMDD-HHmmss.md`   |
| Report retention     | 30 most recent, auto-pruned                       |
| State file           | `<workspace-root>/maintenance-state.json`         |
| Reset state          | Delete `maintenance-state.json`                   |
| Test trigger         | `trigger_idle_cycle` command in SquadDash         |
| `after-commits` frequency | Runs once per new HEAD commit SHA; alias: `per-commit`  |
| Inbox messages   | `INBOX_MESSAGE_JSON` block at end of task output        |
| Deferred actions | `actions` array in inbox messages; rendered as buttons  |
