#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SquadDash.PanelDocking;

internal sealed class DockingMapWindow : Window
{
    private readonly DockingMapViewModel _viewModel;
    private readonly PanelDockingService _dockingService;
    private readonly string _workspacePath;
    private readonly Brush? _hoverBrush;

    private Window? _previewOverlay;

    public DockingMapWindow(
        DockingMapViewModel viewModel,
        PanelDockingService dockingService,
        string workspacePath,
        ResourceDictionary appResources,
        Brush? hoverBrush = null)
    {
        _viewModel      = viewModel;
        _dockingService = dockingService;
        _workspacePath  = workspacePath;
        _hoverBrush     = hoverBrush;

        WindowStyle      = WindowStyle.None;
        AllowsTransparency = true;
        Background       = Brushes.Transparent;
        Topmost          = true;
        ShowInTaskbar    = false;
        ResizeMode       = ResizeMode.NoResize;
        SizeToContent    = SizeToContent.WidthAndHeight;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        // Wire Deactivated only after the first Activated — wiring it in the constructor
        // causes an immediate Close() because WPF fires WM_ACTIVATE during Show() itself.
        void OnFirstActivated(object? s, EventArgs e)
        {
            Activated   -= OnFirstActivated;
            Deactivated += (_, _) => { if (IsVisible) Dispatcher.InvokeAsync(Close); };
        }
        Activated += OnFirstActivated;

        // Close the preview overlay whenever this window closes.
        Closed += (_, _) => { _previewOverlay?.Close(); _previewOverlay = null; };

        BuildUI(appResources);
    }

    private void BuildUI(ResourceDictionary appResources)
    {
        var root = new Border
        {
            Background = appResources.Contains("ChromeSurface")
                ? (Brush)appResources["ChromeSurface"]
                : new SolidColorBrush(Color.FromRgb(40, 40, 44)),
            CornerRadius = new CornerRadius(4),
            Padding  = new Thickness(PopupPadding),
            Width    = _viewModel.PopupWidth,
            Height   = _viewModel.PopupHeight,
            Effect   = new DropShadowEffect
            {
                BlurRadius  = 8,
                Opacity     = 0.35,
                ShadowDepth = 2,
                Direction   = 270,
            }
        };

        var canvas = new Canvas
        {
            Width  = _viewModel.PopupWidth  - PopupPadding * 2,
            Height = _viewModel.PopupHeight - PopupPadding * 2,
        };

        foreach (var slot in _viewModel.Slots)
        {
            var btn = BuildSlotButton(slot, appResources);
            Canvas.SetLeft(btn, slot.X);
            Canvas.SetTop(btn,  slot.Y);
            canvas.Children.Add(btn);
        }

        root.Child = canvas;
        Content    = root;
    }

    private const double PopupPadding = 8;

    private Button BuildSlotButton(SlotButtonViewModel slot, ResourceDictionary appResources)
    {
        var btn = new Button
        {
            Width     = slot.Width,
            Height    = slot.Height,
            IsEnabled = !slot.IsSourcePanel,
            FontSize  = 11,
        };

        if (appResources.Contains("DockSlotButtonStyle"))
            btn.Style = (Style)appResources["DockSlotButtonStyle"];

        Brush chromeSurface = GetRes(appResources, "ChromeSurface", Brushes.DimGray);
        Brush labelText     = GetRes(appResources, "LabelText",     Brushes.White);
        Brush subtleText    = GetRes(appResources, "SubtleText",    Brushes.DarkGray);
        Brush panelBorder   = GetRes(appResources, "RosterPanelBorder", Brushes.SlateGray);

        btn.Background      = chromeSurface;
        btn.BorderBrush     = panelBorder;
        btn.BorderThickness = new Thickness(1);

        if (slot.IsSourcePanel)
        {
            btn.Foreground = subtleText;
        }
        else
        {
            btn.Foreground = labelText;
            btn.Click += (_, _) =>
            {
                try
                {
                    _dockingService.MovePanel(slot.SourcePanelId, slot.TargetZone, slot.TargetOrder);
                    if (!string.IsNullOrEmpty(_workspacePath))
                        _dockingService.SaveLayout(_workspacePath);
                }
                catch { /* swallow — best effort */ }
                Close();
            };

            // Show/hide a semi-transparent overlay on the main window at the target location.
            btn.MouseEnter += (_, _) => OnSlotHover(slot);
            btn.MouseLeave += (_, _) => HidePreview();
        }

        return btn;
    }

    private void OnSlotHover(SlotButtonViewModel slot)
    {
        var rect = _dockingService.GetSlotScreenRect(slot);
        if (rect.IsEmpty) { HidePreview(); return; }
        ShowPreview(rect);
    }

    private void ShowPreview(Rect screenRect)
    {
        if (_hoverBrush is null) return;
        EnsurePreviewOverlay();
        _previewOverlay!.Left   = screenRect.Left;
        _previewOverlay!.Top    = screenRect.Top;
        _previewOverlay!.Width  = Math.Max(screenRect.Width,  1);
        _previewOverlay!.Height = Math.Max(screenRect.Height, 1);
        if (!_previewOverlay.IsVisible)
            _previewOverlay.Show();
    }

    private void HidePreview() => _previewOverlay?.Hide();

    private void EnsurePreviewOverlay()
    {
        if (_previewOverlay is not null) return;

        var previewBorder = new Border
        {
            Background   = _hoverBrush,
            CornerRadius = new CornerRadius(6),
        };

        _previewOverlay = new Window
        {
            WindowStyle           = WindowStyle.None,
            AllowsTransparency    = true,
            Background            = Brushes.Transparent,
            Topmost               = true,
            ShowInTaskbar         = false,
            ResizeMode            = ResizeMode.NoResize,
            ShowActivated         = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content               = previewBorder,
        };
    }

    private static Brush GetRes(ResourceDictionary r, string key, Brush fallback) =>
        r.Contains(key) ? (Brush)r[key] : fallback;

    /// <summary>
    /// Opens the popup positioned so the source panel slot is centered on the given screen point.
    /// Clamps to keep the window fully within the active monitor's working area.
    /// </summary>
    public void ShowAtScreenPoint(Point clickScreenPoint)
    {
        // Position so source slot center aligns with click point
        Left = clickScreenPoint.X - _viewModel.SourceSlotCenterX;
        Top  = clickScreenPoint.Y - _viewModel.SourceSlotCenterY;

        Show();

        // Clamp to monitor work area using the project's existing helper
        WindowPlacementHelper.EnsureOnScreen(this);
    }
}
