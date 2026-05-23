using System.Text.Json;
using System.Text.RegularExpressions;

namespace SquadDash;

internal static class TranscriptTextUtilities
{
    internal static string SanitizeResponseText(string? text) =>
        StripInboxMessageBlock(StripHostCommandBlock(StripAwaitInputSentinel(ToolTranscriptFormatter.StripSystemNotifications(text)))).TrimEnd();

    internal static string? SanitizeResponseTextOrNull(string? text)
    {
        var sanitized = SanitizeResponseText(text);
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    internal static string GetSanitizedTurnResponseText(TranscriptTurnView? turn) =>
        SanitizeResponseText(turn?.ResponseTextBuilder.ToString());

    internal static string FormatThinkingText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        normalized = Regex.Replace(normalized, @"(?<=\w)\s+'(?=\w)", "'");
        normalized = Regex.Replace(
            normalized,
            @"(?<=[A-Za-z]{4,})\s+(?=(?:ize|ized|ization|ise|ised|ises|ing|ed|er|ers|ly|ment|ments|tion|tions|able|ible|ality|ality|ities|ity)\b)",
            string.Empty,
            RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+([,.;:!?%\)\]\}])", "$1");
        normalized = Regex.Replace(normalized, @"([\(\[\{])\s+", "$1");
        return normalized;
    }

    internal static string BuildThreadPreview(string text)
    {
        var collapsed = CollapseWhitespace(RemoveQuickReplySuffix(SanitizeResponseText(text)));
        if (collapsed.Length <= 120)
            return collapsed;

        return collapsed[..117] + "...";
    }

    internal static string BuildTimedStatusText(
        string? statusText,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset now)
    {
        var status = AgentThreadRegistry.HumanizeThreadStatus(statusText);
        if (string.IsNullOrWhiteSpace(status))
            status = completedAt is null ? "Running" : "Completed";

        var effectiveStartedAt = startedAt ?? completedAt ?? now;
        return StatusTimingPresentation.BuildStatus(status, effectiveStartedAt, completedAt, now);
    }

    internal static string CollapseWhitespace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    internal static string FormatJson(JsonElement element)
    {
        return JsonSerializer.Serialize(element, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string StripHostCommandBlock(string text)
    {
        if (HostCommandParser.TryExtract(text, out var body, out _))
            return body;
        return text;
    }

    private static string StripInboxMessageBlock(string text)
    {
        // Strip complete block (already handled by parser).
        if (InboxMessageParser.TryExtract(text, out var body, out _))
            return body;

        // Strip partial block (still streaming), but only when the sentinel is on its
        // own top-level line. Inline references and code-fenced examples must remain visible.
        var sentinelIdx = FindTopLevelInboxSentinelIndex(text);
        if (sentinelIdx >= 0)
            return text[..sentinelIdx].TrimEnd();

        return text;
    }

    private static int FindTopLevelInboxSentinelIndex(string text)
    {
        const string sentinel = "INBOX_MESSAGE_JSON:";

        var inFence = false;
        var offset  = 0;

        while (offset < text.Length)
        {
            var lineEnd = text.IndexOf('\n', offset);
            if (lineEnd < 0)
                lineEnd = text.Length;

            var lineLength = lineEnd - offset;
            if (lineLength > 0 && text[offset + lineLength - 1] == '\r')
                lineLength--;

            var line = text.Substring(offset, lineLength);
            var leadingWhitespace = line.Length - line.TrimStart().Length;
            var trimmed = line[leadingWhitespace..];

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
                inFence = !inFence;
            else if (!inFence && trimmed.StartsWith(sentinel, StringComparison.Ordinal))
                return offset + leadingWhitespace;

            offset = lineEnd == text.Length ? text.Length : lineEnd + 1;
        }

        return -1;
    }

    private static string StripAwaitInputSentinel(string text) =>
        text.Replace(PromptExecutionController.QueueAwaitInputSentinel, string.Empty,
                     StringComparison.Ordinal);

    private static string RemoveQuickReplySuffix(string text) =>
        QuickReplyOptionParser.TryExtract(text, out var body, out _) ? body : text;
}
