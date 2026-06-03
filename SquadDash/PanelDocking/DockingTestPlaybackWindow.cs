#nullable enable

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SquadDash.PanelDocking;

/// <summary>
/// Developer tool window for stepping through recorded docking test cases interactively.
/// Phases per test: (auto) Apply Layout + Open Map → Execute Move → Next Test.
/// </summary>
internal sealed class DockingTestPlaybackWindow : ChromedWindow
{
    private enum PlaybackPhase { MapOpen, MoveExecuted }

    private sealed record TestCaseEntry(string FilePath, string DisplayName);

    private sealed record ParsedTestCase(
        string SourcePanelId,
        Dictionary<string, List<string>> InitialLayout,
        Dictionary<string, List<string>> ExpectedLayout,
        string TargetZoneDisplay,
        int TargetOrder,
        bool TargetIsInsert);

    private readonly string _testCaseFolder;
    private readonly Action<Dictionary<string, List<string>>> _applyLayout;
    private readonly Action<string, (DockZone Zone, int Order, bool IsInsert)> _openDockingMap;
    private readonly Action<string, string> _addFileToChat;
    private readonly Action<string, DockZone, int> _executeMove;
    private readonly Func<Dictionary<string, List<string>>> _getCurrentLayout;
    private readonly Action<string, DockZone, int>? _showDropPreview;
    private readonly Action? _hideDropPreview;
    private readonly Func<string, (DockZone Zone, int Order), IReadOnlyList<string>>? _getMapViolations;

    private ListBox _testList = null!;
    private TextBlock _detailBlock = null!;
    private Button _stepButton = null!;
    private TextBlock _statusLabel = null!;

    private PlaybackPhase _phase = PlaybackPhase.MapOpen;
    private bool _testLoaded = false;
    private ParsedTestCase? _currentTest;
    private readonly List<TestCaseEntry> _entries = [];

    public DockingTestPlaybackWindow(
        string testCaseFolder,
        Action<Dictionary<string, List<string>>> applyLayout,
        Action<string, (DockZone Zone, int Order, bool IsInsert)> openDockingMap,
        Action<string, string> addFileToChat,
        Action<string, DockZone, int> executeMove,
        Func<Dictionary<string, List<string>>> getCurrentLayout,
        ResourceDictionary appResources,
        Action<string, DockZone, int>? showDropPreview = null,
        Action? hideDropPreview = null,
        Func<string, (DockZone Zone, int Order), IReadOnlyList<string>>? getMapViolations = null)
        : base(captionHeight: ChromedWindow.CloseButtonHeight, resizeMode: ResizeMode.CanResize)
    {
        _testCaseFolder    = testCaseFolder;
        _applyLayout       = applyLayout;
        _openDockingMap    = openDockingMap;
        _addFileToChat     = addFileToChat;
        _executeMove       = executeMove;
        _getCurrentLayout  = getCurrentLayout;
        _showDropPreview   = showDropPreview;
        _hideDropPreview   = hideDropPreview;
        _getMapViolations  = getMapViolations;

        Title      = "Docking Test Playback";
        Width      = 720;
        Height     = 500;

        var contentHolder = ApplyOuterBorder("AppSurface", "Docking Test Playback");
        BuildUI(appResources, contentHolder);
        LoadTestCases();
    }

