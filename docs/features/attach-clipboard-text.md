---
title: Attaching Clipboard Text
nav_order: 2
parent: Prompts
---

# Attaching Clipboard Text

When you have a large block of text — a build log, an error trace, a chunk of code — you often want the agent to see it without it cluttering your transcript. **Ctrl+Shift+V** attaches whatever text is on your clipboard as a compact follow-up attachment rather than pasting it inline. The agent receives the full content, but your message stays clean.

---

## How to Use It

1. Copy the text you want to attach (e.g. select a build error in your terminal and press **Ctrl+C**).
2. Click into the SquadDash prompt text box and type your prompt as usual.
3. Press **Ctrl+Shift+V** — a **📎 Clipboard text** pill appears in the attachment strip below the prompt box.
4. Press **Enter** (or click **Send**) to submit. The agent receives both your typed prompt and the attached text.

![Screenshot: Clipboard text attachment pill in the prompt area](images/attach-clipboard-text-pill.png)
> 📸 *Screenshot needed: The prompt area with a "📎 Clipboard text" pill visible in the attachment strip below the prompt text box.*

---

## When to Use This vs Ctrl+V

| | **Ctrl+V** (standard paste) | **Ctrl+Shift+V** (attach) |
|---|---|---|
| Where the text appears | Inline in the prompt text box | As a pill in the attachment strip |
| Visible in transcript | Yes — shown as part of your message | No — sent as a follow-up attachment |
| Best for | Short snippets you want to reference directly | Long logs, error output, or code blocks |
| Works with images | No — use Ctrl+V for clipboard images | N/A — text only |

---

## Notes

- The clipboard must contain **text**. To attach an image from the clipboard, use **Ctrl+V** instead (see [Paste Image](paste-image.md)).
- Attaching the same clipboard text twice replaces the earlier attachment — no duplicates.
- A maximum of **15 follow-up attachments** per prompt is allowed.
- Works on both the Active Draft and on queued tabs.

---

## Related

- **[Entering Prompts](entering-prompts.md)** — Full overview of the prompt input area
- **[Paste Image](paste-image.md)** — Attach a screenshot or image via the clipboard
- **[Keyboard Shortcuts](../reference/keyboard-shortcuts.md)** — All prompt-box shortcuts at a glance
