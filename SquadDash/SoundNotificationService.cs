using System;
using System.IO;
using System.Diagnostics;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SquadDash;

/// <summary>
/// Identifies the application event that triggers a notification sound.
/// </summary>
internal enum SoundEvent
{
    PromptComplete,
    PromptError,
    ApprovalNeeded,
    QueueEmpty,
    LoopIterationComplete,
    LoopStopped,
    CommitMade,
    QuickRepliesShown
}

/// <summary>
/// Plays per-event notification sounds according to the current settings.
/// Safe to call from any thread. Play is fire-and-forget.
/// </summary>
internal sealed class SoundNotificationService
{
    private readonly ApplicationSettingsStore _settingsStore;
    private readonly ITtsProvider?            _ttsProvider;

    // 0 = idle, 1 = speaking; updated via Interlocked for overlap guard.
    private int _ttsSpeaking;

    public SoundNotificationService(
        ApplicationSettingsStore settingsStore,
        ITtsProvider?            ttsProvider = null)
    {
        _settingsStore = settingsStore;
        _ttsProvider   = ttsProvider;
    }

    /// <summary>
    /// Plays the sound for the given event if the event is enabled in settings.
    /// Uses a custom audio file when configured and the file exists; falls back
    /// to <see cref="SystemSounds.Asterisk"/> otherwise.
    /// If the custom path is a quoted phrase (e.g. <c>"Hello world"</c>) the text
    /// is sent to the configured TTS provider instead.
    /// Never blocks the caller — exceptions are swallowed and debug-logged.
    /// </summary>
    public void Play(SoundEvent evt)
    {
        try
        {
            var settings = _settingsStore.Load();
            var (enabled, customPath) = GetEventSettings(settings, evt);

            if (!enabled)
                return;

            // --- TTS path ---
            if (!string.IsNullOrEmpty(customPath) && IsPhrase(customPath))
            {
                if (_ttsProvider == null) return;

                // Skip-if-busy overlap guard: only one TTS utterance at a time.
                if (Interlocked.CompareExchange(ref _ttsSpeaking, 1, 0) != 0) return;

                var phrase = ExtractPhrase(customPath);
                _ = Task.Run(async () =>
                {
                    try   { await _ttsProvider.SpeakAsync(phrase).ConfigureAwait(false); }
                    catch (Exception ex) { Debug.WriteLine($"TTS error: {ex.Message}"); }
                    finally { Interlocked.Exchange(ref _ttsSpeaking, 0); }
                });
                return;
            }

            // --- Audio file path ---
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            {
                // MediaPlayer must run on a thread that has a Dispatcher.
                // BeginInvoke is fire-and-forget — never blocks the caller.
                var capturedPath = customPath;
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var player = new MediaPlayer();
                        player.Open(new Uri(capturedPath, UriKind.Absolute));
                        player.Play();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"SoundNotificationService: MediaPlayer error for {evt}: {ex.Message}");
                    }
                });
            }
            else
            {
                // System sound: plays synchronously but is nearly instantaneous.
                SystemSounds.Asterisk.Play();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SoundNotificationService: Play error for {evt}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>Returns true when <paramref name="s"/> is a double-quoted phrase.</summary>
    private static bool IsPhrase(string s) =>
        s.Length >= 2 && s[0] == '"' && s[^1] == '"';

    /// <summary>Strips the surrounding double-quotes from a phrase string.</summary>
    private static string ExtractPhrase(string s) => s[1..^1];

    private static (bool Enabled, string CustomPath) GetEventSettings(
        ApplicationSettingsSnapshot s, SoundEvent evt) =>
        evt switch
        {
            SoundEvent.PromptComplete        => (s.Sound_PromptComplete_Enabled,        s.Sound_PromptComplete_CustomPath),
            SoundEvent.PromptError           => (s.Sound_PromptError_Enabled,           s.Sound_PromptError_CustomPath),
            SoundEvent.ApprovalNeeded        => (s.Sound_ApprovalNeeded_Enabled,        s.Sound_ApprovalNeeded_CustomPath),
            SoundEvent.QueueEmpty            => (s.Sound_QueueEmpty_Enabled,            s.Sound_QueueEmpty_CustomPath),
            SoundEvent.LoopIterationComplete => (s.Sound_LoopIterationComplete_Enabled, s.Sound_LoopIterationComplete_CustomPath),
            SoundEvent.LoopStopped           => (s.Sound_LoopStopped_Enabled,           s.Sound_LoopStopped_CustomPath),
            SoundEvent.CommitMade            => (s.Sound_CommitMade_Enabled,            s.Sound_CommitMade_CustomPath),
            SoundEvent.QuickRepliesShown     => (s.Sound_QuickRepliesShown_Enabled,     s.Sound_QuickRepliesShown_CustomPath),
            _                                => (false, "")
        };
}
