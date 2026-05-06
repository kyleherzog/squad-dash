---
configured: true
interval: 10
timeout: 60
description: "Task Runner — works through your .squad/tasks.md backlog one task at a time"
commands: [stop_loop]
---

# Filtered Task Runner

You are running as part of a SquadDash autonomous loop. **Each iteration must complete exactly one task** from `.squad/tasks.md`, then stop. The next iteration will pick up the next task.

## Step 1 — Find the next actionable task

Read `.squad/tasks.md`. Find the **first unchecked (`- [ ]`) item** that is NOT owned by `*(Owner: User)*`. Work top-to-bottom; higher sections (🔴 High, 🟡 Mid) take priority over lower ones (🟢 Low).

## Step 2 — If NO actionable tasks remain

No unchecked tasks remain (or all remaining tasks are `*(Owner: User)*`). Do the following and nothing else:

1. Append this block at the **very end** of your response (after all other content):
   ```
   HOST_COMMAND_JSON:
   [
     { "command": "stop_loop" }
   ]
   ```
2. Do not attempt any further work this iteration.

## Step 3 — If a task IS found, implement it fully

1. Read `.squad/routing.md` to identify the correct owner/agent for this task.
2. Delegate to or become that agent. Complete the work — implementation, decisions, tests, commit.
3. For **"define…" or "decide…" or "architecture" tasks**: document the decision in `.squad/decisions.md` (create if missing) and update relevant architecture docs, then consider the task done.
4. For **implementation tasks**: build, verify tests pass, commit with the `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` trailer. Report the commit SHA.
5. After work is complete, mark the task `[x]` in `.squad/tasks.md` and move it to the `## ✅ Done` section at the bottom.
6. Report a one-line summary of what was done.

## Reference material

- `.squad/tasks.md` — the full task backlog
- `.squad/routing.md` — who owns what
- `.squad/team.md` — squad roster
