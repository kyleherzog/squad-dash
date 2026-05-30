#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SquadDash.PanelDocking;

/// <summary>
/// Manages the current panel layout and moves panel controls between dock zones.
/// </summary>
internal sealed class PanelDockingService
{
    private string? _workspacePath;

    // WPF context — null when running under unit tests.
    private readonly Dictionary<string, FrameworkElement>? _panelRegistry;
    private readonly Grid? _leftZonePanel;
    private readonly Grid? _rightZonePanel;
    private readonly Grid? _left2ZonePanel;
    private readonly Grid? _right2ZonePanel;
    private readonly Grid? _topZoneGrid;
    private readonly ColumnDefinition? _leftZoneColumn;
    private readonly ColumnDefinition? _rightZoneColumn;
    private readonly ColumnDefinition? _left2ZoneColumn;
    private readonly ColumnDefinition? _right2ZoneColumn;
    private readonly ColumnDefinition? _leftSplitterColumn;
    private readonly ColumnDefinition? _rightSplitterColumn;
    private readonly ColumnDefinition? _left2SplitterColumn;
    private readonly ColumnDefinition? _right2SplitterColumn;
    private readonly UIElement? _leftZoneScrollViewer;
    private readonly UIElement? _rightZoneScrollViewer;
    private readonly UIElement? _left2ZoneScrollViewer;
    private readonly UIElement? _right2ZoneScrollViewer;
    private readonly UIElement? _leftZoneSplitter;
    private readonly UIElement? _rightZoneSplitter;
    private readonly UIElement? _left2ZoneSplitter;
    private readonly UIElement? _right2ZoneSplitter;