    private void BuildUI(ResourceDictionary appResources, Border contentHolder)
    {
        var outer = new Grid { Margin = new Thickness(4) };
        outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Main content (list + detail) ────────────────────────────────────
        var content = new Grid { Margin = new Thickness(8, 8, 8, 4) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(content, 0);
        outer.Children.Add(content);

        // Test list
        _testList = new ListBox { BorderThickness = new Thickness(1) };
        _testList.SetResourceReference(ListBox.BackgroundProperty,  "AppSurface");
        _testList.SetResourceReference(ListBox.ForegroundProperty,  "LabelText");
        _testList.SetResourceReference(ListBox.BorderBrushProperty, "PanelBorder");
        _testList.SelectionChanged += OnTestSelected;

        // Re-apply layout when clicking an already-selected item (SelectionChanged won't fire).
        // Deferred to Loaded priority so the full mouse-event cycle finishes first — otherwise
        // the ListBoxItem focus-grab after MouseLeftButtonDown deactivates the map window,
        // which auto-closes on Deactivated.
        _testList.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject src)
            {
                var item = FindAncestorOrSelf<ListBoxItem>(src);
                if (item is not null && item.IsSelected)
                    Dispatcher.InvokeAsync(() => OnTestSelected(_testList, null!),
                        System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };

        var contextMenu      = new ContextMenu();
        var addToChatItem    = new MenuItem { Header = "Add to Chat" };
        addToChatItem.Click += OnAddToChat;
        contextMenu.Items.Add(addToChatItem);
        _testList.ContextMenu = contextMenu;

        Grid.SetColumn(_testList, 0);
        content.Children.Add(_testList);

        // Detail panel
        var detailScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        detailScroll.SetResourceReference(ScrollViewer.BackgroundProperty, "AppSurface");

        _detailBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Padding      = new Thickness(4),
            FontFamily   = new FontFamily("Consolas"),
            FontSize     = 11,
        };
        _detailBlock.SetResourceReference(TextBlock.ForegroundProperty, "LabelText");

        detailScroll.Content = _detailBlock;
        Grid.SetColumn(detailScroll, 2);
        content.Children.Add(detailScroll);

        // ── Bottom bar (status + step button) ───────────────────────────────
        var bottomBar = new Grid { Margin = new Thickness(8, 4, 8, 8) };
        bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(bottomBar, 1);
        outer.Children.Add(bottomBar);

        _statusLabel = new TextBlock
        {
            Text              = "Select a test case to begin.",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping      = TextWrapping.Wrap,
        };
        _statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "SubtleText");

        var copyStatusItem = new MenuItem { Header = "Copy" };
        copyStatusItem.Click += (_, _) => System.Windows.Clipboard.SetText(_statusLabel.Text);
        var statusMenu = new ContextMenu();
        statusMenu.Items.Add(copyStatusItem);
        _statusLabel.ContextMenu = statusMenu;

        Grid.SetColumn(_statusLabel, 0);
        bottomBar.Children.Add(_statusLabel);

        _stepButton = new Button
        {
            Content   = "Step →",
            Width     = 130,
            Height    = 28,
            IsEnabled = false,
            Margin    = new Thickness(8, 0, 0, 0),
        };
        if (appResources["FlatButtonStyle"] is Style flatStyle)
            _stepButton.Style = flatStyle;
        _stepButton.Click += OnStep;
        Grid.SetColumn(_stepButton, 1);
        bottomBar.Children.Add(_stepButton);

