using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace SquadDash;

/// <summary>
/// Attaches push-to-talk (PTT) voice dictation to a <see cref="RichTextBox"/>.
/// Mirrors <see cref="PttTextBoxAttachment"/> but uses <see cref="TextPointer"/>-based
/// insertion rather than character-index APIs.
/// </summary>
internal sealed class PttRichTextBoxAttachment : IDisposable {

    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly Func<ApplicationSettingsSnapshot> _settingsProvider;
    private readonly Window     _ownerWindow;
    private readonly Dispatcher _dispatcher;

    // ── Per-session state ─────────────────────────────────────────────────────
    private ISpeechRecognitionService? _service;
    private PushToTalkWindow?          _pttWindow;
    private bool                       _stopOnCtrlRelease;
    private RichTextBox?               _target;
    private bool                       _firstPhrase;

    // ── Gesture tracker ───────────────────────────────────────────────────────
    private readonly CtrlDoubleTapGestureTracker _gesture =
        new(maxTapHoldMs: 250, doubleTapGapMs: 350);

    internal bool IsActive => _service is not null || _pttWindow is not null;

    // ── Constructor ────────────────────────────────────────────────────────────
    internal PttRichTextBoxAttachment(
        Func<ApplicationSettingsSnapshot> settingsProvider,
        Window ownerWindow,
        Dispatcher dispatcher) {
        _settingsProvider = settingsProvider;
        _ownerWindow      = ownerWindow;
        _dispatcher       = dispatcher;
    }

    // ── Key event handlers ────────────────────────────────────────────────────

    /// <summary>
    /// Call from the host window's PreviewKeyDown when <paramref name="activeBox"/> has keyboard focus.
    /// Returns <c>true</c> when the event was consumed — the caller should set <c>e.Handled = true</c>.
    /// </summary>
    internal bool HandlePreviewKeyDown(KeyEventArgs e, RichTextBox? activeBox) {
        if (activeBox is null) return false;

        var action = _gesture.HandleKeyDown(e.Key, e.IsRepeat, DateTime.UtcNow);
        if (action != CtrlDoubleTapGestureAction.Triggered) return false;

        if (_service is null) {
            _stopOnCtrlRelease = true;
            _ = StartAsync(activeBox);
        }
        else {
            _ = StopAsync();
        }
        return true;
    }

    /// <summary>
    /// Call from the host window's PreviewKeyUp.
    /// Returns <c>true</c> when the event was consumed (set <c>e.Handled = true</c>).
    /// </summary>
    internal bool HandlePreviewKeyUp(KeyEventArgs e) {
        if (!CtrlDoubleTapGestureTracker.IsCtrlKey(e.Key)) return false;

        if (_stopOnCtrlRelease && IsActive) {
            _ = StopAsync();
            return true;
        }

        _gesture.HandleKeyUp(e.Key, DateTime.UtcNow);
        return false;
    }

    // ── PTT session lifecycle ─────────────────────────────────────────────────

