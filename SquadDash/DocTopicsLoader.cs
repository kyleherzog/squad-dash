using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SquadDash;

internal static class DocTopicsLoader
{
    private static readonly Regex SummaryLineRegex = new(@"^\s*\*\s+\[([^\]]+)\]\(([^)]+)\)\s*$", RegexOptions.Compiled);

    public static void LoadTopics(TreeView treeView, out TreeViewItem? firstItemToSelect, string? workspaceFolder = null, DocStatusStore? statusStore = null)
    {
        firstItemToSelect = null;
        if (treeView is null)
            return;

        treeView.Items.Clear();

        var docsRoot = FindDocsFolder(workspaceFolder);
        if (string.IsNullOrEmpty(docsRoot) || !Directory.Exists(docsRoot))
        {
            var errorItem = new TreeViewItem { Header = "No docs/ folder in workspace" };
            treeView.Items.Add(errorItem);
            return;
        }

        var summaryPath = Path.Combine(docsRoot, "SUMMARY.md");
        if (File.Exists(summaryPath))
        {
            firstItemToSelect = LoadFromSummary(treeView, summaryPath, docsRoot, statusStore);
        }
        else
        {
            firstItemToSelect = LoadFromFolderScan(treeView, docsRoot, statusStore);
        }
    }

    internal static string? FindDocsFolderPath(string? workspaceFolder = null)
    {
        if (!string.IsNullOrEmpty(workspaceFolder))
        {
            var docsInWorkspace = Path.Combine(workspaceFolder, "docs");
            if (Directory.Exists(docsInWorkspace))
                return docsInWorkspace;
            return null;
        }

        var current = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 5; i++)
        {
            if (string.IsNullOrEmpty(current))
                break;

            var gitPath = Path.Combine(current, ".git");
            if (Directory.Exists(gitPath))
            {
                var docsPath = Path.Combine(current, "docs");
                return Directory.Exists(docsPath) ? docsPath : null;
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }

        return null;
    }

    private static string? FindDocsFolder(string? workspaceFolder = null) => FindDocsFolderPath(workspaceFolder);

    private static TreeViewItem? LoadFromSummary(TreeView treeView, string summaryPath, string docsRoot, DocStatusStore? statusStore)
    {
        var lines = File.ReadAllLines(summaryPath);
        TreeViewItem? lastTopLevel = null;
        TreeViewItem? firstChild = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = SummaryLineRegex.Match(line);
            if (!match.Success)
                continue;

            var title = match.Groups[1].Value;
            var path = match.Groups[2].Value;
            var fullPath = Path.Combine(docsRoot, path.Replace('/', '\\'));

            var indent = line.TakeWhile(char.IsWhiteSpace).Count();
            var isTopLevel = indent < 2;

            var fileExists = File.Exists(fullPath);
            var item = new TreeViewItem
            {
                Header = fileExists ? BuildItemHeader(title, fullPath, statusStore) : (object)title,
                Tag = fileExists ? fullPath : null
            };

            if (isTopLevel)
            {
                treeView.Items.Add(item);
                lastTopLevel = item;
            }
            else if (lastTopLevel != null)
            {
                lastTopLevel.Items.Add(item);
                if (firstChild == null && item.Tag != null)
                    firstChild = item;
            }
        }

        if (treeView.Items.Count > 0 && treeView.Items[0] is TreeViewItem first)
        {
            first.IsExpanded = true;
        }

