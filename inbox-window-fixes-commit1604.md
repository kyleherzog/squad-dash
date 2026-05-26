# Inbox Window Issues - Commit 1604 Fixes

## 🔍 Investigation Results

### Critical Finding: Trace Log File Did Not Exist
- **Expected location:** `%TEMP%\squaddash-excerpt-debug.log`
- **Actual result:** File not found
- **Conclusion:** The `Dispatcher.BeginInvoke` callback in `OpenOrFocusInboxMessageAndSelectText` was **NEVER EXECUTING**

## ❌ Issue 1: Window Size Still Wrong

### Root Cause
The constructor correctly sets `Width = 750` and `Height = 550` (InboxMessageWindow.cs lines 38-39).

**Problem:** No window size persistence exists in ApplicationSettingsStore for InboxMessageWindow. The real issue may be:
1. Something overriding the size after construction
2. The window not rendering at the specified size due to layout issues

### Fix Applied
Added diagnostic logging to track the window size lifecycle:
- **Constructor logging:** Records Width/Height when window is created
- **Loaded event logging:** Records ActualWidth/ActualHeight when window is fully loaded

Log file: `%TEMP%\squaddash-window-debug.log`

This will help identify where/when the size is being changed.

## ❌ Issue 2: Excerpt Selection Not Working

### Root Cause
In `MainWindow.xaml.cs`, the `OpenOrFocusInboxMessageAndSelectText` method used:

```csharp
win.Show();
Dispatcher.BeginInvoke(() => {
    win.SelectAndScrollToText(excerptText);
}, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
```

**Problem:** `ApplicationIdle` priority may NEVER execute if:
- The application is busy processing other events
- The window closes before the callback fires
- The dispatcher queue is constantly active

**Evidence:** The trace log file that should have been created by `SelectAndScrollToText` did not exist, proving the method was never called.

### Fix Applied
Changed the approach to use the window's `Loaded` event instead of relying on `ApplicationIdle`:

```csharp
// Defer text selection until window is fully loaded and rendered
win.Loaded += (_, _) =>
{
    Trace.WriteLine($"[Excerpt] Window Loaded event fired, scheduling selection with Dispatcher.BeginInvoke (Loaded priority)");
    
    // Use Loaded priority (higher than ApplicationIdle) and dispatch to ensure UI is ready
    Dispatcher.BeginInvoke(() =>
    {
        Trace.WriteLine($"[Excerpt] Dispatcher callback executing, calling SelectAndScrollToText");
        win.SelectAndScrollToText(excerptText);
    }, System.Windows.Threading.DispatcherPriority.Loaded);
};

win.Show();
```

**Key changes:**
1. Moved callback attachment to BEFORE `win.Show()` so the Loaded event is guaranteed to be subscribed
2. Changed from `ApplicationIdle` to `Loaded` priority (much more reliable)
3. Chained off the window's `Loaded` event to ensure the FlowDocument is fully rendered

## 🧪 Testing Plan

1. **Build Debug:** Rebuild SquadDash in Debug mode
2. **Test Window Size:**
   - Open an inbox message
   - Check actual window dimensions
   - Read `%TEMP%\squaddash-window-debug.log` to see constructor vs loaded sizes
3. **Test Excerpt Selection:**
   - Create an inbox message with body text
   - Use "Add to Chat" to create an excerpt attachment
   - Click the excerpt attachment in the follow-up strip
   - Verify:
     - Window opens
     - Text is selected (highlighted)
     - Text is scrolled into view
   - Read `%TEMP%\squaddash-excerpt-debug.log` to see full trace

## 📝 Files Modified

1. **InboxMessageWindow.cs**
   - Added constructor logging for window size
   - Added Loaded event logging for actual rendered size

2. **MainWindow.xaml.cs**
   - Changed `OpenOrFocusInboxMessageAndSelectText` to use Loaded event
   - Changed dispatcher priority from ApplicationIdle to Loaded
   - Moved event subscription before Show()

## ⚠️ Next Steps if Issues Persist

### If window size is still wrong after these fixes:
1. Check the log file to see actual sizes
2. Look for WPF layout/measure/arrange issues
3. Check if ResizeMode or other properties are interfering

### If excerpt selection still doesn't work:
1. The log file will now definitively show whether the method is being called
2. If called but not working, the issue is in the text search/selection logic
3. The extensive logging in SelectAndScrollToText will pinpoint the exact failure point
