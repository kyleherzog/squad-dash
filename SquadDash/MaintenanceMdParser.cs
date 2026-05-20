using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SquadDash;

/// <summary>
/// Parses a maintenance.md file. Tasks are embedded as a YAML list inside the frontmatter.
/// </summary>
internal static class MaintenanceMdParser {

    /// <summary>
    /// Returns null if the file is missing, unreadable, or the frontmatter lacks
    /// <c>configured: true</c>.
    /// </summary>
    public static MaintenanceMdConfig? Parse(string maintenanceMdPath) {
        if (!File.Exists(maintenanceMdPath))
            return null;

        string content;
        try {
            content = File.ReadAllText(maintenanceMdPath);
        }
        catch {
            return null;
        }

        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        int i = 0;

        // Skip to opening ---
        while (i < lines.Length && lines[i].Trim() != "---")
            i++;
        if (i >= lines.Length) return null;
        i++;

        bool   configured  = false;
        double idleTimeout = 15;
        int    maxTasks    = 5;
        string safety      = "branch";

        var tasks                      = new List<MaintenanceTaskBuilder>();
        bool inTasksList               = false;
        MaintenanceTaskBuilder? current = null;
        bool inOptionsBlock            = false;
        string? currentOptionKey       = null;
        var optionKeys                 = new List<string>();
        var optionBuilders             = new Dictionary<string, LoopOptionBuilder>(StringComparer.Ordinal);

        while (i < lines.Length && lines[i].Trim() != "---") {
            var line = lines[i];
            i++;

            // Count leading spaces
            int indent = CountLeadingSpaces(line);
            string trimmed = line.TrimStart();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!inTasksList) {
                // Global frontmatter key at column 0
                if (trimmed == "tasks:") {
                    inTasksList = true;
                    continue;
                }

                ParseGlobalKV(trimmed, ref configured, ref idleTimeout, ref maxTasks, ref safety);
                continue;
            }

            // ── inside tasks list ─────────────────────────────────────────────

            // New task item: "  - id: xxx"  (indent 2, followed by "- ")
            if (indent == 2 && trimmed.StartsWith("- ")) {
                FinalizeCurrentTask(current, optionKeys, optionBuilders, tasks);
                current          = new MaintenanceTaskBuilder();
                inOptionsBlock   = false;
                currentOptionKey = null;
                optionKeys.Clear();
                optionBuilders.Clear();

                // Parse first key-value on the same line, e.g. "id: cleanup"
                ParseTaskKV(trimmed[2..], current);
                continue;
            }

            if (current is null) continue;

            // Task field: indent == 4
            if (indent == 4) {
                if (trimmed == "options:" || trimmed.StartsWith("options: ")) {
                    inOptionsBlock   = true;
                    currentOptionKey = null;
                }
                else {
                    inOptionsBlock = false;
                    ParseTaskKV(trimmed, current);
                }
                continue;
            }

            // Option key: indent == 6  e.g. "strategy:"
            if (inOptionsBlock && indent == 6) {
                if (trimmed.EndsWith(':') && !trimmed.Contains(' ')) {
                    currentOptionKey = trimmed.TrimEnd(':');
                    if (!optionBuilders.ContainsKey(currentOptionKey)) {
                        optionBuilders[currentOptionKey] = new LoopOptionBuilder { Key = currentOptionKey };
                        optionKeys.Add(currentOptionKey);
                    }
                }
                continue;
            }

            // Option sub-field: indent == 8  e.g. "type: radio"
            if (inOptionsBlock && currentOptionKey is not null && indent == 8) {
                if (optionBuilders.TryGetValue(currentOptionKey, out var builder))
                    ParseOptionSubfield(trimmed, builder);
                continue;
            }
        }

        FinalizeCurrentTask(current, optionKeys, optionBuilders, tasks);

        if (!configured) return null;

        var builtTasks = tasks
            .Select(t => t.Build(safety))
            .ToList();