        return firstChild;
    }

    private static TreeViewItem? LoadFromFolderScan(TreeView treeView, string docsRoot, DocStatusStore? statusStore)
    {
        TreeViewItem? firstChild = null;

        var subdirs = Directory.GetDirectories(docsRoot)
            .Select(d => new DirectoryInfo(d))
            .Where(d => !d.Name.Equals("images", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name);

        foreach (var dir in subdirs)
        {
            var parentItem = new TreeViewItem
            {
                Header = TitleCase(dir.Name),
                IsExpanded = false
            };

            var mdFiles = dir.GetFiles("*.md")
                .Where(f => !f.Name.Equals("SUMMARY.md", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Name);

            foreach (var file in mdFiles)
            {
                var title = ExtractMarkdownTitle(file.FullName) ?? Path.GetFileNameWithoutExtension(file.Name);
                var childItem = new TreeViewItem
                {
                    Header = BuildItemHeader(title, file.FullName, statusStore),
                    Tag = file.FullName
                };
                parentItem.Items.Add(childItem);

                if (firstChild == null)
                    firstChild = childItem;
            }

            if (parentItem.Items.Count > 0)
                treeView.Items.Add(parentItem);
        }

        if (treeView.Items.Count > 0 && treeView.Items[0] is TreeViewItem first)
        {
            first.IsExpanded = true;
        }

        return firstChild;
    }

    private static FrameworkElement BuildItemHeader(string title, string filePath, DocStatusStore? statusStore)
        => BuildItemHeaderCore(title, filePath, statusStore);

    internal static FrameworkElement BuildItemHeaderPublic(string title, string filePath, DocStatusStore? statusStore)
        => BuildItemHeaderCore(title, filePath, statusStore);

    private static FrameworkElement BuildItemHeaderCore(string title, string filePath, DocStatusStore? statusStore)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        // Dim row and show eye-slash icon when nav_exclude: true is set in front matter
        var isNavExcluded = IsNavExcluded(filePath);
        if (isNavExcluded)
        {
            panel.Children.Add(MakeEyeSlashIcon());
            panel.Opacity = 0.55;
        }

        var label = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(label);

        if (statusStore != null && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            var status = statusStore.GetStatus(filePath);
            var needsScreenshots = DocStatusStore.HasScreenshotPlaceholders(filePath);

            if (status == DocApprovalStatus.Approved)
            {
                // LimeGreen (#00FF00) is too low-contrast in light theme; use a darker muted green.
                panel.Children.Add(MakeStatusIcon("✓", new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), "Approved"));
            }
            else if (statusStore.HasBeenTracked(filePath))
            {
                panel.Children.Add(MakeStatusIcon("!", Brushes.Orange, "Needs review"));
            }

            if (needsScreenshots)
                panel.Children.Add(MakeStatusIcon("📷", null, "Needs screenshots", opacity: 0.45));
        }

        return panel;
    }

    internal static bool IsNavExcluded(string filePath)
    {
        try
        {
            var raw = File.ReadAllText(filePath);
            DocumentUtilities.StripDocFrontMatter(raw, out var fm);
            return DocumentUtilities.ReadNavExclude(fm);
        }
        catch
        {
            return false;
        }
    }

    private static UIElement MakeEyeSlashIcon()
    {
        var canvas = new Canvas { Width = 16, Height = 12 };

        // Eye outline: bezier approximating the lens shape
        var eyePath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 1,6 C 4,1 12,1 15,6 C 12,11 4,11 1,6 Z"),
            Fill = Brushes.Transparent,
            StrokeThickness = 1.4,
        };
        eyePath.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "LabelText");
        canvas.Children.Add(eyePath);

        // Pupil
        var pupil = new System.Windows.Shapes.Ellipse { Width = 4, Height = 4, Fill = Brushes.Transparent, StrokeThickness = 1.2 };
        pupil.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "LabelText");
        Canvas.SetLeft(pupil, 6);
        Canvas.SetTop(pupil, 4);
        canvas.Children.Add(pupil);

        // Diagonal slash across the eye
        var slash = new System.Windows.Shapes.Line
        {
            X1 = 2, Y1 = 11,
            X2 = 14, Y2 = 1,
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap   = PenLineCap.Round,
        };
        slash.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "LabelText");
        canvas.Children.Add(slash);

        return new Viewbox
        {
            Width  = 13,
            Height = 10,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 3, 0),
            Child  = canvas,
        };
    }

    private static TextBlock MakeStatusIcon(string text, Brush? foreground, string tooltip, double opacity = 1.0)
    {
        var tb = new TextBlock
        {
            Text = " " + text,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = tooltip,
            Opacity = opacity,
        };
        tb.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSmall");
        if (foreground != null)
            tb.Foreground = foreground;
        return tb;
    }

    private static string TitleCase(string input)
    {
        var words = input.Split('-', '_');
        return string.Join(" ", words.Select(w =>
            w.Length > 0 ? char.ToUpper(w[0]) + w.Substring(1).ToLower() : w));
    }

    internal static string? ExtractMarkdownTitle(string filePath)
    {
        try
        {
            var lines = File.ReadLines(filePath).Take(10);
            foreach (var line in lines)
            {
                if (line.StartsWith("# "))
                    return line.Substring(2).Trim();
            }
        }
        catch
        {
        }
        return null;
    }
}