using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SquadDash;

internal static class LoopMdParser {

    private static readonly Regex _kvPattern =
        new(@"^(\w+):\s*(.+)$", RegexOptions.Compiled);

    /// <summary>
    /// Parses a loop.md file and returns configuration.
    /// Returns null if the file does not exist, cannot be read,
    /// or the frontmatter does not contain <c>configured: true</c>.
    /// </summary>
    public static LoopMdConfig? Parse(string loopMdPath) {
        if (!File.Exists(loopMdPath))
            return null;

        string content;
        try {
            content = File.ReadAllText(loopMdPath);
        }
        catch {
            return null;
        }

        // Normalise line endings so CRLF and LF both work.
        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        int i = 0;

        // Skip until first ---
        while (i < lines.Length && lines[i].Trim() != "---")
            i++;
        if (i >= lines.Length)
            return null;
        i++; // move past the opening ---

        bool configured = false;
        double intervalMinutes = 10;
        double timeoutMinutes  = 5;
        string description     = "";
        var    commands        = new List<string>();

        // Read key: value pairs up to the closing ---
        while (i < lines.Length && lines[i].Trim() != "---") {
            var m = _kvPattern.Match(lines[i]);
            if (m.Success) {
                var key   = m.Groups[1].Value.ToLowerInvariant();
                var value = m.Groups[2].Value.Trim();
                switch (key) {
                    case "configured":
                        configured = string.Equals(
                            value.Trim('"', '\''),
                            "true",
                            StringComparison.OrdinalIgnoreCase);
                        break;
                    case "interval":
                        if (double.TryParse(
                                value,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var iv))
                            intervalMinutes = iv;
                        break;
                    case "timeout":
                        if (double.TryParse(
                                value,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var tv))
                            timeoutMinutes = tv;
                        break;
                    case "description":
                        description = value.Trim('"', '\'');
                        break;
                    case "commands":
                        // Accepts: [stop_loop, start_loop] or "stop_loop, start_loop"
                        var raw = value.Trim('[', ']', '"', '\'');
                        foreach (var cmd in raw.Split(',')) {
                            var c = cmd.Trim();
                            if (!string.IsNullOrEmpty(c))
                                commands.Add(c);
                        }
                        break;
                }
            }
            i++;
        }

        if (!configured)
            return null;

        // Everything after the closing --- is the instructions body.
        if (i >= lines.Length)
            return new LoopMdConfig(intervalMinutes, timeoutMinutes, description, "", commands);

        i++; // move past the closing ---

        var sb = new StringBuilder();
        for (; i < lines.Length; i++) {
            sb.AppendLine(lines[i]);
        }

        return new LoopMdConfig(
            intervalMinutes,
            timeoutMinutes,
            description,
            sb.ToString().Trim(),
            commands);
    }

    /// <summary>
    /// Scans <paramref name="squadFolder"/> for all loop*.md files and returns
    /// display entries sorted with loop.md first, then alphabetically.
    /// Display name = Description from frontmatter, or filename without extension as fallback.
    /// </summary>
    public static IReadOnlyList<LoopFileEntry> ScanForLoopFiles(string squadFolder)
    {
        if (!Directory.Exists(squadFolder))
            return Array.Empty<LoopFileEntry>();

        var files = Directory.GetFiles(squadFolder, "loop*.md");
        var entries = new List<LoopFileEntry>(files.Length);
        foreach (var path in files)
        {
            var config = Parse(path);
            string rawDescription = config is { Description.Length: > 0 }
                ? config.Description
                : Path.GetFileNameWithoutExtension(path);

            string displayName;
            string tooltipText = "";
            var dashIdx = rawDescription.IndexOfAny(['-', '\u2013', '\u2014', '\u2012', '\u2015']);
            if (dashIdx > 0)
            {
                displayName = rawDescription[..dashIdx].Trim();
                tooltipText = rawDescription[(dashIdx + 1)..].Trim();
            }
            else
            {
                displayName = rawDescription;
            }

            entries.Add(new LoopFileEntry(path, displayName, tooltipText));
        }

        entries.Sort((a, b) =>
        {
            bool aIsDefault = string.Equals(Path.GetFileName(a.FilePath), "loop.md", StringComparison.OrdinalIgnoreCase);
            bool bIsDefault = string.Equals(Path.GetFileName(b.FilePath), "loop.md", StringComparison.OrdinalIgnoreCase);
            if (aIsDefault != bIsDefault) return aIsDefault ? -1 : 1;
            return string.Compare(Path.GetFileName(a.FilePath), Path.GetFileName(b.FilePath), StringComparison.OrdinalIgnoreCase);
        });

        return entries;
    }
}

internal sealed record LoopFileEntry(string FilePath, string DisplayName, string TooltipText = "");
