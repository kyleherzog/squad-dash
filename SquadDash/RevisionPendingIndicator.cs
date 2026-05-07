using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SquadDash;

/// <summary>
/// An animated inline indicator inserted into a <see cref="RichTextBox"/> FlowDocument
/// at the end of the user's selection while a Revise-with-AI request is in flight.
///
/// The indicator is an <see cref="InlineUIContainer"/> and is therefore intentionally
/// excluded from <see cref="RichTextBoxExtensions.GetPlainText"/> output — it never
/// appears in the text written to disk.
/// </summary>
internal sealed class RevisionPendingIndicator
{
    // Safety timeout slightly beyond the 120 s AI request timeout.
    private const double FallbackTimeoutSeconds = 130;

    private readonly InlineUIContainer _container;
    private readonly DispatcherTimer   _fallbackTimer;
    private bool _removed;

    private RevisionPendingIndicator(InlineUIContainer container)
    {
        _container = container;

        _fallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(FallbackTimeoutSeconds) };
        _fallbackTimer.Tick += (_, _) => Remove();
        _fallbackTimer.Start();
    }

    /// <summary>
    /// Inserts a pulsing indicator inline immediately after <paramref name="afterCharOffset"/>
    /// in <paramref name="rtb"/>'s FlowDocument. Returns <c>null</c> on any error (the
    /// indicator is cosmetic — failure must never affect the revision flow).
    /// </summary>
    internal static RevisionPendingIndicator? Insert(RichTextBox rtb, int afterCharOffset)
    {
        try
        {
            var pointer     = rtb.GetTextPointerAt(afterCharOffset);
            var insertPoint = pointer.GetInsertionPosition(LogicalDirection.Forward);
            var element     = BuildElement(rtb);
            var container   = new InlineUIContainer(element, insertPoint);
            return new RevisionPendingIndicator(container);
        }
        catch { return null; }
    }

    /// <summary>
    /// Removes the indicator from the FlowDocument. Safe to call more than once.
    /// </summary>
    internal void Remove()
    {
        if (_removed) return;
        _removed = true;
        _fallbackTimer.Stop();
        try { _container.SiblingInlines?.Remove(_container); }
        catch { }
    }

    // ── Visual ───────────────────────────────────────────────────────────────

    private static UIElement BuildElement(RichTextBox rtb)
    {
        // Spinning arc circle — a single partial-arc Ellipse rotating over a dim full ring.
        // A circumference of ~31.4 px at diameter 10, StrokeThickness 1.5 → ~20.9 stroke-units.
        // Dash {14, 100}: a ~67% arc segment, gap large enough to hide the repeat.
        const double Diameter       = 10;
        const double StrokeW        = 1.5;
        const double ArcDash        = 14.0;  // ≈ 67% of circle visible
        const double ArcGap         = 100.0; // larger than circumference → single arc
        const double SpinDurationMs = 900;

        var container = new Grid
        {
            Width               = Diameter + StrokeW * 2,
            Height              = Diameter + StrokeW * 2,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin              = new Thickness(4, 0, 2, 0),
            ToolTip             = "AI is revising this selection",
        };

        // Dim background ring
        var trackRing = new Ellipse
        {
            Width               = Diameter,
            Height              = Diameter,
            StrokeThickness     = StrokeW,
            Opacity             = 0.2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        trackRing.SetResourceReference(Shape.StrokeProperty, "ActionLinkText");
        container.Children.Add(trackRing);

        // Spinning arc
        var spinRing = new Ellipse
        {
            Width               = Diameter,
            Height              = Diameter,
            StrokeThickness     = StrokeW,
            StrokeDashArray     = new DoubleCollection(new[] { ArcDash, ArcGap }),
            RenderTransformOrigin = new Point(0.5, 0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        spinRing.SetResourceReference(Shape.StrokeProperty, "ActionLinkText");

        var rt = new RotateTransform(0);
        spinRing.RenderTransform = rt;

        var spin = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromMilliseconds(SpinDurationMs)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        rt.BeginAnimation(RotateTransform.AngleProperty, spin);

        container.Children.Add(spinRing);

        return container;
    }
}