        contentHolder.Child = outer;
    }

    private void LoadTestCases()
    {
        _entries.Clear();
        _testList.Items.Clear();

        if (!Directory.Exists(_testCaseFolder)) return;

        foreach (var file in Directory.GetFiles(_testCaseFolder, "*.json").OrderBy(f => f))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            _entries.Add(new TestCaseEntry(file, name));
            _testList.Items.Add(name);
        }
    }

    private void OnTestSelected(object sender, SelectionChangedEventArgs e)
    {
        _phase      = PlaybackPhase.MapOpen;
        _testLoaded = false;
        _currentTest = null;

        int idx = _testList.SelectedIndex;
        if (idx < 0 || idx >= _entries.Count)
        {
            _detailBlock.Text     = string.Empty;
            _stepButton.IsEnabled = false;
            SetStatus("Select a test case to begin.", StatusKind.Subtle);
            return;
        }

        var entry = _entries[idx];
        try
        {
            var json = File.ReadAllText(entry.FilePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var sourcePanelId  = root.GetProperty("sourcePanelId").GetString() ?? "";
            var initialLayout  = ParseZoneMap(root.GetProperty("initialLayout"));
            var action         = root.GetProperty("action");
            var targetZone     = action.GetProperty("targetZone").GetString() ?? "";
            var targetOrder    = action.GetProperty("targetOrder").GetInt32();
            var targetIsInsert = action.TryGetProperty("insertKind", out var ikProp)
                              && ikProp.GetString() is { Length: > 0 };
            var expectedLayout = root.TryGetProperty("expectedLayout", out var elProp)
                ? ParseZoneMap(elProp)
                : new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            _currentTest = new ParsedTestCase(sourcePanelId, initialLayout, expectedLayout, targetZone, targetOrder, targetIsInsert);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Source  : {sourcePanelId}");
            sb.AppendLine($"Action  : move → {targetZone} @ order {targetOrder}");
            sb.AppendLine();

            sb.AppendLine("Initial layout:");
            foreach (var kv in initialLayout)
                if (kv.Value.Count > 0)
                    sb.AppendLine($"  {kv.Key,-8}: {string.Join(", ", kv.Value)}");

            sb.AppendLine();
            sb.AppendLine("Expected layout:");
            foreach (var kv in expectedLayout)
                if (kv.Value.Count > 0)
                    sb.AppendLine($"  {kv.Key,-8}: {string.Join(", ", kv.Value)}");

            _detailBlock.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            _detailBlock.Text = $"Error reading test case:\n{ex.Message}";
            _stepButton.IsEnabled = false;
            SetStatus($"Error: {ex.Message}", StatusKind.Subtle);
            return;
        }

        // Apply layout and open docking map immediately on selection.
        _applyLayout(_currentTest.InitialLayout);
        var actualLayout = _getCurrentLayout();
        var (layoutOk, layoutDetails) = CompareZoneMaps(_currentTest.InitialLayout, actualLayout);

        var zone = DockingLayoutEngine.ParseZoneDisplayName(_currentTest.TargetZoneDisplay);
        _openDockingMap(_currentTest.SourcePanelId, (zone, _currentTest.TargetOrder, _currentTest.TargetIsInsert));
        _showDropPreview?.Invoke(_currentTest.SourcePanelId, zone, _currentTest.TargetOrder);

        // Check for adjacent-thin violations in the generated docking map.
        IReadOnlyList<string>? mapViolations = null;
        if (_getMapViolations is not null)
            mapViolations = _getMapViolations(_currentTest.SourcePanelId, (zone, _currentTest.TargetOrder));

        _phase      = PlaybackPhase.MapOpen;
        _testLoaded = true;

        if (mapViolations is { Count: > 0 })
        {
            var violationText = string.Join("; ", mapViolations);
            SetStatus(
                (layoutOk ? "✓ Layout OK — " : $"✗ Layout mismatch ({layoutDetails}) — ")
                + $"⚠ Map violation: {violationText}",
                StatusKind.Error);
        }
        else
        {
            SetStatus(layoutOk
                ? "✓ Layout confirmed, map open — click Execute Move."
                : $"✗ Layout mismatch — {layoutDetails}",
                layoutOk ? StatusKind.Normal : StatusKind.Error);
        }

        UpdateStepButton();
    }

    private void OnStep(object sender, RoutedEventArgs e)
    {
        if (_currentTest is null) return;

        switch (_phase)
        {
            case PlaybackPhase.MapOpen:
            {
                _hideDropPreview?.Invoke();
                var zone = DockingLayoutEngine.ParseZoneDisplayName(_currentTest.TargetZoneDisplay);
                _executeMove(_currentTest.SourcePanelId, zone, _currentTest.TargetOrder);
                _phase = PlaybackPhase.MoveExecuted;
                var actual = _getCurrentLayout();
                var (ok, details) = CompareZoneMaps(_currentTest.ExpectedLayout, actual);
                SetStatus(ok
                    ? $"✓ Test passed: {_currentTest.SourcePanelId} → {_currentTest.TargetZoneDisplay} @ {_currentTest.TargetOrder} — click Next Test."
                    : $"✗ Test FAILED — {details}",
                    ok ? StatusKind.Normal : StatusKind.Error);
                break;
            }

            case PlaybackPhase.MoveExecuted:
            {
                // Advancing selection triggers OnTestSelected which auto-applies the next layout and opens the map.
                int nextIdx = _testList.SelectedIndex + 1;
                if (nextIdx < _testList.Items.Count)
                    _testList.SelectedIndex = nextIdx;
                else
                {
                    SetStatus("All tests complete.", StatusKind.Subtle);
                    _stepButton.IsEnabled = false;
                }
                return;
            }
        }

        UpdateStepButton();
    }

    private void OnAddToChat(object sender, RoutedEventArgs e)
    {
        int idx = _testList.SelectedIndex;
        if (idx < 0 || idx >= _entries.Count) return;
        var entry = _entries[idx];
        _addFileToChat(entry.FilePath, entry.DisplayName);
    }

    private void UpdateStepButton()
    {
        _stepButton.IsEnabled = _testLoaded && _currentTest is not null;
        _stepButton.Content   = _phase switch
        {
            PlaybackPhase.MapOpen      => "Execute Move →",
            PlaybackPhase.MoveExecuted => "Next Test →",
            _                          => "Step →",
        };
    }

    private enum StatusKind { Subtle, Normal, Error }

    private void SetStatus(string text, StatusKind kind)
    {
        _statusLabel.Text = text;
        // Pass/neutral messages use high-contrast label text; only failures get red.
        if (kind == StatusKind.Error)
            _statusLabel.SetValue(TextBlock.ForegroundProperty,
                new SolidColorBrush(Color.FromRgb(210, 60, 60)));
        else
            _statusLabel.SetResourceReference(TextBlock.ForegroundProperty,
                kind == StatusKind.Subtle ? "SubtleText" : "LabelText");
    }

    /// <summary>
    /// Compares <paramref name="expected"/> zone map against <paramref name="actual"/>.
    /// Only checks zones/panels listed in expected; extra panels in actual are ignored.
    /// This handles the common case where the test JSON was recorded with a subset of
    /// visible panels — unrecorded panels (approvals, notes, etc.) may still be in Top
    /// in the live layout and should not cause a false failure.
    /// </summary>
    private static (bool Match, string Details) CompareZoneMaps(
        Dictionary<string, List<string>> expected,
        Dictionary<string, List<string>> actual)
    {
        // Build the full set of panels mentioned in the expected layout so we can filter
        // actual panels down to only those we care about.
        var expectedPanelSet = new HashSet<string>(
            expected.Values.SelectMany(v => v),
            StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<string>();
        foreach (var (zone, expectedPanels) in expected)
        {
            actual.TryGetValue(zone, out var actualPanels);
            actualPanels ??= [];

            // Only compare panels that appear in the expected layout; ignore extras.
            var exp = expectedPanels.Select(p => p.ToLowerInvariant()).ToList();
            var act = actualPanels
                .Where(p => expectedPanelSet.Contains(p))
                .Select(p => p.ToLowerInvariant())
                .ToList();

            if (!exp.SequenceEqual(act))
                mismatches.Add($"{zone}: expected [{string.Join(",", exp)}] got [{string.Join(",", act)}]");
        }
        return mismatches.Count == 0
            ? (true, "All zones match")
            : (false, string.Join("; ", mismatches));
    }

    private static Dictionary<string, List<string>> ParseZoneMap(JsonElement element)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            var panels = new List<string>();
            foreach (var item in prop.Value.EnumerateArray())
            {
                var s = item.GetString();
                if (s is not null) panels.Add(s);
            }
            result[prop.Name] = panels;
        }
        return result;
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject obj) where T : DependencyObject
    {
        var current = obj;
        while (current is not null)
        {
            if (current is T match) return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
