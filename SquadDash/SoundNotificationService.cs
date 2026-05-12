using System;
using System.IO;
using System.Collections.Concurrent;
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
///
/// Multiple events fired within <see cref="CoalesceWindowMs"/> of each other
/// are coalesced: only the highest-priority enabled event is played.
/// Priority order (1 = highest): QuickRepliesShown, ApprovalNeeded, CommitMade,
/// PromptComplete, PromptError, LoopStopped, LoopIterationComplete, QueueEmpty.
/// </summary>
internal sealed class SoundNotificationService
{
    private const int CoalesceWindowMs = 50;

    private readonly ApplicationSettingsStore    _settingsStore;
    private readonly Func<ITtsProvider?>         _ttsProviderFactory;
    private readonly ConcurrentQueue<SoundEvent> _pending = new();

    // 0 = idle, 1 = drain scheduled; updated via Interlocked.
    private int _drainScheduled;
    // 0 = idle, 1 = speaking; updated via Interlocked for TTS overlap guard.
    private int _ttsSpeaking;

    // Holds active MediaPlayer instances to prevent GC collection mid-playback.
    private readonly System.Collections.Generic.HashSet<MediaPlayer> _activePlayers = new();

    public SoundNotificationService(
        ApplicationSettingsStore settingsStore,
        Func<ITtsProvider?>      ttsProviderFactory)
    {
        _settingsStore      = settingsStore;
        _ttsProviderFactory = ttsProviderFactory;
    }

    /// <summary>
    /// Queues the sound for the given event. If no drain is already scheduled,
    /// schedules one after <see cref="CoalesceWindowMs"/> ms. The drain picks
    /// the highest-priority enabled event from all queued events and plays it.
    /// Never blocks the caller — exceptions are swallowed and debug-logged.
    /// </summary>
    public void Play(SoundEvent evt)
    {
        _pending.Enqueue(evt);
        if (Interlocked.CompareExchange(ref _drainScheduled, 1, 0) == 0)
        {
            _ = Task.Delay(CoalesceWindowMs).ContinueWith(
                _ => DrainAndPlay(),
                TaskScheduler.Default);
        }
    }

    // ---------------------------------------------------------------------------
    // Private — coalescing drain
    // ---------------------------------------------------------------------------

    private void DrainAndPlay()
    {
        Interlocked.Exchange(ref _drainScheduled, 0);

        var settings = _settingsStore.Load();

        // Collect all pending events (deduplicated).
        var seen = new System.Collections.Generic.HashSet<SoundEvent>();
        while (_pending.TryDequeue(out var e)) seen.Add(e);
        if (seen.Count == 0) return;

        if (seen.Count > 1)
        {
            var names = string.Join(", ", seen);
            SquadDashTrace.Write("Sound", $"Coalescing {seen.Count} simultaneous events: [{names}]");
        }

        // Pick the highest-priority event that is actually enabled.
        SoundEvent? best = null;
        int         bestPri = int.MaxValue;
        foreach (var evt in seen)
        {
            var (enabled, _) = GetEventSettings(settings, evt);
            if (!enabled) continue;
            var pri = GetPriority(evt);
            if (pri < bestPri) { bestPri = pri; best = evt; }
        }

        if (best is null)
        {
            SquadDashTrace.Write("Sound", "All coalesced events disabled — nothing to play");
            return;
        }

        if (seen.Count > 1)
            SquadDashTrace.Write("Sound", $"Coalesced winner: {best}");

        PlayImmediate(best.Value, settings);
    }

    // ---------------------------------------------------------------------------
    // Private — immediate playback (called after coalescing)
    // ---------------------------------------------------------------------------

    private void PlayImmediate(SoundEvent evt, ApplicationSettingsSnapshot settings)
    {
        try
        {
            var (enabled, customPath) = GetEventSettings(settings, evt);

            SquadDashTrace.Write("Sound", $"Play attempt: {evt} enabled={enabled}");

            if (!enabled)
            {
                SquadDashTrace.Write("Sound", $"Play skipped: {evt} is disabled");
                return;
            }

            // --- TTS path ---
            if (!string.IsNullOrEmpty(customPath) && IsPhrase(customPath))
            {
                var ttsProvider = _ttsProviderFactory();
                if (ttsProvider == null)
                {
                    SquadDashTrace.Write("Sound", $"Play skipped: {evt} TTS phrase configured but no TTS provider");
                    return;
                }

                // Skip-if-busy overlap guard: only one TTS utterance at a time.
                if (Interlocked.CompareExchange(ref _ttsSpeaking, 1, 0) != 0)
                {
                    SquadDashTrace.Write("Sound", $"Play skipped: {evt} TTS already speaking");
                    return;
                }

                var phrase = ExtractPhrase(customPath);
                SquadDashTrace.Write("Sound", $"Playing {evt} via TTS: \"{phrase}\"");
                _ = Task.Run(async () =>
                {
                    try   { await ttsProvider.SpeakAsync(phrase).ConfigureAwait(false); }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"TTS error: {ex.Message}");
                        SquadDashTrace.Write("Sound", $"TTS failed for {evt}: {ex.Message}");
                    }
                    finally { Interlocked.Exchange(ref _ttsSpeaking, 0); }
                });
                return;
            }

            // --- Audio file path ---
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            {
                var capturedPath = customPath;
                SquadDashTrace.Write("Sound", $"Playing {evt} via file: {capturedPath}");
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var player = new MediaPlayer();
                        // Root the player in _activePlayers so the GC cannot collect it
                        // mid-playback. The self-referencing event-handler cycle alone is
                        // not enough — the GC can break it.
                        lock (_activePlayers) { _activePlayers.Add(player); }
                        player.MediaOpened += (_, _) => player.Play();
                        player.MediaFailed += (_, args) =>
                        {
                            SquadDashTrace.Write("Sound", $"MediaPlayer failed for {evt}: {args.ErrorException?.Message}");
                            lock (_activePlayers) { _activePlayers.Remove(player); }
                            player.Close();
                        };
                        player.MediaEnded += (_, _) =>
                        {
                            lock (_activePlayers) { _activePlayers.Remove(player); }
                            player.Close();
                        };
                        player.Open(new Uri(capturedPath, UriKind.Absolute));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"SoundNotificationService: MediaPlayer error for {evt}: {ex.Message}");
                        SquadDashTrace.Write("Sound", $"File playback failed for {evt}: {ex.Message}");
                    }
                });
            }
            else
            {
                SquadDashTrace.Write("Sound", $"Playing {evt} via system sound (Asterisk)");
                SystemSounds.Asterisk.Play();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SoundNotificationService: Play error for {evt}: {ex.Message}");
            SquadDashTrace.Write("Sound", $"Play error for {evt}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Priority order for coalescing. Lower number = higher priority.
    /// </summary>
    private static int GetPriority(SoundEvent evt) => evt switch
    {
        SoundEvent.QuickRepliesShown     => 1,
        SoundEvent.ApprovalNeeded        => 2,
        SoundEvent.CommitMade            => 3,
        SoundEvent.PromptComplete        => 4,
        SoundEvent.PromptError           => 5,
        SoundEvent.LoopStopped           => 6,
        SoundEvent.LoopIterationComplete => 7,
        SoundEvent.QueueEmpty            => 8,
        _                                => 99
    };

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

