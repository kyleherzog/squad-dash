using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace SquadDash;

/// <summary>
/// An animated adorner-based indicator positioned at the end of the user's selection
/// while a Revise-with-AI request is in flight. Unlike the previous FlowDocument-based
/// implementation, this adorner does not appear in the undo stack.
/// </summary>
internal sealed class RevisionPendingIndicator : Adorner
{
    private const double FallbackTimeoutSeconds = 130;
    private const double AnimationIntervalMs = 180;
    private const double DotSize = 3.0;
    private const double DotSpacing = 6.0;
    private const double TotalWidth = DotSize * 3 + DotSpacing * 2;
    
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _fallbackTimer;
    private readonly TextPointer _position;
    private int _animationPhase;
    private bool _removed;

    private RevisionPendingIndicator(RichTextBox rtb, TextPointer position) : base(rtb)
    {
        _position = position;
        IsHitTestVisible = false;
        ToolTip = "AI is revising this selection";

        _animationTimer = new DispatcherTimer 
        { 
            Interval = TimeSpan.FromMilliseconds(AnimationIntervalMs) 
        };
        _animationTimer.Tick += (_, _) => 
        {
            _animationPhase = (_animationPhase + 1) % 9;
            InvalidateVisual();
        };
        _animationTimer.Start();

        _fallbackTimer = new DispatcherTimer 
        { 
            Interval = TimeSpan.FromSeconds(FallbackTimeoutSeconds) 
        };
        _fallbackTimer.Tick += (_, _) => Detach();
        _fallbackTimer.Start();
    }

    /// <summary>
    /// Attaches a pulsing indicator adorner at the specified <paramref name="position"/>
    /// in <paramref name="rtb"/>. Returns <c>null</c> on any error (the indicator is
    /// cosmetic — failure must never affect the revision flow).
    /// </summary>
    internal static RevisionPendingIndicator? Attach(RichTextBox rtb, TextPointer position)
    {
        try
        {
            var layer = AdornerLayer.GetAdornerLayer(rtb);
            if (layer is null) return null;

            var indicator = new RevisionPendingIndicator(rtb, position);
            layer.Add(indicator);
            return indicator;
        }
        catch { return null; }
    }

    /// <summary>
    /// Removes the indicator from the adorner layer. Safe to call more than once.
    /// </summary>
    internal void Detach()
    {
        if (_removed) return;
        _removed = true;
        
        _animationTimer.Stop();
        _fallbackTimer.Stop();
        
        try 
        { 
            AdornerLayer.GetAdornerLayer(AdornedElement as RichTextBox)?.Remove(this); 
        }
        catch { }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_removed) return;

        try
        {
            var rect = _position.GetCharacterRect(LogicalDirection.Forward);
            if (rect.IsEmpty) return;

            var brush = GetBrush("ActionLinkText", Colors.SteelBlue);
            
            // Three dots with pulsing opacity
            for (int i = 0; i < 3; i++)
            {
                var opacity = GetDotOpacity(i, _animationPhase);
                var x = rect.Right + 4 + i * DotSpacing;
                var y = rect.Top + rect.Height / 2;
                
                var opacityBrush = brush.Clone();
                opacityBrush.Opacity = opacity;
                
                dc.DrawEllipse(opacityBrush, null, new Point(x, y), DotSize / 2, DotSize / 2);
            }
        }
        catch { /* TextPointer becomes invalid when document is rebuilt — skip silently */ }
    }

    private static double GetDotOpacity(int dotIndex, int phase)
    {
        // Create a pulsing wave effect across the three dots
        // Each dot offset by 3 phases (120 degrees)
        var dotPhase = (phase + dotIndex * 3) % 9;
        
        // Map to sine wave: 0→1→0.3→1→... (pulsing between 0.3 and 1.0)
        return dotPhase switch
        {
            0 => 0.3,
            1 => 0.5,
            2 => 0.7,
            3 => 0.9,
            4 => 1.0,
            5 => 0.9,
            6 => 0.7,
            7 => 0.5,
            8 => 0.3,
            _ => 0.5
        };
    }

    private static Brush GetBrush(string key, Color fallback)
    {
        if (Application.Current?.Resources[key] is Brush b) return b;
        return new SolidColorBrush(fallback);
    }
}
