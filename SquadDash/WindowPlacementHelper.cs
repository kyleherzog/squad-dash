using System;
using System.Windows;
using System.Windows.Media;

namespace SquadDash;

/// <summary>
/// Shared utility for ensuring floating windows are fully visible on a monitor's work area.
/// Uses the existing <see cref="NativeMethods"/> P/Invoke helpers for multi-monitor DPI-aware
/// work-area queries — no System.Windows.Forms reference required.
/// </summary>
internal static class WindowPlacementHelper
{
    /// <summary>
    /// Clamps <paramref name="window"/> Left/Top (and Width/Height when they exceed the work
    /// area) so the entire window is visible within the nearest monitor's work area.
    /// Call this after setting Left/Top/Width/Height but before Show() / ShowDialog().
    /// </summary>
    /// <param name="window">Window to clamp.</param>
    /// <param name="dpiSource">
    /// An already-visible window whose DPI transform is used for logical↔physical conversion.
    /// Pass the owner/main window when <paramref name="window"/> has not yet been shown.
    /// If null the window itself is tried; falls back to 1:1 (96 DPI) if no source is found.
    /// </param>
    public static void EnsureOnScreen(Window window, Window? dpiSource = null)
    {
        var m = GetDpiMatrix(dpiSource ?? window);

        double left   = double.IsNaN(window.Left)   ? 0   : window.Left;
        double top    = double.IsNaN(window.Top)     ? 0   : window.Top;
        double width  = ResolveSize(window.Width,  window.ActualWidth,  800);
        double height = ResolveSize(window.Height, window.ActualHeight, 600);

        // Convert window centre to physical pixels to identify the correct monitor.
        int physCx = (int)Math.Round((left + width  / 2) * m.M11);
        int physCy = (int)Math.Round((top  + height / 2) * m.M22);
        var physWa = NativeMethods.GetWorkAreaForPhysicalPoint(physCx, physCy);

        // Convert work area from physical pixels back to WPF logical DIPs.
        double sx = m.M11 > 0 ? m.M11 : 1;
        double sy = m.M22 > 0 ? m.M22 : 1;
        double waLeft   = physWa.Left   / sx;
        double waTop    = physWa.Top    / sy;
        double waRight  = physWa.Right  / sx;
        double waBottom = physWa.Bottom / sy;
        double waW      = waRight  - waLeft;
        double waH      = waBottom - waTop;

        // Clamp size to fit within work area.
        double newW = Math.Min(width,  waW);
        double newH = Math.Min(height, waH);

        // Clamp position so the window's right/bottom edges stay inside the work area.
        window.Left = Math.Max(waLeft, Math.Min(left, waRight  - newW));
        window.Top  = Math.Max(waTop,  Math.Min(top,  waBottom - newH));
        if (newW < width)  window.Width  = newW;
        if (newH < height) window.Height = newH;
    }

    /// <summary>
    /// Centers <paramref name="window"/> over <paramref name="owner"/> using the owner's actual
    /// on-screen bounds (correct even when the owner is maximized), then clamps the result to
    /// the monitor's work area via <see cref="EnsureOnScreen"/>.
    /// </summary>
    public static void CenterOnOwnerAndEnsureOnScreen(Window window, Window owner)
    {
        var ownerBounds = NativeMethods.GetActualWindowBoundsLogical(owner);
        if (ownerBounds.IsEmpty || ownerBounds.Width <= 0)
        {
            ownerBounds = new Rect(
                owner.Left, owner.Top,
                owner.ActualWidth  > 0 ? owner.ActualWidth  : owner.Width,
                owner.ActualHeight > 0 ? owner.ActualHeight : owner.Height);
        }

        double w = ResolveSize(window.Width,  window.ActualWidth,  800);
        double h = ResolveSize(window.Height, window.ActualHeight, 600);

        window.Left = ownerBounds.Left + (ownerBounds.Width  - w) / 2;
        window.Top  = ownerBounds.Top  + (ownerBounds.Height - h) / 2;
        EnsureOnScreen(window, owner);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static Matrix GetDpiMatrix(Window? w)
    {
        if (w != null)
        {
            var src = PresentationSource.FromVisual(w);
            if (src?.CompositionTarget is { } ct)
                return ct.TransformToDevice;
        }
        return Matrix.Identity;
    }

    private static double ResolveSize(double declared, double actual, double fallback)
    {
        if (!double.IsNaN(declared) && declared > 0) return declared;
        if (actual > 0) return actual;
        return fallback;
    }
}
