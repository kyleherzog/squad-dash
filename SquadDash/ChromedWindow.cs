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
    /// Height of the floating close (✕) button.  Subclasses should ensure their
    /// top-most content row starts at least this many pixels below the border edge
    /// so the close button does not cover interactive controls.
    /// </summary>
    protected const double CloseButtonHeight = 34;

    /// <summary>
    /// Applies the standard SquadDash custom chrome to the window.
    /// </summary>
    /// <param name="captionHeight">
    /// Height of the drag-to-move caption strip at the top. Default 36px suits
    /// windows with a full header row. Use a smaller value (e.g. 28) for compact
    /// ToolWindow-style popups.
    /// </param>
    /// <param name="resizeMode">Defaults to CanResizeWithGrip.</param>
    /// <param name="resizeBorderThickness">
    /// Width of the invisible resize hit-test border. Default 6. Pass 0 for
    /// non-resizable windows (ResizeMode.NoResize) to suppress the hit-test region.
    /// </param>
    protected ChromedWindow(
        double     captionHeight          = 36,
        ResizeMode resizeMode             = ResizeMode.CanResizeWithGrip,
        double     resizeBorderThickness  = 6) {

        WindowStyle        = WindowStyle.None;
        AllowsTransparency = true;
        Background         = Brushes.Transparent;
        ResizeMode         = resizeMode;

        WindowChrome.SetWindowChrome(this, new WindowChrome {
            CaptionHeight         = captionHeight,
            ResizeBorderThickness = new Thickness(resizeBorderThickness),
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
    /// <param name="backgroundResource">
    /// Resource key for the border background. Defaults to <c>"AppSurface"</c>.
    /// Pass a different key (e.g. <c>"PopupSurface"</c>) for windows that use
    /// an alternative surface colour.
    /// </param>
    protected Border ApplyOuterBorder(string backgroundResource = "AppSurface") {
        var border = new Border {
            BorderThickness = new Thickness(1.5),
            CornerRadius    = new CornerRadius(4),
        };
        border.SetResourceReference(Border.BackgroundProperty,  backgroundResource);
        border.SetResourceReference(Border.BorderBrushProperty, "PanelBorder");

        // Close button floats in the top-right corner of the caption strip.
        var closeBtnText = new TextBlock {
            Text                = "\u2715",
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        closeBtnText.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeBody");

        var closeBtn = new Button {
            Width               = 38,
            Height              = CloseButtonHeight,
            Padding             = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Top,
            ToolTip             = "Close",
            Content             = closeBtnText,
        };
        closeBtn.SetResourceReference(Button.StyleProperty, "CaptionCloseButtonStyle");
        WindowChrome.SetIsHitTestVisibleInChrome(closeBtn, true);
        closeBtn.Click += (_, _) => Close();

        // Overlay grid: border fills the whole window area; close button is layered on top.
        var overlay = new Grid();
        overlay.Children.Add(border);
        overlay.Children.Add(closeBtn);

        Content = overlay;
        return border;
    }
}
