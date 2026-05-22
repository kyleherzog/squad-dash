using System.Collections.Generic;
using System.Windows;

namespace SquadDash;

internal sealed record MarkdownDocumentSpec(string TabTitle, string FilePath);

// Stub: full implementation lives in MarkdownDocumentWindow.cs which is excluded from the test project.
internal sealed class MarkdownDocumentWindow
{
    public static void Show(Window? owner, string title, string filePath,
        bool showSource = false) { }

    public static void Show(Window? owner, string title,
        IReadOnlyList<MarkdownDocumentSpec> documents, bool showSource = false) { }

    public static void ShowContent(Window? owner, string title, string content) { }
}
