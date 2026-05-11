using System;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace SquadDash;

/// <summary>
/// Plays a sound when an agent completes a prompt turn.
/// Uses the Windows Notification system sound by default, or a custom .mp3/.wav file.
/// </summary>
internal sealed class CompletionSoundService {
    // SND_ALIAS plays a named Windows sound event; SND_ASYNC plays without blocking.
    private const uint SND_ALIAS = 0x00010000;
    private const uint SND_ASYNC = 0x00000001;
    private const uint SND_NODEFAULT = 0x00000002;

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string? lpszSound, IntPtr hModule, uint dwFlags);

    private ApplicationSettingsSnapshot _settings;

    public CompletionSoundService(ApplicationSettingsSnapshot settings) {
        _settings = settings;
    }

    public void UpdateSettings(ApplicationSettingsSnapshot settings) {
        _settings = settings;
    }

    public void PlayCompletionSound() {
        if (!_settings.CompletionSoundEnabled)
            return;

        var customPath = _settings.CompletionSoundFilePath;

        if (string.IsNullOrWhiteSpace(customPath)) {
            PlaySystemNotificationSound();
            return;
        }

        try {
            if (customPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) {
                using var player = new SoundPlayer(customPath);
                player.Play();
            } else {
                var player = new MediaPlayer();
                player.Open(new Uri(customPath, UriKind.Absolute));
                player.Play();
            }
        } catch (Exception) {
            // Fall back to system sound if custom file fails
            PlaySystemNotificationSound();
        }
    }

    private static void PlaySystemNotificationSound() {
        // Play the Windows "Notification" system sound event asynchronously.
        // Falls back silently if the sound is not configured on this system.
        PlaySound("SystemNotification", IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_NODEFAULT);
    }
}
