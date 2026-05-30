# ADR: Panel Docking System

**Status:** Accepted  
**Date:** 2026-06-01  
**Author:** Orion Vale (Lead Architect)  
**Decisions recorded:** 2026-05-29 (answers to open questions)

---

## Problem Statement

All togglable side panels (Tasks, Inbox, Maintenance, Notes, Health, Trace, Approvals, Docs) currently
occupy a single horizontal strip across Row 2 of `MainGrid` (`StatusAgentPanelsGrid`). As the panel
count grows this strip overflows horizontally, competes with the agent area for vertical height, and
gives users no way to reposition panels to better suit their workflow.

We need a panel placement system that:
- Lets users move panels out of the top strip to free vertical space for the agent area and transcript.
- Supports left and right vertical column slots without breaking the existing default layout.
- Is simple enough to ship without drag-and-drop infrastructure.
- Persists per-workspace so each project can have its own preferred layout.

---

## Decision

### Three-Zone Docking Model

Panels may be placed in one of three **dock zones**:

| Zone  | Layout         | Position                                                                 | Default |
|-------|----------------|--------------------------------------------------------------------------|---------|
| `Top`  | Horizontal row | Row 2 of `MainGrid` — current location of all panels                   | ✅ All panels |
| `Left` | Vertical stack | A new column to the **left** of the main content area (active agent area, coordinator transcript, prompt box, and their borders) | ❌ Empty |
| `Right`| Vertical stack | A new column to the **right** of the current rightmost element (right of `DocsPanel` when visible, otherwise right of `MainGrid` column 0) | ❌ Empty |

**Extensibility:** The enum is designed for future additional columns (`Left2`, `Right2`, etc.). The
`PanelDockingService` uses zone identity — not hard-coded column indices — so new zones are additive.

### Interaction Model

- **Ctrl+click** anywhere on a dockable panel → a context menu popup appears with dock-target buttons
  (one per available zone).
- Clicking a button → panel **instantly** moves to that zone.
- No drag-and-drop. No floating windows. No animation (add later if desired).

### Core Abstractions

```
DockZone       — enum: Top | Left | Right  (extensible for Left2/Right2)
PanelSlot      — record: PanelId (string), Zone (DockZone), Order (int)
DockLayout     — class: Name (string), Slots (List<PanelSlot>), CreateDefault()
PanelDockingService — service: CurrentLayout, MovePanel(), SaveLayout(), LoadLayout()
```

**Panel IDs (stable string identifiers):**

| Panel       | PanelId        | WPF element name       |
|-------------|----------------|------------------------|
| Tasks       | `"tasks"`      | `TasksPanelBorder`     |
| Inbox       | `"inbox"`      | `InboxPanelBorder`     |
| Maintenance | `"maintenance"`| `MaintenancePanelBorder`|
| Notes       | `"notes"`      | `NotesPanelBorder`     |
| Health      | `"health"`     | `HealthPanelBorder`    |
| Trace       | `"trace"`      | `TracePanelBorder`     |
| Approvals   | `"approvals"`  | `ApprovalPanelBorder`  |
| Docs        | `"docs"`       | `DocsPanel`            |

### Layout Persistence

- Layouts are serialized as `DockLayout` objects (JSON).
- Each workspace stores its layouts in **`.squad/panel-layouts.json`** (workspace-relative path). This keeps layout state out of the already large `WorkspaceDocsPanelState` record.
- Multiple named layouts are supported: the user can save the current arrangement under a name and recall it later.
- The **active layout name** is also persisted so it survives restart.
- `DockLayout.CreateDefault()` returns the canonical default: all panels in `Top`, ordered by their
  current left-to-right column position.

---

## What Is Explicitly Out of Scope

- **Drag-and-drop** panel reordering or placement.
- **Floating / detached** windows.
- **Resizable zone widths** at this stage (Left/Right columns will have a fixed or auto width initially).
- **Per-panel resize handles** within a zone (future work).
- **Keyboard-only dock shortcut** (deferred to the UI spec task assigned to Mira Quill).
- **Undo/redo** for panel moves.

---

## Layout Architecture Notes

