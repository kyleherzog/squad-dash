using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SquadDash;

public enum SpinnerActivityKind { Thinking, Writing }

/// <summary>
/// Physics-based activity spinner drawn directly via OnRender.
/// Subscribes to its DataContext (AgentStatusCard) for activity pulses.
/// </summary>
public sealed class ActivitySpinner : FrameworkElement
{
    // Physics state
    private double _angularVelocity;    // rad/s
    private double _angle;              // radians, current rotation
    private double _diameter;           // currently rendered diameter, interpolates 12..18
    private double _targetDiameter;
    private double _writeColorBlend;    // 0=blue, 1=red
    private double _spinnerOpacity;     // 0..1
    private double _satLightPhase;      // radians, for max-speed pulse oscillation

    // Timers
    private readonly DispatcherTimer _physicsTimer;
    private DateTime _lastTick;
    private DateTime _lastWriteEventTime = DateTime.MinValue;

    // Geometry cache
    private StreamGeometry? _cachedGeo;
    private double _cachedGeoDiameter = -1;

    // Physics constants
    private const double MaxAngularVelocity = 20.0;
    private const double FrictionK = 0.11;           // exp(-k*t) → ~6% at 25s
    private const double ThinkingImpulse = 3.5;
    private const double WritingImpulse = 6.0;
    private const double ThinkingDiameter = 12.0;
    private const double WritingDiameter = 18.0;
    private const double WriteColorDecaySeconds = 7.5;
    private const double FadeOutThreshold = 0.18;    // rad/s — start fade below this
    private const double FadeOutDuration = 2.0;      // seconds to fade to invisible
    private const double DiameterLerpRate = 2.5;     // interpolation speed (1/s)
    private const double PulseFrequency = 3.0;       // Hz for max-speed oscillation
    private const double PulseAmplitude = 0.22;      // ±22% brightness

    // Colors
    private static readonly Color ThinkingColor = Color.FromRgb(0x22, 0x88, 0xFF);
    private static readonly Color WritingColor = Color.FromRgb(0xFF, 0x44, 0x22);

    public ActivitySpinner()
    {
        _diameter = ThinkingDiameter;
        _targetDiameter = ThinkingDiameter;
        _spinnerOpacity = 0;
        Visibility = Visibility.Collapsed;

        _physicsTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(1.0 / 60.0)
        };
        _physicsTimer.Tick += OnPhysicsTick;
        _lastTick = DateTime.UtcNow;

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;

