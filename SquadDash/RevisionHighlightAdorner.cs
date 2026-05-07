using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SquadDash;

/// <summary>
/// Adorner that draws a translucent background highlight over the character range
/// being revised by "Revise with AI".  Drawn without touching the FlowDocument;
/// removed by calling <see cref="Remove"/>.
/// </summary>
internal sealed class RevisionHighlightAdorner : Adorner
{
    private readonly RichTextBox _rtb;
    private TextPointer? _start;
    private TextPointer? _end;

    private RevisionHighlightAdorner(RichTextBox rtb) : base(rtb)
    {
        _rtb = rtb;
        IsHitTestVisible = false;

        rtb.SizeChanged += (_, _) => InvalidateVisual();
        rtb.Loaded += (_, _) => SubscribeToScrollViewer();
        if (rtb.IsLoaded)
            SubscribeToScrollViewer();
    }

    private void SubscribeToScrollViewer()
    {
        var sv = FindScrollViewer(_rtb);
        if (sv is not null)
            sv.ScrollChanged += (_, _) => InvalidateVisual();
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;
            var found = FindScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    /// Attaches a highlight adorner to <paramref name="rtb"/> covering chars
    /// <paramref name="startOffset"/>..<paramref name="startOffset"/>+<paramref name="length"/>.
    /// Returns <c>null</c> on any error — the highlight is cosmetic only.
    /// </summary>
    internal static RevisionHighlightAdorner? Attach(RichTextBox rtb, int startOffset, int length)
    {
        try
        {
            var layer = AdornerLayer.GetAdornerLayer(rtb);
            if (layer is null) return null;

            var startPtr = rtb.GetTextPointerAt(startOffset)
                              .GetInsertionPosition(LogicalDirection.Forward);
            var endPtr   = rtb.GetTextPointerAt(startOffset + length)
                              .GetInsertionPosition(LogicalDirection.Backward);

            var adorner = new RevisionHighlightAdorner(rtb) { _start = startPtr, _end = endPtr };
            layer.Add(adorner);
            return adorner;
        }
        catch { return null; }
    }

    /// <summary>Removes the adorner from its layer. Safe to call more than once.</summary>
    internal void Remove()
    {
        try
        {
            _start = null;
            _end   = null;
            AdornerLayer.GetAdornerLayer(_rtb)?.Remove(this);
        }
        catch { }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_start is null || _end is null) return;

        var brush = GetBrush("RevisionHighlight", Color.FromArgb(100, 100, 140, 255));

        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        try
        {
            DrawRange(dc, brush);
        }
        catch { /* TextPointers become invalid when document is rebuilt — skip silently */ }
        dc.Pop();
    }

    private void DrawRange(DrawingContext dc, Brush brush)
    {
        var startRect = _start!.GetCharacterRect(LogicalDirection.Forward);
        var endRect   = _end!.GetCharacterRect(LogicalDirection.Backward);

        if (startRect.IsEmpty || endRect.IsEmpty) return;

        var bounds = new Rect(RenderSize);

        if (Math.Abs(startRect.Top - endRect.Top) < 2.0)
        {
            // Single-line selection
            DrawRect(dc, brush,
                new Rect(startRect.Left, startRect.Top,
                    Math.Max(2, endRect.Right - startRect.Left),
                    Math.Max(2, startRect.Height)),
                bounds);
        }
        else
        {
            // Multi-line: first line, optional middle block, last line
            DrawRect(dc, brush,
                new Rect(startRect.Left, startRect.Top,
                    Math.Max(2, RenderSize.Width - startRect.Left), startRect.Height),
                bounds);

            var midTop    = startRect.Bottom;
            var midBottom = endRect.Top;
            if (midBottom - midTop > 1.0)
                DrawRect(dc, brush,
                    new Rect(0, midTop, RenderSize.Width, midBottom - midTop),
                    bounds);

            DrawRect(dc, brush,
                new Rect(0, endRect.Top, Math.Max(2, endRect.Right), endRect.Height),
                bounds);
        }
    }

    private static void DrawRect(DrawingContext dc, Brush brush, Rect rect, Rect bounds)
    {
        if (rect.IntersectsWith(bounds))
            dc.DrawRectangle(brush, null, rect);
    }

    private static Brush GetBrush(string key, Color fallback)
    {
        if (Application.Current?.Resources[key] is Brush b) return b;
        return new SolidColorBrush(fallback);
    }
}
