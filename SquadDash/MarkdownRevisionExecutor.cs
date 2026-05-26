using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SquadDash;

/// <summary>
/// Shared executor for the "Direct Revise with AI" (Quick Cleanup / Ctrl+Shift+C) flow
/// across all markdown editor surfaces. Both <see cref="MainWindow"/> and
/// <see cref="MarkdownDocumentWindow"/> delegate to this class so the async task,
/// adorner lifecycle, revision-lock management, and result application live in one place.
/// </summary>
internal static class MarkdownRevisionExecutor
{
    /// <summary>
    /// Runs an AI revision on the current selection of <paramref name="textBox"/> using
    /// <paramref name="instructions"/>, bypassing the instruction popup. Shows the working
    /// overlay and highlight adorner while the call is in flight, then applies the result
    /// (or falls back to <see cref="RevisionResultWindow"/> if the selection was edited
    /// in the meantime). Optionally acquires a revision lock on the selected range so that
    /// keystrokes targeting the locked region are swallowed while the AI is working.
    /// </summary>
    /// <param name="textBox">The editor to revise.</param>
    /// <param name="filePath">Full path of the document, or empty string when not file-backed.</param>
    /// <param name="instructions">Revision prompt to pass to the AI.</param>
    /// <param name="reviseCallback">AI call delegate — same signature as <c>RunDocRevisionAsync</c>.</param>
    /// <param name="owner">The host window (used for overlay anchoring and RevisionResultWindow ownership).</param>
    /// <param name="doc">
    ///   Optional tab state — when supplied the selected range is locked for the duration of the
    ///   AI call so that keyboard input targeting that range is suppressed.
    /// </param>
    /// <param name="onCompleted">Optional action fired on the UI thread once the revision
    ///   finishes (whether it succeeded, returned empty, or threw).</param>
    public static void DirectRevise(
        RichTextBox textBox,
        string filePath,
        string instructions,
        Func<string, string, string, string, CancellationToken, Task<string>> reviseCallback,
        Window owner,
        MarkdownDocumentTabState? doc = null,
        Action? onCompleted = null)
    {
        var selStart = textBox.GetSelectionStart();
        var selLen   = textBox.GetSelectionLength();
        if (selLen <= 0) return;

        var originalText = textBox.GetSubstring(selStart, selLen);
        var fullText     = textBox.GetPlainText();
        var startPointer = textBox.GetTextPointerAt(selStart);
        var endPointer   = textBox.GetTextPointerAt(selStart + selLen);

        var adorner   = RevisionHighlightAdorner.Attach(textBox, startPointer, endPointer);
        var indicator = RevisionPendingIndicator.Attach(textBox, endPointer);
        var revLock   = doc?.AddRevisionLock(startPointer, endPointer);

        var center = new Point(owner.Left + owner.Width / 2, owner.Top + owner.Height / 2);
        RevisionWorkingOverlay.ShowAt(center, owner);

        var cwd = string.IsNullOrEmpty(filePath)
            ? string.Empty
            : Path.GetDirectoryName(filePath) ?? string.Empty;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        _ = Task.Run(async () =>
        {
            string? revised = null;
            try
            {
                revised = await reviseCallback(instructions, originalText, fullText, cwd, cts.Token);
                if (!string.IsNullOrWhiteSpace(revised))
                {
                    owner.Dispatcher.Invoke(() =>
                    {
                        // TextRange.Text may contain \r\n; normalise to \n so the comparison
                        // against originalText (which comes from GetSubstring / GetPlainText
                        // and is always \n-only) is reliable.
                        var currentText = new TextRange(startPointer, endPointer).Text
                            .Replace("\r\n", "\n");

                        if (currentText == originalText)
                        {
                            new TextRange(startPointer, endPointer).Text = revised;
                        }
                        else
                        {
                            var win = new RevisionResultWindow(revised) { Owner = owner };
                            WindowPlacementHelper.CenterOnOwnerAndEnsureOnScreen(win, owner);
                            win.Show();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                SquadDashTrace.Write("Revision", $"Revision failed: {ex}");
                owner.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Revision failed: {ex.Message}", "Revision Error",
                        MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                cts.Dispose();
                owner.Dispatcher.Invoke(() =>
                {
                    adorner?.Remove();
                    indicator?.Detach();
                    if (revLock is not null) doc?.RemoveRevisionLock(revLock);
                    onCompleted?.Invoke();
                });
            }
        });
    }
}
