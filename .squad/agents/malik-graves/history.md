# Malik Graves — History & Learnings

## Core Context

**Project:** SquadDash — WPF dashboard for Squad CLI AI agent management
**Stack:** C# / WPF / .NET 10, NUnit 4.4+, TypeScript SDK
**Key markdown files:**
- `.squad/maintenance.md` — autonomous maintenance task definitions (frontmatter + task blocks)
- `.squad/tasks.md` — full task backlog (🔴/🟡/🟢 priority sections, `- [ ]`/`- [x]` items)
- `.squad/team.md` — active roster table
- `.squad/routing.md` — work-type → agent routing table
- `.squad/decisions.md` — architectural decision log
- `loop*.md` — loop configuration files (frontmatter + options blocks)
- `docs/features/maintenance-mode.md` — developer runbook for Maintenance Mode

---

## Markdown Format Reference

### maintenance.md

```yaml
---
configured: true          # Must be true for SquadDash to load tasks
idle_timeout: 15          # Minutes of inactivity before maintenance triggers
max_tasks_per_session: 5  # Max tasks per window
safety: branch            # Global safety floor: report-only | branch | direct
---
```

Task block format (level-2 heading = task slug):
```markdown
## task-slug

enabled: false
frequency: daily          # daily | per-commit | always
safety: branch            # Per-task; cannot be less safe than global floor
title: Human Readable Title
instructions: |
  Multi-line prompt instructions. Use {{option_key}} for option injection.

options:
  option_key:
    type: radio
    label: Label shown in UI
    default: choice-a
    choices: choice-a | choice-b
      choice-a: Description
      choice-b: Description
```

### tasks.md

```markdown
## 🔴 High Priority
- [ ] Task description
  Optional indented detail shown in hover preview.

## 🟡 Mid Priority
- [ ] Another task *(Owner: agent-handle)*

## 🟢 Low Priority

## ✅ Done
- [x] **[Tag] Completed task title** — ✅ Brief summary of what was done
```

---

## Learnings

### 2026-05-20 — Hired

Joined SquadDash as Markdown Specialist. Key orientation facts:

- `maintenance.md` uses `configured: false` by default — must be flipped to `true` before any tasks execute
- The `safety:` global floor is enforced at runtime by `MaintenanceRunner.ApplySafetyFloor()` — a task set to `direct` when global is `branch` is silently promoted. This is now traced via `SquadDashTrace.Write`.
- `ToggleTaskEnabled` in `MaintenancePanelController` looks for `enabled:` at indent 4 inside a task's YAML block — must be `    enabled: true/false` (4 spaces)
- `tasks.md` priority-section emoji must match exactly: 🔴, 🟡, 🟢, ✅ — the Tasks panel parser is case/emoji-sensitive
- Owner tags on task lines: ` *(Owner: agent-handle)*` — note the leading space before `*(`
- Done items in `tasks.md` should use bold tag format: `- [x] **[Tag] Title** — ✅ summary`
- `loop*.md` options blocks follow the same `type: radio` / `choices:` format as `maintenance.md` options
