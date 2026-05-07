using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SquadDash;

/// <summary>
/// Draws the transcript selection while the owning RichTextBox is inactive.
/// WPF's built-in inactive selection highlight can cache stale viewport rectangles
/// during mouse-wheel scrolling, so transcript boxes keep that feature disabled and
/// use this adorner for the dim inactive state instead.
/// </summary>
internal sealed class TranscriptInactiveSelectionAdorner : Adorner
{
    private const double SameLineTolerance = 2.0;
    private const int    MaxLineSegments   = 1000;

    private readonly RichTextBox _rtb;
    private AdornerLayer? _layer;
    private ScrollViewer? _scrollViewer;
    private ScrollChangedEventHandler? _scrollChangedHandler;
    private MouseWheelEventHandler? _mouseWheelHandler;

    private TranscriptInactiveSelectionAdorner(RichTextBox rtb) : base(rtb)
    {
        _rtb = rtb ?? throw new ArgumentNullException(nameof(rtb));
        IsHitTestVisible = false;

        _rtb.IsInactiveSelectionHighlightEnabled = false;
        _rtb.Loaded += OnLoaded;
        _rtb.Unloaded += OnUnloaded;
        _rtb.SizeChanged += (_, _) => InvalidateHighlight();
        _rtb.LayoutUpdated += (_, _) =>
        {
            if (ShouldDrawSelection())
                InvalidateVisual();
        };
        _rtb.SelectionChanged += (_, _) => InvalidateHighlight();
        _rtb.IsKeyboardFocusWithinChanged += (_, _) => InvalidateHighlight();

        _mouseWheelHandler = (_, _) => InvalidateHighlight();
        _rtb.AddHandler(UIElement.MouseWheelEvent, _mouseWheelHandler, true);

        TryAttachToLayer();
        RefreshScrollViewerSubscription();
    }

    internal static TranscriptInactiveSelectionAdorner Attach(RichTextBox rtb) => new(rtb);

    internal void InvalidateHighlight() => InvalidateVisual();

    internal void RefreshScrollViewerSubscription()
    {
        var newScrollViewer = FindScrollViewer(_rtb);
        if (ReferenceEquals(newScrollViewer, _scrollViewer))
            return;

        if (_scrollViewer is not null && _scrollChangedHandler is not null)
            _scrollViewer.ScrollChanged -= _scrollChangedHandler;

        _scrollViewer = newScrollViewer;
        if (_scrollViewer is not null)
        {
            _scrollChangedHandler = (_, _) => InvalidateHighlight();
            _scrollViewer.ScrollChanged += _scrollChangedHandler;
        }
        else
        {
            _scrollChangedHandler = null;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _rtb.IsInactiveSelectionHighlightEnabled = false;
        TryAttachToLayer();
        RefreshScrollViewerSubscription();
        InvalidateHighlight();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is not null && _scrollChangedHandler is not null)
            _scrollViewer.ScrollChanged -= _scrollChangedHandler;
        _scrollViewer = null;
        _scrollChangedHandler = null;

        if (_layer is not null)
        {
            try { _layer.Remove(this); }
            catch { }
            _layer = null;
        }
    }

    private void TryAttachToLayer()
    {
        if (_layer is not null)
            return;

        var layer = AdornerLayer.GetAdornerLayer(_rtb);
        if (layer is null)
            return;

        layer.Add(this);
        _layer = layer;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!ShouldDrawSelection())
            return;

