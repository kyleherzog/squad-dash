# VoiceHeuristics

[![NuGet](https://img.shields.io/nuget/v/VoiceHeuristics)](https://www.nuget.org/packages/VoiceHeuristics)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Context-aware adjustments for speech-to-text text before it is inserted at the cursor. Handles filler-word stripping, sentence continuation, capitalisation, punctuation correction, and spacing — so dictated text reads naturally no matter where the caret is.

## Installation

```
dotnet add package VoiceHeuristics
```

## Quick start

**No selection (caret only)**

```csharp
using VoiceHeuristics;

string leftContext  = text[..caretIndex];   // everything left of the caret
string rightContext = text[caretIndex..];   // everything right of the caret

string adjusted = VoiceInsertionHeuristics.Apply(leftContext, rawSpeechText, rightContext);
// Replace the caret position with `adjusted`
```

**With a selection (voice replaces the selected text)**

```csharp
using VoiceHeuristics;

// selStart / selEnd are the selection start and end indices
string leftContext  = text[..selStart];   // text before the selection
string rightContext = text[selEnd..];     // text after the selection
// The selected text is discarded — voice dictation replaces it entirely

string adjusted = VoiceInsertionHeuristics.Apply(leftContext, rawSpeechText, rightContext);
// Delete the selection, then insert `adjusted` at selStart
```

## What it does

`VoiceInsertionHeuristics.Apply()` runs the following heuristics in order:

| # | Heuristic | Example |
|---|-----------|---------|
| 0 | **Filler-word stripping** — removes "uh", "uhh", "um", "umm" (any case) from the start, middle, and end of the text. Filler-only remnants ("Yeah.", "Yep.") are discarded entirely. | `"Uh, do this"` → `"Do this"` |
| 1 | **Sentence continuation** — if the left context ends with a lowercase letter, comma, open paren, semicolon, or dash, the first word of the incoming text is lowercased (unless it's a special token). | `"add a "` + `"New item."` → `"add a new item."` |
| 2 | **Mid-sentence insertion** — if the right context starts with a lowercase letter or `)`, trailing periods are stripped to avoid broken punctuation. | inserting before `" years ago"` → `"Four score and twenty"` (no period) |
| 2b | **Punctuation double-up prevention** — if the right context starts with `.`, `,`, `;`, `!`, `?`, or `:`, the inserted text's trailing sentence punctuation is removed. | inserting before `", which"` → `"hello"` not `"hello."` |
| 3 | **Trailing punctuation correction** — specific high-precision rewrites (e.g. text ending with `"this"` gets a colon appended). | `"it looks like this."` → `"it looks like this: "` |
| 4 | **Trailing space** — when the right context starts with a non-whitespace character (other than `)` or sentence punctuation), a space is appended so the inserted text doesn't run into the next word. | inserting before `"years"` → `"twenty "` |
| 5 | **Post-digit capitalisation** — if the left context ends with a digit, the first word of the incoming text is lowercased and a separating space is prepended if needed. | `"step 6"` + `"And then"` → `" and then"` |

### Special-case tokens (never lowercased)

- The pronoun **`I`** and its contractions (`I'm`, `I've`, `I'll`, `I'd`, …)
- Words containing **two or more uppercase letters** (acronyms: `API`, `URL`; CamelCase: `JavaScript`, `iPhone`)
- Words containing a **digit** (`3D`, `v2`, `R2D2`)

## API reference

### `VoiceInsertionHeuristics.Apply`

```csharp
public static string Apply(
    string leftContext,
    string incomingText,
    string rightContext = "")
```

| Parameter | Description |
|-----------|-------------|
| `leftContext` | Text to the **left** of the caret or selection start. No selection: `text[..caretIndex]`. With selection: `text[..selStart]`. |
| `incomingText` | Raw text from the speech recogniser. When a selection is active it replaces the selected text entirely. |
| `rightContext` | Text to the **right** of the caret or selection end. No selection: `text[caretIndex..]`. With selection: `text[selEnd..]`. Omit or pass `""` when appending at the end. |

**Returns** the adjusted string to insert.

### Individual helper methods (all `public static`)

These are exposed so callers can unit-test or compose heuristics independently.

| Method | Purpose |
|--------|---------|
| `StripFillerWords(text)` | Removes uh/um filler words. |
| `IsSentenceContinuation(leftContext)` | `true` when left context signals a continuing sentence. |
| `LowercaseFirstWordIfNotSpecial(text)` | Lowercases first word unless it's a special token. |
| `CapitalizeFirstWordIfNotSpecial(text)` | Uppercases first word unless it's a special token. |
| `IsSpecialCaseWord(word)` | `true` for `I`, `I'*` contractions, acronyms, CamelCase, digit-containing words. |
| `ApplyTrailingPunctuationFixes(text)` | Conservative trailing-punctuation rewrites. |
| `IsRightContextMidSentence(rightContext)` | `true` when right context starts with a lowercase letter or `)`. |
| `IsRightContextStartsWithPunctuation(rightContext)` | `true` when right context starts with `. , ; ! ? :`. |
| `IsRightContextRequiresTrailingSpace(rightContext)` | `true` when a trailing space should be appended. |
| `StripTrailingPeriods(text)` | Removes one trailing `.`. |
| `StripTrailingSentencePunctuation(text)` | Removes one trailing `. ! ?`. |
| `IsLeftContextEndsWithDigit(leftContext)` | `true` when left context ends with a digit. |

## Requirements

- .NET 10.0 or later

## License

MIT — see [LICENSE](../LICENSE).
