# Excerpt Attachment Diagnostic Test Guide

## ✅ Fixes Implemented

All three diagnostic fixes have been successfully implemented in `MainWindow.xaml.cs`:

### Fix 1: Replace Trace.WriteLine with File Logging ✅
- **Lines 27607-27620**: Replaced `Trace.WriteLine()` with `File.AppendAllText()` for attachment building diagnostics
- **Lines 27649-27662**: Replaced `Trace.WriteLine()` with `File.AppendAllText()` for click handler diagnostics
- **Lines 27307-27321**: Added logging to `BuildTypedAttachmentBlock()` wrapper

All logging now writes to: `%TEMP%\squaddash-excerpt-debug.log`

### Fix 2: Comprehensive Diagnostic Logging ✅
The following information is now logged:

**During attachment building (lines 27607-27620):**
```
[HH:mm:ss.fff] [EXCERPT ATTACH] isInboxExcerpt={true/false}, InboxMessageId={GUID or NULL}, hasContentBlock={true/false}
[HH:mm:ss.fff] [EXCERPT ATTACH] ContentBlock preview: {first 200 chars}
[HH:mm:ss.fff] [EXCERPT ATTACH] ✅ Registering click handler for inbox-excerpt
```

**During BuildTypedAttachmentBlock (lines 27307-27321):**
```
[HH:mm:ss.fff] [BUILD ATTACHMENT] Created inbox-excerpt block:
{first 300 chars of the generated attachment block}...
```

**During click (lines 27654-27660):**
```
[HH:mm:ss.fff] [EXCERPT CLICK] 🖱️ Attachment clicked! InboxMessageId={GUID}
[HH:mm:ss.fff] [EXCERPT CLICK] Extracted text: '{extracted excerpt text}'
```

### Fix 3: BuildTypedAttachmentBlock Verification ✅
Added logging to verify the attachment block format includes:
- The `type="inbox-excerpt"` attribute
- The `title` attribute with the message subject
- The content structure

---

## 🧪 Test Procedure

### Step 1: Start SquadDash in Debug Mode
1. Launch `SquadDash.exe` from `SquadDash\bin\Debug\net10.0-windows\`
2. Or press F5 in Visual Studio to run in debug mode

### Step 2: Create an Inbox Excerpt Attachment
1. Open the **Inbox Messages** window (via the menu or hotkey)
2. Open any message
3. **Select some text** in the message body
4. Right-click and choose **"Add Excerpt to Conversation"** (or use the context menu)

**Expected log entries at this point:**
```
[HH:mm:ss.fff] [BUILD ATTACHMENT] Created inbox-excerpt block:
<attachment type="inbox-excerpt" title="Excerpt from: {Subject}">
{message metadata}
---
{selected text}
</attachment>...

[HH:mm:ss.fff] [EXCERPT ATTACH] isInboxExcerpt=True, InboxMessageId={GUID}, hasContentBlock=True
[HH:mm:ss.fff] [EXCERPT ATTACH] ContentBlock preview: <attachment type="inbox-excerpt"...
[HH:mm:ss.fff] [EXCERPT ATTACH] ✅ Registering click handler for inbox-excerpt
```

### Step 3: Click the Excerpt Attachment
1. In the main chat window, you should see the excerpt attachment in the attachments area
2. **Click on the excerpt attachment**

**Expected log entries:**
```
[HH:mm:ss.fff] [EXCERPT CLICK] 🖱️ Attachment clicked! InboxMessageId={GUID}
[HH:mm:ss.fff] [EXCERPT CLICK] Extracted text: '{the excerpt text you selected}'
```

**Expected behavior:**
- The inbox message window should open (or focus if already open)
- The selected text should be highlighted in the message body
- You should be scrolled to the highlighted text

### Step 4: Read the Debug Log
Open PowerShell and run:
```powershell
Get-Content "$env:TEMP\squaddash-excerpt-debug.log"
```

Or use this command to watch the log in real-time:
```powershell
Get-Content "$env:TEMP\squaddash-excerpt-debug.log" -Wait -Tail 20
```

---

## 🔍 What the Log Will Tell Us

The diagnostic log will reveal **exactly where the issue is** if the click handler isn't working:

### Scenario 1: BuildTypedAttachmentBlock Not Creating Correct Format
**Symptom:** No `[BUILD ATTACHMENT]` log entry, or the format is wrong
**Indicates:** The attachment block isn't being created with `type="inbox-excerpt"`

### Scenario 2: isInboxExcerpt is False
**Symptom:** `isInboxExcerpt=False` in the log
**Root causes:**
- `ContentBlock` doesn't contain `type="inbox-excerpt"` (format issue)
- `InboxMessageId` is `NULL` (not being passed correctly)

### Scenario 3: Click Handler Not Registered
**Symptom:** `[EXCERPT ATTACH] ✅ Registering click handler` is NOT in the log
**Indicates:** The `isInboxExcerpt` check is failing (see Scenario 2)

### Scenario 4: Click Handler Not Firing
**Symptom:** No `[EXCERPT CLICK]` log entries when you click
**Indicates:** Event handler was never registered (see Scenario 3)

### Scenario 5: Excerpt Text Not Extracted
**Symptom:** `[EXCERPT CLICK] Extracted text: ''` (empty string)
**Indicates:** `ExtractExcerptTextFromAttachment()` is failing to parse the content

---

## 📋 Expected Complete Log Sequence

When everything works correctly, you should see:

```
[10:23:45.123] [BUILD ATTACHMENT] Created inbox-excerpt block:
<attachment type="inbox-excerpt" title="Excerpt from: Test Message Subject">
Message: Test Message Subject
From: sender@example.com
Date: 2026-05-11 10:20:00
InboxMessageId: 12345678-1234-1234-1234-123456789abc
---
This is the selected text from the inbox message that I want to discuss.
</attachment>...

[10:23:45.234] [EXCERPT ATTACH] isInboxExcerpt=True, InboxMessageId=12345678-1234-1234-1234-123456789abc, hasContentBlock=True
[10:23:45.235] [EXCERPT ATTACH] ContentBlock preview: <attachment type="inbox-excerpt" title="Excerpt from: Test Message Subject">
Message: Test Message Subject
From: sender@example.com
Date: 2026-05-11 10:20:00...
[10:23:45.236] [EXCERPT ATTACH] ✅ Registering click handler for inbox-excerpt

[10:24:10.456] [EXCERPT CLICK] 🖱️ Attachment clicked! InboxMessageId=12345678-1234-1234-1234-123456789abc
[10:24:10.457] [EXCERPT CLICK] Extracted text: 'This is the selected text from the inbox message that I want to discuss.'
```

---

## 🎯 Next Steps After Testing

1. **Run the test** following the procedure above
2. **Capture the log output** from `%TEMP%\squaddash-excerpt-debug.log`
3. **Report findings** with the complete log sequence
4. **Identify the exact failure point** based on which log entries appear vs. which are missing

The diagnostic logging will give us **100% visibility** into the attachment lifecycle from creation → display → click → excerpt extraction.
