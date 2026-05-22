using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace SquadDash;

/// <summary>
/// Base class for SquadDash popup windows that use the custom owner-drawn chrome
/// (WindowStyle.None + WindowChrome + themed outer border) instead of the
/// standard OS title bar.
/// </summary>
internal class ChromedWindow : Window {

    /// <summary>
    /// Applies the standard SquadDash custom chrome to the window.
    /// </summary>
    /// <param name="captionHeight">
    /// Height of the drag-to-move caption strip at the top. Default 36px suits
    /// windows with a full header row. Use a smaller value (e.g. 28) for compact
    /// ToolWindow-style popups.
    /// </param>
    /// <param name="resizeMode">Defaults to CanResizeWithGrip.</param>
    protected ChromedWindow(
        double     captionHeight = 36,
        ResizeMode resizeMode    = ResizeMode.CanResizeWithGrip) {

        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        Background         = Brushes.Transparent;
        ResizeMode         = resizeMode;

        WindowChrome.SetWindowChrome(this, new WindowChrome {
            CaptionHeight         = captionHeight,
            ResizeBorderThickness = new Thickness(4),
            GlassFrameThickness   = new Thickness(0),
            UseAeroCaptionButtons = false,
        });

        SourceInitialized += (_, _) =>
            NativeMethods.DisableRoundedCorners(new WindowInteropHelper(this).Handle);
    }

    /// <summary>
    /// Creates the standard themed outer border, sets it as the window Content,
    /// and returns it so the subclass can set its Child.
    /// Call this once in the subclass constructor after setting window dimensions.
    /// </summary>
    protected Border ApplyOuterBorder() {
        var border = new Border {
            BorderThickness = new Thickness(1.5),
            CornerRadius    = new CornerRadius(4),
        };
        border.SetResourceReference(Border.BackgroundProperty,    "AppSurface");
        border.SetResourceReference(Border.BorderBrushProperty,   "PanelBorder");
        Content = border;
        return border;
    }
}
