# 📋 Inbox Excerpt Click-to-Select - Testing Guide

## What Was Fixed

### 1. ✅ Timing Fix
- **Before:** Used DispatcherPriority.Loaded 
- **After:** Changed to DispatcherPriority.ApplicationIdle
- **Why:** ApplicationIdle runs later in the dispatcher queue, ensuring the FlowDocument has fully completed layout and rendering before we try to select text

### 2. ✅ Focus Fix
- **Added:** _bodyViewer.Focus() call before scrolling
- **Why:** Without focus, the selection may be set internally but not visually highlighted to the user

### 3. ✅ Scrolling Fix
- **Before:** Only called BringIntoView() on the paragraph
- **After:** Dual approach - both paragraph AND character rect
- **Why:** Different WPF versions/scenarios may need different scroll triggers

### 4. ✅ Diagnostic Logging
- **Location:** %TEMP%\squaddash-excerpt-debug.log
- **Contents:** Document text, excerpt being searched, match status, selection state
- **Why:** Allows us to see exactly what's happening when the feature is used

## How to Test

### Setup
1. Build is already complete (Release configuration)
2. Executable is at: SquadDash\bin\Release\net10.0-windows\SquadDash.exe
3. Log file will be created at: %TEMP%\squaddash-excerpt-debug.log

### Test Steps

#### Test 1: Basic Excerpt Selection
1. Launch SquadDash
2. Open any inbox message that has text in the body
3. Select a few words (e.g., "This is a test")
4. Right-click → "Follow up on this..." or use the "Attach selected text to chat" option
5. An attachment chip should appear with the excerpt
6. **Click the excerpt attachment chip**
7. **Expected results:**
   - ✅ Inbox message window opens (or focuses if already open)
   - ✅ The exact text you selected is highlighted/selected in the message body
   - ✅ The window scrolls to show the selected text

#### Test 2: Excerpt from Long Message
1. Find or create an inbox message with a long body (multiple paragraphs/screens)
2. Scroll down and select text near the bottom
3. Create an excerpt attachment
4. **Click the excerpt attachment**
5. **Expected results:**
   - ✅ Window opens/focuses
   - ✅ Window automatically scrolls down to show the excerpt
   - ✅ Text is highlighted

#### Test 3: Check the Diagnostic Log
After performing tests 1-2, check the log file:
`
notepad %TEMP%\squaddash-excerpt-debug.log
`

**What to look for:**
- \[SelectAndScroll] Called with text: '...'\ - Shows the excerpt being searched
- \Document text length: ...\ - Shows the document loaded
- \Excerpt exists in doc: True\ - Confirms text was found (should be True)
- \Selection set...\ - Confirms selection was created
- \Scrolling complete\ - Confirms scrolling was attempted

**If it fails:**
- Check if "Excerpt exists in doc: False" - means text mismatch (this is the real bug if it happens)
- Check if log stops before "Selection set" - means text search failed
- Check for any exceptions in the log

## Troubleshooting

### If Text Is Not Selected
**Check log for:** \Excerpt exists in doc: False\
**Likely cause:** The text in the FlowDocument doesn't match the excerpt text character-for-character
**Solution:** May need to normalize whitespace or use fuzzy matching

### If Text Is Selected But Not Visible
**Check log for:** Selection shows as set but not visible in UI
**Likely cause:** Focus issue or scrolling didn't work
**Already fixed by:** Added Focus() call and dual scrolling approach

### If Window Doesn't Open
**Check:** Is this actually an inbox-excerpt attachment?
**Verify:** Look for \[Excerpt] Opening inbox message\ in log

## Success Criteria

Feature is WORKING when:
1. ✅ Clicking excerpt attachment opens the message window
2. ✅ The exact excerpt text is highlighted/selected (blue background)
3. ✅ The window scrolls to show the selection
4. ✅ This works for excerpts at any position (top, middle, bottom of message)

Feature is BROKEN if:
- ❌ Window opens but nothing is selected
- ❌ Wrong text is selected
- ❌ Selection is set but window doesn't scroll to it
- ❌ Selection is invisible (not highlighted)

## Next Steps After Testing

Please test and report:
1. Does the basic test (Test 1) work? ✅ or ❌
2. Does scrolling work for long messages (Test 2)? ✅ or ❌
3. What does the log file show? (paste relevant excerpts)
4. If it still doesn't work, what specific behavior are you seeing?

Then we can either:
- **If working:** Remove debug logging and finalize
- **If not working:** Analyze the log output to identify the root cause
