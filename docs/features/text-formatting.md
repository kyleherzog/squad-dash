---
title: Text Formatting Shortcuts
nav_order: 7
parent: Features
---

# Text Formatting Shortcuts

Every text editor in SquadDash — the **prompt text box**, the **doc source editor**, and any **markdown document window** — supports *selection embedding*: select text, press a key, and SquadDash wraps the selection in markdown formatting automatically.

---

## Backtick — Inline Code and Code Fences

Press `` ` `` (backtick) with text selected to wrap it as code.

### Single-line selection → inline code

Select a word or phrase on a single line, then press `` ` ``.

| You select | Result |
|---|---|
| `myVariable` | `` `myVariable` `` |
| `great ` *(trailing space)* | `` `great` `` *(space moves outside the backticks)* |
| `my method name` *(plain words)* | `` `myMethodName` `` *(converted to camelCase)* |

**Leading and trailing spaces** in your selection are moved *outside* the backtick markers; only the trimmed content goes inside.

**Multi-word selections** that contain no code-like characters (punctuation, digits, existing camelCase) are automatically converted to **camelCase**. This is handy when you've typed a method name as natural language and want to quote it as an identifier.

Before:
```
…call the get user profile endpoint…
```

After selecting `get user profile` and pressing `` ` ``:
```
…call the `getUserProfile` endpoint…
```

![Screenshot: Single-line inline code wrapping in the prompt box](images/text-formatting-inline-code.png)
> 📸 *Screenshot needed: The prompt text box with a multi-word phrase selected (e.g. "get user profile"), then the result showing `getUserProfile` wrapped in backticks.*

### Multi-line selection → fenced code block

Select text that spans **two or more lines**, then press `` ` ``.

Before (three lines selected):
```
const x = 1;
const y = 2;
return x + y;
```

After pressing `` ` ``:
````
```
const x = 1;
const y = 2;
return x + y;
```
````

![Screenshot: Multi-line fenced code block wrapping](images/text-formatting-code-fence.png)
> 📸 *Screenshot needed: The prompt text box (or doc source editor) with multiple lines selected, then the result with the selection wrapped in triple-backtick fences.*

---

## Double-Quote — Inline Quote

Press `"` (Shift+") with a single-line selection to wrap it in curly double-quote characters.

| You select | Result |
|---|---|
| `important term` | `"important term"` |
| ` important term ` *(spaces)* | `"important term"` *(spaces move outside the quotes)* |

The same leading/trailing-space rule applies: spaces are moved outside the quote markers, not stripped.

> **Note:** Double-quote wrapping only applies to single-line selections. Multi-line selections are left unchanged when you press `"`.

![Screenshot: Inline quote wrapping in the prompt box](images/text-formatting-inline-quote.png)
> 📸 *Screenshot needed: The prompt text box with a short phrase selected, then the result with the phrase wrapped in double quotes.*

---

## Where These Shortcuts Work

| Editor | Backtick wrapping | Quote wrapping |
|---|---|---|
| Prompt text box | ✅ | ✅ |
| Doc source editor (markdown source panel) | ✅ | ✅ |
| Markdown document windows | ✅ | ✅ |

---

## Selection After Wrapping

After SquadDash wraps your selection, the new selection covers the **entire wrapped span** — including the markers themselves (e.g., `` `word` `` or `"phrase"`). This means you can immediately:

- Press **Delete** or start typing to replace the wrapped text entirely.
- Press **→** to deselect and position the cursor right after the closing marker.
- Apply another shortcut to re-wrap (e.g., undo and try a different format).

---

## See Also

- **[Entering Prompts](entering-prompts.md)** — All prompt-box features, including multi-line input and slash commands
- **[Keyboard Shortcuts](../reference/keyboard-shortcuts.md)** — Full shortcut reference
- **[Documentation Panel](../concepts/documentation-panel.md)** — Using the doc source editor
