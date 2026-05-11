---
title: Paste Image
nav_order: 6
parent: Features
---

# Paste Image

SquadDash lets you paste a clipboard image — a screenshot, bitmap, or any image copied to the clipboard — directly into the prompt input box. The image is saved locally and attached to your prompt so the Copilot CLI agent can analyse it using its `view` tool.

---

## Pasting an Image

1. Copy an image to your clipboard (e.g. **PrintScreen**, **Win+Shift+S**, or right-click → Copy Image in a browser).
2. Click inside the **prompt text box** (or ensure it has focus).
3. Press **Ctrl+V**.

If the clipboard contains an image, SquadDash intercepts the paste and:

- Saves the image as a PNG to the workspace's `pasted-images\` folder.
- Displays a **📷 Image** attachment pill below the prompt text box.

If the clipboard contains only text, **Ctrl+V** behaves normally and pastes the text.

> **Tip:** You can paste an image and then continue typing your prompt — they are submitted together.

---

## The Attachment Strip

Once an image is pasted, a **📷 Image** pill appears in the attachment strip beneath the prompt text box.

- **Click the pill** to open the image in the built-in **Image Viewer** tab, so you can verify what was captured before sending.
- You can paste multiple images; each appears as its own pill.

---

## Submitting a Prompt with an Image

When you press **Send** (or **Enter**), SquadDash automatically injects the image reference into the prompt text:

```
[Attached image: C:\Users\...\pasted-images\{id}.png]
```

This lets the Copilot CLI agent call its `view` tool on the file path and analyse the image in the same turn as your text prompt.

> **Note:** The injection is invisible in the prompt text box — it is appended at dispatch time, not shown in the editor.

---

## Transcript Links

After a prompt with an attached image dispatches, the transcript records the attachment as a clickable **📷 Image** link. Clicking it re-opens the image in the built-in viewer.

If the image has since been pruned (see [Retention & Cleanup](#retention--cleanup) below), the viewer shows:

> *This image has expired and been deleted.*

---

## Retention & Cleanup

Pasted images are stored in:

```
%LocalAppData%\SquadDash\workspaces\{workspace}\pasted-images\
```

SquadDash applies a two-tier automatic retention policy:

| Type | Retention |
|---|---|
| **Submitted** images (attached to a sent prompt) | Deleted **14 days** after submission |
| **Unsent** images (pasted but never submitted) | Deleted **30 days** after the file was created |

Pruning runs automatically in the background each time a workspace loads. No action is required.

---

## Clearing All Pasted Images Manually

To delete all pasted images for the current workspace immediately:

1. Open the **_Cleanup** menu (top menu bar).
2. Click **Clear pasted images…**
3. Confirm the dialog.

SquadDash reports how much disk space was freed.

> **Note:** This deletes all pasted images for the current workspace, including any that have not yet been sent. Existing transcript links to those images will show the "expired" message if clicked.

---

## Related

- **[Entering Prompts](entering-prompts.md)** — The prompt text box and how prompts are sent
- **[Keyboard Shortcuts](../reference/keyboard-shortcuts.md)** — Ctrl+V and other prompt shortcuts
- **[Transcripts](../concepts/transcripts.md)** — How attachments appear in the conversation history


---

## Annotation Editor

When you press **Ctrl+V** with an image on the clipboard, SquadDash opens the **Annotation Editor** — a full-screen dialog where you can mark up the image before attaching it.

---

### Toolbar Buttons

| Button | Icon | Description |
|--------|------|-------------|
| **Arrow** | 🡲 | Drag to draw an annotation arrow. The tail is where you start dragging; the arrowhead lands where you release. |
| **Rectangle** | ☐ | Drag to draw a rectangle annotation box. Useful for circling or highlighting regions. |
| **Cursor indicator** | ↖ | Click once to enter placement mode, then click on the canvas to stamp a mouse-cursor overlay at that point. |
| **Eyedropper** | ⊕ | Click anywhere on the canvas to sample a pixel color. The hex value appears in the toolbar for easy copying. |
| **Round corners** | ⌐ | When enabled, the output PNG has its four corners masked transparent (cosmetic, not a crop). Prompt mode only. |

### Shift+Click — Multi-Drop Mode

Holding **Shift** when clicking the **Arrow** or **Rectangle** button enters **multi-drop mode**:

- Drag to place one annotation, then drag again to place another — without re-clicking the button each time.
- The active-tool indicator bar becomes slightly wider and rounded to signal multi-drop is active.
- Press **ESC** to exit multi-drop mode.

### Crop Region

Drag anywhere on the canvas (with no tool active) to draw a crop rectangle. The dashed overlay shows the region that will be included in the final image. Handles on the edges and corners let you resize the crop after drawing.

Press **Enter** (or click **Attach Image / Insert Image**) to finalise the crop and close the editor.

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **Ctrl+Z** | Undo last change |
| **Delete** | Remove the selected annotation arrow |
| **Space** (hold) | Switch to pan mode — drag to scroll the canvas |
| **ESC** | Exit the current tool mode; if no mode is active, prompts to discard and close |
| **Enter** | Attach/insert the image (equivalent to the toolbar button) |
| **Ctrl+0** | Reset canvas zoom to 100 % |