        var brush = GetBrush("DocEditorSelectionBrush", Color.FromRgb(21, 101, 192));
        var opacity = GetDouble("TranscriptInactiveSelectionOpacity", 0.15);

        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        dc.PushOpacity(opacity);
        try
        {
            DrawSelection(dc, _rtb.Selection.Start, _rtb.Selection.End, brush);
        }
        catch
        {
            // TextPointers can become temporarily invalid while the document is moved
            // between transcript boxes. The selection is cosmetic; skip that frame.
        }
        finally
        {
            dc.Pop();
            dc.Pop();
        }
    }

    private bool ShouldDrawSelection() =>
        _rtb.IsVisible
        && _rtb.Opacity > 0.01
        && _rtb.IsHitTestVisible
        && RenderSize.Width > 0
        && RenderSize.Height > 0
        && !_rtb.Selection.IsEmpty
        && !_rtb.IsKeyboardFocusWithin;

    private void DrawSelection(DrawingContext dc, TextPointer start, TextPointer end, Brush brush)
    {
        if (start.CompareTo(end) >= 0)
            return;

        var startRect = start.GetCharacterRect(LogicalDirection.Forward);
        var endRect   = end.GetCharacterRect(LogicalDirection.Backward);
        if (startRect.IsEmpty || endRect.IsEmpty)
            return;

        var bounds = new Rect(RenderSize);
        if (Math.Abs(startRect.Top - endRect.Top) <= SameLineTolerance)
        {
            DrawRect(dc, brush,
                new Rect(
                    startRect.Left,
                    startRect.Top,
                    Math.Max(2, endRect.Right - startRect.Left),
                    Math.Max(2, startRect.Height)),
                bounds);
            return;
        }

        if (!TryDrawLineSegments(dc, brush, start, end, bounds))
            DrawEndpointFallback(dc, brush, startRect, endRect, bounds);
    }

    private bool TryDrawLineSegments(
        DrawingContext dc,
        Brush brush,
        TextPointer start,
        TextPointer end,
        Rect bounds)
    {
        var current = start;
        var drewAny = false;

        for (var guard = 0; guard < MaxLineSegments && current.CompareTo(end) < 0; guard++)
        {
            var lineStart = current.GetLineStartPosition(0) ?? current;
            var nextLineStart = lineStart.GetLineStartPosition(1);
            var segmentEnd = nextLineStart is null || nextLineStart.CompareTo(end) >= 0
                ? end
                : nextLineStart;

            if (current.CompareTo(segmentEnd) < 0)
            {
                if (!TryDrawSegment(dc, brush, current, segmentEnd, bounds))
                    return false;
                drewAny = true;
            }

            if (nextLineStart is null || nextLineStart.CompareTo(end) >= 0)
                break;

            if (nextLineStart.CompareTo(current) <= 0)
                break;

            current = nextLineStart;
        }

        return drewAny;
    }

    private bool TryDrawSegment(
        DrawingContext dc,
        Brush brush,
        TextPointer start,
        TextPointer end,
        Rect bounds)
    {
        var startRect = start.GetCharacterRect(LogicalDirection.Forward);
        var endRect   = end.GetCharacterRect(LogicalDirection.Backward);
        if (startRect.IsEmpty || endRect.IsEmpty)
            return false;

        if (Math.Abs(startRect.Top - endRect.Top) > SameLineTolerance)
            return false;

        var right = endRect.Right;
        if (right <= startRect.Left)
            right = RenderSize.Width;

        DrawRect(dc, brush,
            new Rect(
                startRect.Left,
                startRect.Top,
                Math.Max(2, right - startRect.Left),
                Math.Max(2, startRect.Height)),
            bounds);
        return true;
    }

    private void DrawEndpointFallback(
        DrawingContext dc,
        Brush brush,
        Rect startRect,
        Rect endRect,
        Rect bounds)
    {
        DrawRect(dc, brush,
            new Rect(
                startRect.Left,
                startRect.Top,
                Math.Max(2, RenderSize.Width - startRect.Left),
                Math.Max(2, startRect.Height)),
            bounds);

        var middleTop = startRect.Bottom;
        var middleBottom = endRect.Top;
        if (middleBottom - middleTop > 1)
            DrawRect(dc, brush, new Rect(0, middleTop, RenderSize.Width, middleBottom - middleTop), bounds);

        DrawRect(dc, brush,
            new Rect(
                0,
                endRect.Top,
                Math.Max(2, endRect.Right),
                Math.Max(2, endRect.Height)),
            bounds);
    }

    private static void DrawRect(DrawingContext dc, Brush brush, Rect rect, Rect bounds)
    {
        if (rect.IntersectsWith(bounds))
            dc.DrawRectangle(brush, null, rect);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv)
                return sv;

            var found = FindScrollViewer(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static Brush GetBrush(string key, Color fallback) =>
        Application.Current?.Resources[key] as Brush ?? new SolidColorBrush(fallback);

    private static double GetDouble(string key, double fallback) =>
        Application.Current?.Resources[key] is double value ? value : fallback;
}