    // Maps each dockable panel ID to its column index within TopZonePanelsGrid.
    // Used only to validate which panels may live in the top zone.
    private static readonly Dictionary<string, int> TopZoneColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["loop"]        = 3,
        ["tasks"]       = 4,
        ["approvals"]   = 6,
        ["notes"]       = 7,
        ["maintenance"] = 8,
        ["inbox"]       = 9,
    };

    // Physical grid columns available for dockable top-zone panels, in left-to-right order.
    // Column 5 is occupied by WatchPanelBorder (non-dockable) so it is skipped.
    private static readonly int[] TopZonePhysicalColumns = [3, 4, 6, 7, 8, 9];

    // Saves each panel's original XAML Height binding so it can be restored when the
    // panel moves back to the Top zone after having been in a Left/Right zone.
    private readonly Dictionary<string, MultiBinding?> _savedHeightBindings =
        new(StringComparer.OrdinalIgnoreCase);

    // Ordered lists of panels currently in each side zone (used to rebuild row layout).
    private readonly List<FrameworkElement> _leftZonePanels   = new();
    private readonly List<FrameworkElement> _rightZonePanels  = new();
    private readonly List<FrameworkElement> _left2ZonePanels  = new();
    private readonly List<FrameworkElement> _right2ZonePanels = new();

    /// <summary>Data-model-only constructor for unit tests.</summary>
    public PanelDockingService() { }

    /// <summary>Full constructor with WPF context for production use.</summary>
    public PanelDockingService(
        Dictionary<string, FrameworkElement> panelRegistry,
        Grid leftZonePanel,
        Grid rightZonePanel,
        Grid left2ZonePanel,
        Grid right2ZonePanel,
        Grid topZoneGrid,
        ColumnDefinition leftZoneColumn,
        ColumnDefinition rightZoneColumn,
        ColumnDefinition left2ZoneColumn,
        ColumnDefinition right2ZoneColumn,
        ColumnDefinition leftSplitterColumn,
        ColumnDefinition rightSplitterColumn,
        ColumnDefinition left2SplitterColumn,
        ColumnDefinition right2SplitterColumn,
        UIElement leftZoneScrollViewer,
        UIElement rightZoneScrollViewer,
        UIElement left2ZoneScrollViewer,
        UIElement right2ZoneScrollViewer,
        UIElement leftZoneSplitter,
        UIElement rightZoneSplitter,
        UIElement left2ZoneSplitter,
        UIElement right2ZoneSplitter)
    {
        _panelRegistry = panelRegistry;
        _leftZonePanel = leftZonePanel;
        _rightZonePanel = rightZonePanel;
        _left2ZonePanel = left2ZonePanel;
        _right2ZonePanel = right2ZonePanel;
        _topZoneGrid = topZoneGrid;
        _leftZoneColumn = leftZoneColumn;
        _rightZoneColumn = rightZoneColumn;
        _left2ZoneColumn = left2ZoneColumn;
        _right2ZoneColumn = right2ZoneColumn;
        _leftSplitterColumn = leftSplitterColumn;
        _rightSplitterColumn = rightSplitterColumn;
        _left2SplitterColumn = left2SplitterColumn;
        _right2SplitterColumn = right2SplitterColumn;
        _leftZoneScrollViewer = leftZoneScrollViewer;
        _rightZoneScrollViewer = rightZoneScrollViewer;
        _left2ZoneScrollViewer = left2ZoneScrollViewer;
        _right2ZoneScrollViewer = right2ZoneScrollViewer;
        _leftZoneSplitter = leftZoneSplitter;
        _rightZoneSplitter = rightZoneSplitter;
        _left2ZoneSplitter = left2ZoneSplitter;
        _right2ZoneSplitter = right2ZoneSplitter;
    }

    /// <summary>The live panel layout for the current session.</summary>
    public DockLayout CurrentLayout { get; private set; } = DockLayout.CreateDefault();

    /// <summary>
    /// Moves <paramref name="panelId"/> to <paramref name="targetZone"/> at position
    /// <paramref name="targetOrder"/>, updating both the in-memory layout model and (when
    /// WPF context is present) the actual UI elements.  When <paramref name="targetOrder"/>
    /// is negative the panel is appended at the end of the zone.
    /// Same-zone reordering is supported.
    /// </summary>
    public void MovePanel(string panelId, DockZone targetZone, int targetOrder = -1)
    {
        var existing = CurrentLayout.Slots.FirstOrDefault(s =>
            string.Equals(s.PanelId, panelId, StringComparison.OrdinalIgnoreCase));

        var sourceZone = existing?.Zone;
        bool sameZone  = existing is not null && existing.Zone == targetZone;

        // Reorder within the same zone — skip if no explicit order requested or already correct.
        if (sameZone)
        {
            if (targetOrder < 0) return; // no-op: same zone, no explicit reorder requested

            var zoneSlots = CurrentLayout.Slots
                .Where(s => s.Zone == targetZone)
                .OrderBy(s => s.Order)
                .Select(s => s.PanelId)
                .ToList();
            int currentIdx = zoneSlots.IndexOf(
                zoneSlots.FirstOrDefault(id => string.Equals(id, panelId, StringComparison.OrdinalIgnoreCase)) ?? "");
            int clampedTarget = targetOrder < 0 ? zoneSlots.Count - 1 : Math.Clamp(targetOrder, 0, zoneSlots.Count - 1);
            if (currentIdx == clampedTarget) return;

            // Reorder the list and reassign Order values using immutable record updates.
            zoneSlots.Remove(panelId);
            zoneSlots.Insert(Math.Clamp(clampedTarget, 0, zoneSlots.Count), panelId);
            CurrentLayout.Slots = CurrentLayout.Slots
                .Where(s => s.Zone != targetZone ||
                            !zoneSlots.Contains(s.PanelId, StringComparer.OrdinalIgnoreCase))
                .Concat(zoneSlots.Select((id, i) => new PanelSlot(id, targetZone, i)))
                .ToList();

            // WPF: reorder the panel within the zone list and rebuild the grid.
            // For the Top zone, reassign physical columns; for side zones, rebuild the row grid.
            if (_panelRegistry is not null)
            {
                if (targetZone == DockZone.Top)
                {
                    RebuildTopZoneLayout();
                }
                else if (_panelRegistry.TryGetValue(panelId, out var el))
                {
                    var (zoneList, zoneGrid, scrollViewer) = GetZoneContext(targetZone);
                    if (zoneList is not null && zoneGrid is not null)
                    {
                        zoneList.Remove(el);
                        zoneList.Insert(Math.Clamp(clampedTarget, 0, zoneList.Count), el);
                        RebuildZoneGrid(zoneGrid, zoneList, scrollViewer as FrameworkElement);
                    }
                }
            }
            return;
        }

        // Cross-zone move — update data model.
        var slots = CurrentLayout.Slots
            .Where(s => !string.Equals(s.PanelId, panelId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Determine insertion index within target zone.
        var targetZoneSlots = slots
            .Where(s => s.Zone == targetZone)
            .OrderBy(s => s.Order)
            .ToList();

        int insertAt = targetOrder < 0 ? targetZoneSlots.Count : Math.Clamp(targetOrder, 0, targetZoneSlots.Count);

        // Build ordered list of target zone panel IDs with the new panel inserted, then renumber.
        var targetIds = targetZoneSlots.Select(s => s.PanelId).ToList();
        targetIds.Insert(insertAt, panelId);

        // Replace all target-zone slots with freshly ordered records.
        slots = slots.Where(s => s.Zone != targetZone).ToList();
        slots.AddRange(targetIds.Select((id, i) => new PanelSlot(id, targetZone, i)));
        CurrentLayout.Slots = slots;

        // WPF reparenting (only when context is wired).
        if (_panelRegistry is null) return;
        if (!_panelRegistry.TryGetValue(panelId, out var element)) return;

        // Zone-aware removal — side zones need Grid row cleanup; top zone is a plain Grid.
        if (sourceZone == DockZone.Left)
            RemoveFromZone(_leftZonePanel!, _leftZonePanels, element, _leftZoneScrollViewer as FrameworkElement);
        else if (sourceZone == DockZone.Right)
            RemoveFromZone(_rightZonePanel!, _rightZonePanels, element, _rightZoneScrollViewer as FrameworkElement);
        else if (sourceZone == DockZone.Left2)
            RemoveFromZone(_left2ZonePanel!, _left2ZonePanels, element, _left2ZoneScrollViewer as FrameworkElement);
        else if (sourceZone == DockZone.Right2)
            RemoveFromZone(_right2ZonePanel!, _right2ZonePanels, element, _right2ZoneScrollViewer as FrameworkElement);
        else
            RemoveFromTopZone(element);

        switch (targetZone)
        {
            case DockZone.Left:
                AddToZone(_leftZonePanel!, _leftZonePanels, element, panelId, _leftZoneScrollViewer as FrameworkElement, insertAt);
                ExpandZone(_leftZoneColumn!, _leftSplitterColumn!, _leftZoneScrollViewer!, _leftZoneSplitter!, element);
                break;

            case DockZone.Right:
                AddToZone(_rightZonePanel!, _rightZonePanels, element, panelId, _rightZoneScrollViewer as FrameworkElement, insertAt);
                ExpandZone(_rightZoneColumn!, _rightSplitterColumn!, _rightZoneScrollViewer!, _rightZoneSplitter!, element);
                break;

            case DockZone.Left2:
                AddToZone(_left2ZonePanel!, _left2ZonePanels, element, panelId, _left2ZoneScrollViewer as FrameworkElement, insertAt);
                ExpandZone(_left2ZoneColumn!, _left2SplitterColumn!, _left2ZoneScrollViewer!, _left2ZoneSplitter!, element);
                break;

            case DockZone.Right2:
                AddToZone(_right2ZonePanel!, _right2ZonePanels, element, panelId, _right2ZoneScrollViewer as FrameworkElement, insertAt);
                ExpandZone(_right2ZoneColumn!, _right2SplitterColumn!, _right2ZoneScrollViewer!, _right2ZoneSplitter!, element);
                break;

            case DockZone.Top:
                AddToTopZone(panelId, element);
                break;
        }

        // Collapse source zone if it is now empty.
        if (sourceZone == DockZone.Left && !ZoneHasPanels(DockZone.Left))
            CollapseZone(_leftZoneColumn!, _leftSplitterColumn!, _leftZoneScrollViewer!, _leftZoneSplitter!);
        else if (sourceZone == DockZone.Right && !ZoneHasPanels(DockZone.Right))
            CollapseZone(_rightZoneColumn!, _rightSplitterColumn!, _rightZoneScrollViewer!, _rightZoneSplitter!);
        else if (sourceZone == DockZone.Left2 && !ZoneHasPanels(DockZone.Left2))
            CollapseZone(_left2ZoneColumn!, _left2SplitterColumn!, _left2ZoneScrollViewer!, _left2ZoneSplitter!);
        else if (sourceZone == DockZone.Right2 && !ZoneHasPanels(DockZone.Right2))
            CollapseZone(_right2ZoneColumn!, _right2SplitterColumn!, _right2ZoneScrollViewer!, _right2ZoneSplitter!);
    }

    private bool ZoneHasPanels(DockZone zone) =>
        CurrentLayout.Slots.Any(s => s.Zone == zone);

    private static void RemoveFromTopZone(FrameworkElement element)
    {
        if (LogicalTreeHelper.GetParent(element) is Panel parent)
            parent.Children.Remove(element);
    }

    private static void RemoveFromZone(Grid zone, List<FrameworkElement> zoneList, FrameworkElement element, FrameworkElement? scrollViewer)
    {
        zoneList.Remove(element);
        RebuildZoneGrid(zone, zoneList, scrollViewer);
    }

    private void AddToZone(Grid zone, List<FrameworkElement> zoneList, FrameworkElement element, string panelId, FrameworkElement? scrollViewer, int insertAt = -1)
    {
        // Save the original height binding before we replace it (only once per panel).
        if (!_savedHeightBindings.ContainsKey(panelId))
            _savedHeightBindings[panelId] = BindingOperations.GetMultiBinding(element, FrameworkElement.HeightProperty);

        DetachFromCurrentPanelParent(element);

        element.ClearValue(Grid.ColumnProperty);
        element.ClearValue(FrameworkElement.MarginProperty);
        element.ClearValue(FrameworkElement.MaxWidthProperty);
        element.ClearValue(FrameworkElement.VerticalAlignmentProperty);
        element.ClearValue(FrameworkElement.HeightProperty); // row sizing handles height

        if (insertAt < 0 || insertAt >= zoneList.Count)
            zoneList.Add(element);
        else
            zoneList.Insert(insertAt, element);
        RebuildZoneGrid(zone, zoneList, scrollViewer);
    }

    /// <summary>Returns the zone list, grid, and scroll viewer for a given side zone.</summary>
    private (List<FrameworkElement>? zoneList, Grid? zoneGrid, UIElement? scrollViewer) GetZoneContext(DockZone zone) =>
        zone switch
        {
            DockZone.Left   => (_leftZonePanels,   _leftZonePanel,   _leftZoneScrollViewer),
            DockZone.Right  => (_rightZonePanels,  _rightZonePanel,  _rightZoneScrollViewer),
            DockZone.Left2  => (_left2ZonePanels,  _left2ZonePanel,  _left2ZoneScrollViewer),
            DockZone.Right2 => (_right2ZonePanels, _right2ZonePanel, _right2ZoneScrollViewer),
            _               => (null, null, null),
        };

    /// <summary>
    /// Clears and rebuilds the side-zone Grid with one star row per panel and a
    /// 5 px <see cref="GridSplitter"/> row between consecutive panels.
    /// The Grid height is bound to <paramref name="scrollViewer"/> so star rows have
    /// a finite space to divide.
    /// </summary>
    private static void RebuildZoneGrid(Grid zone, List<FrameworkElement> panels, FrameworkElement? scrollViewer)
    {
        zone.Children.Clear();
        zone.RowDefinitions.Clear();

        for (int i = 0; i < panels.Count; i++)
        {
            if (i > 0)
            {
                zone.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
                var splitter = new GridSplitter
                {
                    Height = 5,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment   = VerticalAlignment.Center,
                    ResizeDirection     = GridResizeDirection.Rows,
                    ResizeBehavior      = GridResizeBehavior.PreviousAndNext,
                };
                splitter.SetResourceReference(Control.BackgroundProperty, "AppSurface");
                splitter.Opacity = 0.25;
                Grid.SetRow(splitter, zone.RowDefinitions.Count - 1);
                zone.Children.Add(splitter);
            }

            zone.RowDefinitions.Add(new RowDefinition
            {
                Height    = new GridLength(1, GridUnitType.Star),
                MinHeight = 100,
            });
            Grid.SetRow(panels[i], zone.RowDefinitions.Count - 1);
            zone.Children.Add(panels[i]);
        }

        // Bind the zone Grid height to the scroll viewer so star rows fill the column.
        BindingOperations.ClearBinding(zone, FrameworkElement.HeightProperty);
        if (scrollViewer is not null && panels.Count > 0)
            zone.SetBinding(FrameworkElement.HeightProperty,
                new Binding(nameof(FrameworkElement.ActualHeight)) { Source = scrollViewer });
    }

    private void AddToTopZone(string panelId, FrameworkElement element)
    {
        if (_topZoneGrid is null) return;
        if (!TopZoneColumnMap.ContainsKey(panelId)) return; // not a valid top-zone panel

        // Remove any zone-height binding and restore the original Top-zone height constraint.
        element.ClearValue(FrameworkElement.HeightProperty);
        if (_savedHeightBindings.TryGetValue(panelId, out var saved) && saved is not null)
            BindingOperations.SetBinding(element, FrameworkElement.HeightProperty, saved);

        element.VerticalAlignment = VerticalAlignment.Top;
        element.Margin = new Thickness(14, 0, 0, 0);
        DetachFromCurrentPanelParent(element);
        _topZoneGrid.Children.Add(element);

        // Assign columns for ALL top-zone panels based on their current Order in the layout.
        RebuildTopZoneLayout();
    }

    /// <summary>
    /// Assigns each top-zone panel element to the physical grid column that corresponds
    /// to its rank (0-based) in <see cref="CurrentLayout"/>.  Call after any change to
    /// the top-zone order so that the visual left-to-right order tracks the data model.
    /// </summary>
    private void RebuildTopZoneLayout()
    {
        if (_topZoneGrid is null || _panelRegistry is null) return;

        var topSlots = CurrentLayout.Slots
            .Where(s => s.Zone == DockZone.Top)
            .OrderBy(s => s.Order)
            .ToList();

        for (int rank = 0; rank < topSlots.Count && rank < TopZonePhysicalColumns.Length; rank++)
        {
            if (_panelRegistry.TryGetValue(topSlots[rank].PanelId, out var element))
                Grid.SetColumn(element, TopZonePhysicalColumns[rank]);
        }
    }

    private static void DetachFromCurrentPanelParent(FrameworkElement element)
    {
        if (LogicalTreeHelper.GetParent(element) is Panel logicalParent)
        {
            logicalParent.Children.Remove(element);
            return;
        }

        if (VisualTreeHelper.GetParent(element) is Panel visualParent)
            visualParent.Children.Remove(element);
    }

    private static void ExpandZone(
        ColumnDefinition zoneCol,
        ColumnDefinition splitterCol,
        UIElement scrollViewer,
        UIElement splitter,
        FrameworkElement arrivedPanel)
    {
        if (zoneCol.Width.IsAbsolute && zoneCol.Width.Value == 0)
        {
            double width = arrivedPanel.ActualWidth > 0 ? arrivedPanel.ActualWidth : 280;
            zoneCol.Width = new GridLength(width);
            splitterCol.Width = new GridLength(5);
            scrollViewer.Visibility = Visibility.Visible;
            splitter.Visibility = Visibility.Visible;
        }
    }

    private static void CollapseZone(
        ColumnDefinition zoneCol,
        ColumnDefinition splitterCol,
        UIElement scrollViewer,
        UIElement splitter)
    {
        zoneCol.Width = new GridLength(0);
        splitterCol.Width = new GridLength(0);
        scrollViewer.Visibility = Visibility.Collapsed;
        splitter.Visibility = Visibility.Collapsed;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string LayoutFilePath(string workspacePath) =>
        Path.Combine(workspacePath, ".squad", "panel-layouts.json");

    /// <summary>
    /// Persists <see cref="CurrentLayout"/> (upserted by name) to
    /// <c>&lt;workspacePath&gt;/.squad/panel-layouts.json</c>.
    /// Creates the <c>.squad</c> directory if it does not yet exist.
    /// </summary>
    public void SaveLayout(string workspacePath)
    {
        _workspacePath = workspacePath;
        var filePath = LayoutFilePath(workspacePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var file = ReadLayoutsFile(filePath);
        file.ActiveLayout = CurrentLayout.Name;

        var idx = file.Layouts.FindIndex(l =>
            string.Equals(l.Name, CurrentLayout.Name, StringComparison.OrdinalIgnoreCase));

        var snapshot = new DockLayout
        {
            Name = CurrentLayout.Name,
            Slots = CurrentLayout.Slots.ToList(),
            LeftZoneWidth   = (_leftZoneColumn   is { } lc  && lc.Width.IsAbsolute  && lc.Width.Value  > 0) ? lc.Width.Value  : (double?)null,
            RightZoneWidth  = (_rightZoneColumn  is { } rc  && rc.Width.IsAbsolute  && rc.Width.Value  > 0) ? rc.Width.Value  : (double?)null,
            Left2ZoneWidth  = (_left2ZoneColumn  is { } l2c && l2c.Width.IsAbsolute && l2c.Width.Value > 0) ? l2c.Width.Value : (double?)null,
            Right2ZoneWidth = (_right2ZoneColumn is { } r2c && r2c.Width.IsAbsolute && r2c.Width.Value > 0) ? r2c.Width.Value : (double?)null,
        };

        if (idx >= 0)
            file.Layouts[idx] = snapshot;
        else
            file.Layouts.Add(snapshot);

        File.WriteAllText(filePath, JsonSerializer.Serialize(file, _jsonOptions));
    }

    /// <summary>
    /// Reads <c>&lt;workspacePath&gt;/.squad/panel-layouts.json</c>, sets
    /// <see cref="CurrentLayout"/> to the active layout, and returns it.
    /// Falls back to <see cref="DockLayout.CreateDefault"/> if the file is
    /// missing or the active layout name is not found.
    /// </summary>
    public DockLayout LoadLayout(string workspacePath)
    {
        _workspacePath = workspacePath;
        CurrentLayout = ReadActiveLayout(workspacePath);
        return CurrentLayout;
    }

    /// <summary>
    /// Reads and applies the active workspace layout, moving the live WPF panels from
    /// their current layout before replacing <see cref="CurrentLayout"/>.
    /// </summary>
    public DockLayout LoadAndApplyLayout(string workspacePath)
    {
        _workspacePath = workspacePath;
        var targetLayout = ReadActiveLayout(workspacePath);
        ApplyLayout(targetLayout);
        return CurrentLayout;
    }

    private void ApplyLayout(DockLayout targetLayout)
    {
        var targetSnapshot = CloneLayout(targetLayout);
        var currentSlots = CurrentLayout.Slots.ToList();

        foreach (var slot in currentSlots.Where(s => s.Zone != DockZone.Top))
            MovePanel(slot.PanelId, DockZone.Top);

        foreach (var slot in targetSnapshot.Slots.Where(s => s.Zone != DockZone.Top).OrderBy(s => s.Order))
            MovePanel(slot.PanelId, slot.Zone);

        CurrentLayout = targetSnapshot;

        // Reassign physical top-zone columns to match the restored layout order.
        RebuildTopZoneLayout();
    }

    private static DockLayout CloneLayout(DockLayout layout) => new()
    {
        Name = layout.Name,
        Slots = layout.Slots.ToList(),
        LeftZoneWidth   = layout.LeftZoneWidth,
        RightZoneWidth  = layout.RightZoneWidth,
        Left2ZoneWidth  = layout.Left2ZoneWidth,
        Right2ZoneWidth = layout.Right2ZoneWidth,
    };

    private static DockLayout ReadActiveLayout(string workspacePath)
    {
        var filePath = LayoutFilePath(workspacePath);
        if (!File.Exists(filePath))
        {
            return DockLayout.CreateDefault();
        }

        var file = ReadLayoutsFile(filePath);
        var layout = file.Layouts.FirstOrDefault(l =>
            string.Equals(l.Name, file.ActiveLayout, StringComparison.OrdinalIgnoreCase));

        return layout is not null
            ? CloneLayout(layout)
            : DockLayout.CreateDefault();
    }

    /// <summary>
    /// Renames <see cref="CurrentLayout"/> without saving to disk.
    /// Call <see cref="SaveLayout"/> afterwards to persist the new name.
    /// </summary>
    public void RenameCurrentLayout(string newName) =>
        CurrentLayout.Name = newName;

    private static PanelLayoutsFile ReadLayoutsFile(string filePath)
    {
        if (!File.Exists(filePath))
            return new PanelLayoutsFile();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<PanelLayoutsFile>(json, _jsonOptions)
                   ?? new PanelLayoutsFile();
        }
        catch
        {
            return new PanelLayoutsFile();
        }
    }

    /// <summary>Returns the names of all layouts saved for the current workspace.</summary>
    public IReadOnlyList<string> SavedLayoutNames =>
        _workspacePath is null
            ? []
            : ReadLayoutsFile(LayoutFilePath(_workspacePath)).Layouts.Select(l => l.Name).ToList();

    /// <summary>
    /// Returns the screen rectangle (in physical pixels) that should be highlighted when the
    /// user hovers over <paramref name="slot"/> in the docking map popup.
    /// <para>
    /// • Column slot (N existing panels) → column's left/right bounds, height divided into
    ///   (N+1) equal bands; targetOrder selects the band (append slot = last band).<br/>
    /// • Empty column zone              → 64px wide strip at the column's near edge.<br/>
    /// • Top zone slot, source in top   → full panel screen rect (replace/reorder).<br/>
    /// • Top zone slot, source elsewhere → thin vertical insertion strip.<br/>
    /// • Empty top zone                 → thin horizontal strip across the top-zone grid.
    /// </para>
    /// Returns <see cref="Rect.Empty"/> when the location cannot be determined.
    /// </summary>
    public Rect GetSlotScreenRect(SlotButtonViewModel slot)
    {
        if (_panelRegistry is null) return Rect.Empty;

        var panelsInZone = CurrentLayout.Slots
            .Where(s => s.Zone == slot.TargetZone)
            .OrderBy(s => s.Order)
            .Select(s => s.PanelId)
            .ToList();

        if (slot.TargetZone == DockZone.Top)
        {
            // If the dragged panel is already in the top zone, hovering another top slot
            // means a reorder/replace — show the full target panel rect, not an insertion strip.
            bool sourceIsInTop = CurrentLayout.Slots.Any(s =>
                s.Zone == DockZone.Top &&
                string.Equals(s.PanelId, slot.SourcePanelId, StringComparison.OrdinalIgnoreCase));

            return sourceIsInTop
                ? GetTopReorderRect(slot.TargetOrder, panelsInZone)
                : GetTopInsertionRect(slot.TargetOrder, panelsInZone);
        }

        return GetColumnSlotRect(slot.TargetZone, slot.TargetOrder, panelsInZone);
    }

    /// <summary>
    /// Source is already in top zone → show the full rect of the panel at targetOrder
    /// (or the last panel's rect for the append "+" slot).
    /// </summary>
    private Rect GetTopReorderRect(int targetOrder, List<string> panelsInZone)
    {
        if (panelsInZone.Count == 0) return Rect.Empty;

        var index = Math.Clamp(targetOrder, 0, panelsInZone.Count - 1);
        if (_panelRegistry!.TryGetValue(panelsInZone[index], out var el))
            return GetScreenRect(el);

        return Rect.Empty;
    }

    private Rect GetColumnSlotRect(DockZone zone, int targetOrder, List<string> panelsInZone)
    {
        UIElement? container = zone switch
        {
            DockZone.Left   => _leftZoneScrollViewer,
            DockZone.Right  => _rightZoneScrollViewer,
            DockZone.Left2  => _left2ZoneScrollViewer,
            DockZone.Right2 => _right2ZoneScrollViewer,
            _               => null,
        };

        if (panelsInZone.Count == 0)
        {
            // Empty zone — 64px wide strip at the edge matching the column's screen side.
            if (container is FrameworkElement cfe && cfe.IsVisible)
            {
                var r = GetScreenRect(cfe);
                if (r.IsEmpty) return r;
                const double StripWidth = 64;
                bool isRightSide = zone == DockZone.Right || zone == DockZone.Right2;
                double x = isRightSide ? r.Right - StripWidth : r.Left;
                return new Rect(x, r.Top, StripWidth, r.Height);
            }
            return Rect.Empty;
        }

        // Get the column's bounding rect (horizontal bounds + total height).
        // Prefer the container scroll-viewer; fall back to the first panel's rect.
        Rect colRect = Rect.Empty;
        if (container is FrameworkElement cfe2 && cfe2.IsVisible)
            colRect = GetScreenRect(cfe2);
        if (colRect.IsEmpty && _panelRegistry!.TryGetValue(panelsInZone[0], out var firstFallback))
            colRect = GetScreenRect(firstFallback);
        if (colRect.IsEmpty) return Rect.Empty;

        // Divide the column height into (N+1) equal bands — one slot per existing panel
        // plus one for the incoming panel.  targetOrder selects the band.
        // The append "+" slot (targetOrder == panelsInZone.Count) naturally becomes the last band.
        int sections = panelsInZone.Count + 1;
        int index    = Math.Clamp(targetOrder, 0, sections - 1);
        double bandH = colRect.Height / sections;
        double top   = colRect.Top + index * bandH;
        return new Rect(colRect.Left, top, colRect.Width, bandH);
    }

    private Rect GetTopInsertionRect(int targetOrder, List<string> panelsInZone)
    {
        if (_topZoneGrid is null) return Rect.Empty;

        var gridRect = GetScreenRect(_topZoneGrid);
        if (gridRect.IsEmpty) return Rect.Empty;

        if (panelsInZone.Count == 0)
        {
            // Rule D: empty top zone — thin horizontal strip across the grid.
            return new Rect(gridRect.Left, gridRect.Top, gridRect.Width, 8);
        }

        const double StripW = 8;

        if (targetOrder < panelsInZone.Count)
        {
            // Thin vertical strip to the LEFT of the panel at targetOrder.
            if (_panelRegistry!.TryGetValue(panelsInZone[targetOrder], out var el))
            {
                var r = GetScreenRect(el);
                if (!r.IsEmpty)
                    return new Rect(r.Left - StripW - 2, gridRect.Top, StripW, gridRect.Height);
            }
        }
        else
        {
            // "+" append slot — thin vertical strip to the RIGHT of the last panel.
            if (_panelRegistry!.TryGetValue(panelsInZone[^1], out var el))
            {
                var r = GetScreenRect(el);
                if (!r.IsEmpty)
                    return new Rect(r.Right + 2, gridRect.Top, StripW, gridRect.Height);
            }
        }

        return Rect.Empty;
    }

    private static Rect GetScreenRect(FrameworkElement el)
    {
        if (!el.IsVisible || el.ActualWidth <= 0) return Rect.Empty;
        try
        {
            var pt = el.PointToScreen(new Point(0, 0));
            return new Rect(pt.X, pt.Y, el.ActualWidth, el.ActualHeight);
        }
        catch { return Rect.Empty; }
    }

    /// <summary>Returns the zone that <paramref name="panelId"/> currently occupies.</summary>
    public DockZone GetCurrentZone(string panelId)
        => CurrentLayout.Slots.FirstOrDefault(s =>
               string.Equals(s.PanelId, panelId, StringComparison.OrdinalIgnoreCase))?.Zone
           ?? DockZone.Top;

    /// <summary>
    /// Registers a panel element at runtime so it can be moved by <see cref="MovePanel"/>.
    /// Adds a <see cref="PanelSlot"/> in <see cref="DockZone.Top"/> if the panel is not
    /// already tracked.
    /// </summary>
    public void RegisterPanel(string panelId, FrameworkElement panel)
    {
        _panelRegistry?.TryAdd(panelId, panel);

        if (!CurrentLayout.Slots.Any(s =>
                string.Equals(s.PanelId, panelId, StringComparison.OrdinalIgnoreCase)))
        {
            int nextOrder = CurrentLayout.Slots
                .Where(s => s.Zone == DockZone.Top)
                .Select(s => s.Order)
                .DefaultIfEmpty(-1)
                .Max() + 1;

            CurrentLayout.Slots = [.. CurrentLayout.Slots, new PanelSlot(panelId, DockZone.Top, nextOrder)];
        }
    }
}
