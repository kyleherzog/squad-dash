---
title: Layout Presets
nav_order: 4
parent: Features
---

# Layout Presets

Save and restore your panel layout instantly with 3 saved presets. Perfect for switching between different work modes—coding, debugging, documentation review—without manually repositioning panels every time.

---

## Overview

**Layout Presets** let you capture your current panel configuration (positions, visibility, docking zones, column widths) and restore it with a single keyboard shortcut or menu click.

### What Gets Saved

Each layout preset stores:
- **Panel positions** — Which panels are open and where they're docked
- **Visibility state** — Which panels are visible vs. hidden
- **Panel widths** — Column widths in multi-column zones
- **Docking zones** — How panels are arranged (side-by-side, stacked, etc.)

### What Doesn't Get Saved

- Panel **content** (transcripts, documents, etc.) is independent of layout
- **Window size or position** on your monitor
- **Font sizes** or other appearance settings

---

## How to Save a Layout

### Using Keyboard Shortcut

**Shift+F7, Shift+F8, or Shift+F9** — Press to save your current layout to slot 1, 2, or 3:

| Shortcut | Saves to |
|---|---|
| **Shift+F7** | Slot 1 |
| **Shift+F8** | Slot 2 |
| **Shift+F9** | Slot 3 |

A **status notification** appears briefly confirming the save:

```
✓ Layout saved to Slot 1
```

### Using the Menu

1. Click **View** menu
2. Hover over **Save Layout**
3. Click your desired slot (1, 2, or 3)

![Menu navigation for saving layout](images/save-layout-menu.png)
> 📸 *Screenshot needed: View menu with "Save Layout" submenu showing slots 1, 2, 3*

---

## How to Restore a Layout

### Using Keyboard Shortcut

**F7, F8, or F9** — Press to restore a layout from slot 1, 2, or 3:

| Shortcut | Restores from |
|---|---|
| **F7** | Slot 1 |
| **F8** | Slot 2 |
| **F9** | Slot 3 |

The restored layout applies instantly. A **status notification** appears confirming the operation:

```
✓ Layout restored from Slot 1
```

### Using the Menu

1. Click **View** menu
2. Hover over **Restore Layout**
3. Click your desired slot (1, 2, or 3)

![Menu navigation for restoring layout](images/restore-layout-menu.png)
> 📸 *Screenshot needed: View menu with "Restore Layout" submenu showing slots 1, 2, 3*

---

## Practical Use Cases

### Scenario 1: Coding vs. Reviewing

- **Slot 1 (Coding Layout):** Transcript panel wide on the right, Tasks and Loop panels stacked on the left, Prompt bar visible
- **Slot 2 (Review Layout):** Documentation panel maximized, Agent cards visible, Transcript in focus mode

Switch between them:
- Code: **F7**
- Review: **F8**

### Scenario 2: Single Agent Focus

Save a layout that hides all but one agent's transcript and maximizes it. Restore with **F9** whenever you need deep focus on that agent's work.

### Scenario 3: Presentation Mode

Create a presentation layout with:
- Fullscreen transcript (**F11**)
- Agent cards hidden
- Large font

Save it to a slot, then restore instantly between demo segments.

---

## Persistence

Layouts are saved automatically to `.squad/panel-layout-presets.json` in your workspace folder.

**Important:** Layouts persist **per workspace**. Each workspace folder has its own set of 3 presets, stored locally. You can safely work across multiple projects—each maintains independent layouts.

---

## Empty Slot Behavior

If you try to restore from an **empty slot** (one you haven't saved to yet):

```
⚠ No layout saved in Slot 1
```

The layout doesn't change. Save a layout first using **Shift+F7/F8/F9** before you can restore from that slot.

---

## Keyboard & Menu Summary

### Restore (Recall Saved Layout)

| Shortcut | Menu Path | Effect |
|---|---|---|
| **F7** | View → Restore Layout → Slot 1 | Restore layout from Slot 1 |
| **F8** | View → Restore Layout → Slot 2 | Restore layout from Slot 2 |
| **F9** | View → Restore Layout → Slot 3 | Restore layout from Slot 3 |

### Save (Store Current Layout)

| Shortcut | Menu Path | Effect |
|---|---|---|
| **Shift+F7** | View → Save Layout → Slot 1 | Save current layout to Slot 1 |
| **Shift+F8** | View → Save Layout → Slot 2 | Save current layout to Slot 2 |
| **Shift+F9** | View → Save Layout → Slot 3 | Save current layout to Slot 3 |

---

## Tips & Tricks

- **Quick Workspace Setup:** Arrange your ideal layout, save it to Slot 1, and it will restore automatically when you reopen the workspace.
- **Keep Slot 1 as Default:** Many users save their "normal working layout" to Slot 1 and use Slots 2 and 3 for specialized configurations.
- **Test Before Overwriting:** If you're unsure about replacing a saved layout, save to an empty slot first, then decide which to keep.

---

## Troubleshooting

### Shortcut Not Working
- Ensure a **SquadDash window is active** (focused) before pressing the shortcut
- Verify you're not in a text input field where **F7–F9** might be intercepted by the text box

### Layout Changes Not Saving
- Confirm you pressed **Shift+F7/F8/F9** (not just **F7/F8/F9**)
- Check the status notification for confirmation
- Verify your workspace folder has write permissions to `.squad/`

### Layouts Not Persisting Across Sessions
- Check that `.squad/panel-layout-presets.json` exists and is writable
- If you move your workspace folder, you'll need to re-save your layouts in the new location

---

## Related

- **[Keyboard Shortcuts](../reference/keyboard-shortcuts.md)** — All global and context-specific shortcuts
- **[View Modes](README.md)** — Fullscreen, focus modes, and other view options
