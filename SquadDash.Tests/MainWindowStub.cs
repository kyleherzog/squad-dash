using System;

namespace SquadDash;

// Minimal stand-in for MainWindow static members called by PromptExecutionController
// and other production classes. MainWindow.xaml.cs is intentionally excluded
// from the test project, so we supply only the static surface those classes need.
internal static class MainWindow {
    internal static readonly string[] UniverseSelectorOptions = [
        SquadInstallerService.SquadDashUniverseName,
        "Star Wars", "The Matrix", "Alien", "Firefly",
        "Ocean's Eleven", "The Simpsons", "Marvel Cinematic Universe",
        "Breaking Bad", "Futurama"
    ];

    internal static string BuildTimedStatusText(
        string? status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset now) => status ?? string.Empty;

    internal static string BuildThreadPreview(string text) => text;

    internal static string GetSanitizedTurnResponseText(TranscriptTurnView? turn)
        => turn?.ResponseTextBuilder.ToString() ?? string.Empty;

    internal static string FormatThinkingText(string text) => text;

    internal static string SanitizeResponseText(string text) => text;

    internal static string? SanitizeResponseTextOrNull(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.TrimEnd();
}