        // Subscribe to static theme-changed notifications
        AgentStatusCard.ThemeChanged += OnThemeChanged;
    }

    // ── DataContext binding to AgentStatusCard ──────────────────────────────

    private AgentStatusCard? _subscribedCard;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedCard is not null)
        {
            _subscribedCard.ActivityPulsed -= OnActivityPulsed;
            _subscribedCard = null;
        }
        if (e.NewValue is AgentStatusCard card)
        {
            _subscribedCard = card;
            card.ActivityPulsed += OnActivityPulsed;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _physicsTimer.Stop();
        if (_subscribedCard is not null)
        {
            _subscribedCard.ActivityPulsed -= OnActivityPulsed;
            _subscribedCard = null;
        }
        AgentStatusCard.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Re-subscribe to theme change; just invalidate visual so next render
        // picks up the correct pulse direction.
        if (_physicsTimer.IsEnabled)
            InvalidateVisual();
    }

    // ── Activity pulse entry point ──────────────────────────────────────────

    private void OnActivityPulsed(object? sender, SpinnerActivityKind kind)
    {
        var impulse = kind == SpinnerActivityKind.Writing ? WritingImpulse : ThinkingImpulse;
        _angularVelocity = Math.Min(_angularVelocity + impulse, MaxAngularVelocity);

        if (kind == SpinnerActivityKind.Writing)
            _lastWriteEventTime = DateTime.UtcNow;

        // Snap back to fully visible regardless of fade state
        _spinnerOpacity = 1.0;
        Visibility = Visibility.Visible;

        if (!_physicsTimer.IsEnabled)
        {
            _lastTick = DateTime.UtcNow;
            _physicsTimer.Start();
        }
    }

    // ── Physics tick (≈60 Hz) ───────────────────────────────────────────────

    private void OnPhysicsTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt = Math.Clamp((now - _lastTick).TotalSeconds, 0.001, 0.2);
        _lastTick = now;

        // Friction: exponential decay
        _angularVelocity *= Math.Exp(-FrictionK * dt);
        if (_angularVelocity < 1e-4) _angularVelocity = 0;

        // Advance rotation angle
        _angle += _angularVelocity * dt;
        if (_angle >= Math.PI * 2) _angle -= Math.PI * 2;

        // Write color blend — decays from 1→0 over WriteColorDecaySeconds after last write
        if (_lastWriteEventTime != DateTime.MinValue)
        {
            var secondsSinceWrite = (now - _lastWriteEventTime).TotalSeconds;
            _writeColorBlend = Math.Clamp(1.0 - secondsSinceWrite / WriteColorDecaySeconds, 0.0, 1.0);
        }
        else
        {
            _writeColorBlend = 0;
        }

        // Interpolate diameter smoothly
        _targetDiameter = ThinkingDiameter + (WritingDiameter - ThinkingDiameter) * _writeColorBlend;
        var diameterDelta = (_targetDiameter - _diameter) * Math.Min(1.0, DiameterLerpRate * dt);
        _diameter += diameterDelta;
        if (Math.Abs(_cachedGeoDiameter - _diameter) > 0.15)
            _cachedGeo = null; // invalidate cached geometry

        // Max-speed saturation/lightness pulse
        var speedRatio = _angularVelocity / MaxAngularVelocity;
        if (speedRatio >= 0.85)
            _satLightPhase += 2.0 * Math.PI * PulseFrequency * dt;
        else
            _satLightPhase = 0; // reset so it doesn't jump on re-entry

        // Fade out only after the spinner has nearly stopped
        if (_angularVelocity < FadeOutThreshold)
        {
            _spinnerOpacity = Math.Max(0.0, _spinnerOpacity - dt / FadeOutDuration);
            if (_spinnerOpacity <= 0.0)
            {
                _spinnerOpacity = 0;
                _physicsTimer.Stop();
                Visibility = Visibility.Collapsed;
                return;
            }
        }
        else
        {
            _spinnerOpacity = 1.0;
        }

        InvalidateVisual();
    }

    // ── Layout ──────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize) => new(18, 18);

    protected override Size ArrangeOverride(Size finalSize) => new(18, 18);

    // ── Rendering ───────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_spinnerOpacity <= 0 || _angularVelocity <= 0)
            return;

        var color = LerpColor(ThinkingColor, WritingColor, _writeColorBlend);

        // Apply max-speed pulse
        if (_angularVelocity / MaxAngularVelocity >= 0.85)
        {
            var isDark = AgentStatusCard.IsDarkTheme;
            var pulse = Math.Sin(_satLightPhase) * PulseAmplitude;
            if (!isDark) pulse = -pulse; // darker in light theme
            color = AdjustBrightness(color, pulse);
        }

        // Bake opacity into alpha channel
        color.A = (byte)Math.Round(color.A * _spinnerOpacity);

        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var geo = GetArcGeometry();

        dc.PushTransform(new RotateTransform(_angle * (180.0 / Math.PI), 9.0, 9.0));
        dc.DrawGeometry(brush, null, geo);
        dc.Pop();
    }

    // ── Geometry ────────────────────────────────────────────────────────────

    private StreamGeometry GetArcGeometry()
    {
        if (_cachedGeo is not null)
            return _cachedGeo;

        var center = new Point(9.0, 9.0);
        var outerR = _diameter / 2.0;
        var thickness = Math.Max(2.0, outerR * 0.36);
        var innerR = Math.Max(0.5, outerR - thickness);

        // Arc from top (-90°) sweeping 270° clockwise to the left (180°)
        const double startRad = -Math.PI / 2.0;
        const double sweepRad = 3.0 * Math.PI / 2.0; // 270°
        const double endRad = startRad + sweepRad;    // 180° = left

        var outerStart = PolarToPoint(center, outerR, startRad);
        var outerEnd = PolarToPoint(center, outerR, endRad);
        var innerEnd = PolarToPoint(center, innerR, endRad);
        var innerStart = PolarToPoint(center, innerR, startRad);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(outerStart, isFilled: true, isClosed: true);
            // Outer arc: 270° clockwise (large arc)
            ctx.ArcTo(outerEnd, new Size(outerR, outerR), 0, isLargeArc: true,
                SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(innerEnd, isStroked: false, isSmoothJoin: false);
            // Inner arc: 270° counter-clockwise (large arc) back to start
            ctx.ArcTo(innerStart, new Size(innerR, innerR), 0, isLargeArc: true,
                SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: false);
        }
        geo.Freeze();

        _cachedGeo = geo;
        _cachedGeoDiameter = _diameter;
        return geo;
    }

    private static Point PolarToPoint(Point center, double radius, double angleRad) =>
        new(center.X + radius * Math.Cos(angleRad),
            center.Y + radius * Math.Sin(angleRad));

    // ── Color helpers ────────────────────────────────────────────────────────

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color AdjustBrightness(Color c, double delta)
    {
        static byte Clamp(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
        var factor = 1.0 + delta;
        return Color.FromArgb(c.A, Clamp(c.R * factor), Clamp(c.G * factor), Clamp(c.B * factor));
    }
}