    internal async Task StartAsync(RichTextBox target) {
        var settings = _settingsProvider();
        string key, region;
        if (settings.SpeechProvider == SpeechProvider.OpenAI) {
            key    = settings.OpenAiSpeechApiKey ?? string.Empty;
            region = string.Empty;
        }
        else {
            key    = Environment.GetEnvironmentVariable("SQUAD_SPEECH_KEY", EnvironmentVariableTarget.User) ?? string.Empty;
            region = settings.SpeechRegion ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(key) ||
            (settings.SpeechProvider == SpeechProvider.Azure && string.IsNullOrWhiteSpace(region))) {
            _stopOnCtrlRelease = false;
            _gesture.Reset();
            return;
        }

        _target      = target;
        _firstPhrase = true;

        _service = settings.SpeechProvider == SpeechProvider.OpenAI
            ? new WhisperSpeechRecognitionService()
            : new AzureSpeechRecognitionService();

        _service.PhraseRecognized += (_, text) =>
            _dispatcher.BeginInvoke(() => AppendSpeech(text));

        _service.VolumeChanged += (_, level) =>
            _dispatcher.BeginInvoke(DispatcherPriority.Render, () => {
                if (_pttWindow is not null)
                    _pttWindow.VolumeBar.Height = Math.Max(2, level * 36);
            });

        _service.RecognitionError += (_, _) =>
            _dispatcher.BeginInvoke(() => _ = StopAsync());

        try {
            System.Windows.Point physicalPt;
            try {
                var rect = target.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
                physicalPt = target.PointToScreen(new System.Windows.Point(rect.Right, rect.Bottom));
            }
            catch {
                physicalPt = target.PointToScreen(new System.Windows.Point(0, target.ActualHeight + 4));
            }

            var physWa          = NativeMethods.GetWorkAreaForPhysicalPoint((int)physicalPt.X, (int)physicalPt.Y);
            var logicalPt       = DpiHelper.PhysicalToLogical(target, physicalPt);
            var logicalWaOrigin = DpiHelper.PhysicalToLogical(target, new System.Windows.Point(physWa.Left, physWa.Top));
            var logicalWaCorner = DpiHelper.PhysicalToLogical(target, new System.Windows.Point(physWa.Right, physWa.Bottom));
            var logicalWorkArea = new System.Windows.Rect(logicalWaOrigin, logicalWaCorner);

            _pttWindow = new PushToTalkWindow(_ownerWindow, showHint: false);
            _pttWindow.PositionUnderCaret(logicalPt, logicalWorkArea);
            _pttWindow.Show();
            target.Focus();

            await _service.StartAsync(key, region, language: settings.SpeechLanguage).ConfigureAwait(false);
        }
        catch {
            await _dispatcher.InvokeAsync(() => {
                _pttWindow?.Close();
                _pttWindow = null;
            });
            _service?.Dispose();
            _service           = null;
            _target            = null;
            _stopOnCtrlRelease = false;
            _gesture.Reset();
        }
    }

    internal async Task StopAsync() {
        await _dispatcher.InvokeAsync(() => {
            _pttWindow?.Close();
            _pttWindow = null;
        });

        var service = _service;
        _service = null;

        if (service is not null) {
            try { await service.StopAsync().ConfigureAwait(false); } catch { }
            service.Dispose();
        }

        await _dispatcher.InvokeAsync(() => _target = null);

        _stopOnCtrlRelease = false;
        _gesture.Reset();
    }

    // ── Speech text insertion ─────────────────────────────────────────────────

    private void AppendSpeech(string text) {
        var rtb = _target;
        if (rtb is null) return;

        var doc      = rtb.Document;
        var caretPos = rtb.CaretPosition;

        // Get text to the left of the caret for heuristics context
        var left  = new TextRange(doc.ContentStart, caretPos).Text;

        // On the first phrase, replace any existing selection; subsequent phrases insert at caret
        var replaceEnd = (_firstPhrase && !rtb.Selection.IsEmpty) ? rtb.Selection.End : caretPos;
        _firstPhrase = false;

        var right = new TextRange(replaceEnd, doc.ContentEnd).Text;

        var prefix    = VoiceInsertionHeuristics.LeadingInsertionSpace(left, right);
        var processed = VoiceInsertionHeuristics.Apply(left, text, right);
        var insert    = prefix + processed;
        var rules     = _settingsProvider().VoiceReplacementRules;
        var final     = rules.Count > 0
            ? prefix + VoiceInsertionHeuristics.ApplyReplacementRules(processed, rules)
            : insert;

        rtb.Selection.Select(caretPos, replaceEnd);
        rtb.Selection.Text = final;
    }

    // ── IDisposable ────────────────────────────────────────────────────────────

    public void Dispose() {
        var service = _service;
        _service = null;
        service?.Dispose();

        _dispatcher.InvokeAsync(() => {
            _pttWindow?.Close();
            _pttWindow = null;
        });
    }
}
