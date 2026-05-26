# Attach to Chat from Inbox Message Selection

## Feature Overview

Users can now select text within an inbox message and attach it to the current conversation with full message context.

## Usage

1. **Open an inbox message** — Click on any message in the Inbox panel or open it from the viewer
2. **Select text** — Highlight any portion of the message body
3. **Right-click** — A context menu will appear with "Attach to Chat"
4. **Click "Attach to Chat"** — The selected text is added to your conversation attachments

## What Gets Attached

When you attach selected text, the AI receives:

- **Selected excerpt** — The exact text you highlighted
- **Message metadata:**
  - Sender (`From`)
  - Subject line
  - Date/time sent
- **Clear context** — The attachment is labeled as an excerpt from a specific inbox message

## Attachment Format

The attachment appears in the conversation as:

```
📎 Excerpt: [Message Subject]
```

When the AI processes it, it sees:

```xml
<attachment type="inbox-excerpt" title="Excerpt from: [Message Subject]">
From: [Sender]
Subject: [Message Subject]
Date: [Timestamp]

Selected excerpt:
---
[Your selected text]
</attachment>
```

## Benefits

- **Precision** — Discuss specific parts of a message without attaching the entire content
- **Context preservation** — The AI knows where the excerpt came from
- **Efficiency** — Quickly reference relevant portions of longer messages

## Comparison: Full Message vs. Excerpt

| Feature | Attach Full Message | Attach Selected Excerpt |
|---------|-------------------|------------------------|
| Trigger | "Add to Chat" button in viewer | Right-click on selected text |
| Content | Full message body | Only selected text |
| Use case | Need entire message context | Focus on specific detail |
| Attachment type | `inbox-message` | `inbox-excerpt` |

## Example Workflow

1. Receive a maintenance report in Inbox from an agent
2. The report contains 10 findings
3. You want to discuss finding #7 specifically
4. Select the text for finding #7
5. Right-click → "Attach to Chat"
6. Ask: "What's the best way to address this?"

The AI sees only finding #7 with clear context about which message it came from.

## Technical Details

- Uses the same `FollowUpAttachment` system as other context items
- Supports special characters and XML escaping
- Attachment is persisted with the conversation
- Links back to the original inbox message ID