        return new MaintenanceMdConfig(idleTimeout, maxTasks, safety, builtTasks);
    }

    // ── Safety floor enforcement ───────────────────────────────────────────────

    /// <summary>
    /// Enforces the safety floor. A per-task safety may not be more permissive
    /// than the global safety setting.
    /// </summary>
    internal static string EnforceSafetyFloor(string globalSafety, string taskSafety) {
        if (string.IsNullOrEmpty(taskSafety))
            return globalSafety;

        return globalSafety switch {
            "report-only" => "report-only",
            "branch"      => taskSafety == "direct" ? "branch" : taskSafety,
            _             => taskSafety,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void FinalizeCurrentTask(
        MaintenanceTaskBuilder? current,
        List<string> optionKeys,
        Dictionary<string, LoopOptionBuilder> optionBuilders,
        List<MaintenanceTaskBuilder> tasks) {

        if (current is null) return;

        if (optionKeys.Count > 0) {
            current.BuiltOptions = optionKeys
                .Select(k => {
                    var b = optionBuilders[k];
                    return new LoopOption(b.Key, b.RawValue ?? "", b.Type ?? "string",
                        b.Label, b.Hint, b.Choices);
                })
                .ToList();
        }

        tasks.Add(current);
    }

    private static void ParseGlobalKV(
        string line,
        ref bool configured, ref double idleTimeout, ref int maxTasks, ref string safety) {

        var colonIdx = line.IndexOf(':');
        if (colonIdx < 0) return;
        var key = line[..colonIdx].Trim();
        var val = line[(colonIdx + 1)..].Trim().Trim('"', '\'');

        switch (key) {
            case "configured":
                configured = string.Equals(val, "true", System.StringComparison.OrdinalIgnoreCase);
                break;
            case "idle_timeout":
                if (double.TryParse(val,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var tv))
                    idleTimeout = tv;
                break;
            case "max_tasks_per_session":
                if (int.TryParse(val,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var mv))
                    maxTasks = mv;
                break;
            case "safety":
                safety = val;
                break;
        }
    }

    private static void ParseTaskKV(string field, MaintenanceTaskBuilder task) {
        var colonIdx = field.IndexOf(':');
        if (colonIdx < 0) return;
        var key = field[..colonIdx].Trim();
        var val = field[(colonIdx + 1)..].Trim().Trim('"', '\'');

        switch (key) {
            case "id":           task.Id           = val; break;
            case "enabled":      task.Enabled      = string.Equals(val, "true", System.StringComparison.OrdinalIgnoreCase); break;
            case "frequency":    task.Frequency    = val; break;
            case "safety":       task.Safety       = val; break;
            case "title":        task.Title        = val; break;
            case "instructions": task.Instructions = val; break;
        }
    }

    private static void ParseOptionSubfield(string field, LoopOptionBuilder opt) {
        var colonIdx = field.IndexOf(':');
        if (colonIdx < 0) return;
        var key = field[..colonIdx].Trim();
        var val = field[(colonIdx + 1)..].Trim();

        switch (key) {
            case "type":    opt.Type    = val;                                              break;
            case "label":   opt.Label   = val.Trim('"', '\'');                              break;
            case "hint":    opt.Hint    = val.Trim('"', '\'');                              break;
            case "value":   opt.RawValue = val;                                             break;
            case "choices":
                val = val.Trim('[', ']');
                opt.Choices = val.Split(',')
                    .Select(s => s.Trim().Trim('"', '\''))
                    .Where(s => s.Length > 0)
                    .ToList();
                break;
        }
    }

    private static int CountLeadingSpaces(string line) {
        int n = 0;
        while (n < line.Length && line[n] == ' ') n++;
        return n;
    }

    // ── Mutable builder ───────────────────────────────────────────────────────

    private sealed class MaintenanceTaskBuilder {
        public string  Id           { get; set; } = "";
        public bool    Enabled      { get; set; } = false;
        public string  Frequency    { get; set; } = "daily";
        public string  Safety       { get; set; } = "";
        public string  Title        { get; set; } = "";
        public string  Instructions { get; set; } = "";
        public List<LoopOption>? BuiltOptions { get; set; }

        public MaintenanceTask Build(string globalSafety) =>
            new(
                Id:           Id,
                Enabled:      Enabled,
                Frequency:    Frequency,
                Safety:       EnforceSafetyFloor(globalSafety, Safety),
                Title:        Title.Length > 0 ? Title : Id,
                Instructions: Instructions,
                Options:      BuiltOptions);
    }
}