### Current layout (as explored, 2026-06-01)

```
MainGrid (Grid, Margin="12,14,14,14")
  Columns: [* MinWidth=320] [DocsSplitterColumn=0] [DocsPanelColumn=0]
  Row 0: TitlebarGrid (38px)
  Row 1: WorkspaceIssuePanelBorder (Auto, hidden by default)
  Row 2: StatusPanelBorder → StatusAgentPanelsGrid
           Col 0: ActiveAgentsPanelBorder   (Active agents — NOT dockable)
           Col 1: 14px spacer
           Col 2: InactiveAgentsPanelBorder (Roster & History — NOT dockable)
           Col 3: LoopPanelBorder
           Col 4: TasksPanelBorder
           Col 5: WatchPanelBorder
           Col 6: ApprovalPanelBorder
           Col 7: NotesPanelBorder
           Col 8+: Inbox, Maintenance, Health, Trace  (generated/added at runtime)
  Row 3/4: TranscriptPanelsGrid (Star/Auto, position swappable via SetPromptOnTop)
  Row 5: PromptBorder (Auto)
  MainGrid Col 1: DocsSplitterColumn (GridSplitter)
  MainGrid Col 2: DocsPanelColumn → DocsPanel
```

### Future layout (with Left/Right zones)

The `Left` zone will require inserting a new column to the left of column 0 in `MainGrid` (or
wrapping `MainGrid` in an outer `DockPanel` or `Grid`). The `Right` zone will add a column after
`DocsPanelColumn` (or share that column group). Exact approach is deferred to the wiring task
assigned to Orion Vale — the data model is deliberately UI-agnostic.

---

## Consequences

**Positive:**
- Panels freed from the top strip give more vertical space to the agent/transcript area.
- Named layouts let power users switch between workflow configurations instantly.
- Additive change — default state is identical to the current UI; no regression risk.
- Data model is in place before any UI work begins, allowing parallel spec work by Mira Quill.

**Negative / Risks:**
- The Left/Right containers require non-trivial `MainGrid` restructuring (new rows/columns or
  wrapping element). This is the highest-complexity part of the implementation.
- **`DocsPanel` is not a dockable panel.** It remains in its dedicated `MainGrid` column with its existing `GridSplitter`. Only the 7 panels in `StatusAgentPanelsGrid` (Tasks, Inbox, Maintenance, Notes, Health, Trace, Approvals) participate in the docking system.
- Panel height management: Top-zone panels currently match the height of `ActiveAgentsPanelBorder`
  via `MultiBinding`. Left/Right panels will need a different sizing strategy (fill available height).

---

## Resolved Decisions

The following questions from the original draft have been answered:

### D1 — Storage format ✅
**Decision:** Separate `.squad/panel-layouts.json` file per workspace.  
Keeps layout state out of the already large `WorkspaceDocsPanelState` record.

### D2 — DocsPanel special-casing ✅
**Decision:** `DocsPanel` is **not** a dockable panel. It remains in its dedicated root-grid column with its existing `GridSplitter`. The docking system applies only to the 7 panels currently in `StatusAgentPanelsGrid`.

### D3 — Left zone width ✅
**Decision:** Left (and Right) zone columns are **user-resizable via `GridSplitter`**.  
Initial width when the first panel arrives in an empty zone: match the panel's natural/current width (so there's no jarring resize). The `GridSplitter` allows the user to adjust from there.

### D4 — Panel header affordance ✅
**Decision:** The hamburger icon (≡) is shown **always**, regardless of which zone the panel occupies.  
Tooltip text: *"Ctrl+click anywhere on this panel to move it to another zone."*  
The icon serves as a visual affordance and as a direct Ctrl+click target.

**Rationale (resolved by Mira Quill's UI spec task):** Always-visible is more discoverable (users can find Ctrl+click from any zone), produces a consistent header layout in every zone, and requires no dynamic visibility binding or code-behind update after `MovePanel()`. The conditional approach (Left/Right only) would require a Visibility binding or code-behind update and hides the affordance until the user already knows about docking.

**Full specification:** See [panel-docking-ui-spec.md](panel-docking-ui-spec.md) — Section 1.


