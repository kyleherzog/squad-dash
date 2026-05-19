using System.Text.RegularExpressions;

namespace SquadDash;

internal static class DocumentUtilities
{
    private static readonly Regex FrontMatterRegex =
        new(@"^---[ \t]*\r?\n[\s\S]*?\r?\n---[ \t]*\r?\n?",
            RegexOptions.Compiled);

    /// <summary>
    /// Strips a Jekyll/JTD YAML front matter block from <paramref name="raw"/> and returns
    /// the body text.  <paramref name="frontMatter"/> receives the stripped block (including
    /// the trailing newline) so it can be prepended on save without data loss.
    /// </summary>
    internal static string StripDocFrontMatter(string raw, out string frontMatter)
    {
        var m = FrontMatterRegex.Match(raw);
        if (m.Success)
        {
            frontMatter = m.Value;
            return raw[m.Length..];
        }
        frontMatter = string.Empty;
        return raw;
    }

    internal static string SlugifyDocName(string name)
    {
        name = name.Trim().ToLowerInvariant();
        name = Regex.Replace(name, @"\s+", "-");
        name = Regex.Replace(name, @"[^a-z0-9\-]", string.Empty);
        name = Regex.Replace(name, @"-+", "-");
        name = name.Trim('-');
        return string.IsNullOrEmpty(name) ? "new-document" : name;
    }

    internal static int[]? ParseSimpleVersion(string v)
    {
        var parts = v.TrimStart('v').Split('.');
        if (parts.Length < 3)
            return null;
        var result = new int[3];
        for (var i = 0; i < 3; i++)
        {
            var numPart = parts[i].Split('-')[0];
            if (!int.TryParse(numPart, out result[i]))
                return null;
        }
        return result;
    }

    internal static bool IsNewerSquadVersion(string candidate, string? current)
    {
        if (string.IsNullOrWhiteSpace(current))
            return false;
        var a = ParseSimpleVersion(candidate);
        var b = ParseSimpleVersion(current);
        if (a is null || b is null)
            return false;
        for (var i = 0; i < 3; i++)
        {
            if (a[i] > b[i]) return true;
            if (a[i] < b[i]) return false;
        }
        return false;
    }

    /// <summary>Returns true if the given front matter contains <c>nav_exclude: true</c>.</summary>
    internal static bool ReadNavExclude(string frontMatter)
    {
        if (string.IsNullOrEmpty(frontMatter)) return false;
        return Regex.IsMatch(frontMatter, @"^\s*nav_exclude\s*:\s*true\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Returns a new front matter string with <c>nav_exclude</c> and <c>search_exclude</c>
    /// added (when <paramref name="excluded"/> is true) or removed (when false).
    /// Creates a minimal front matter block if none existed and exclusion is requested.
    /// </summary>
    internal static string SetNavExclude(string currentFrontMatter, bool excluded)
    {
        if (excluded)
        {
            if (string.IsNullOrEmpty(currentFrontMatter))
                return "---\nnav_exclude: true\nsearch_exclude: true\n---\n";
            var fm = UpdateOrAddFrontMatterKey(currentFrontMatter, "nav_exclude", "true");
            fm     = UpdateOrAddFrontMatterKey(fm, "search_exclude", "true");
            return fm;
        }
        else
        {
            if (string.IsNullOrEmpty(currentFrontMatter)) return string.Empty;
            var fm = RemoveFrontMatterKey(currentFrontMatter, "nav_exclude");
            fm     = RemoveFrontMatterKey(fm, "search_exclude");
            return fm;
        }
    }

    private static string UpdateOrAddFrontMatterKey(string frontMatter, string key, string value)
    {
        var pattern = new Regex($@"^({Regex.Escape(key)}\s*:).*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (pattern.IsMatch(frontMatter))
            return pattern.Replace(frontMatter, $"$1 {value}");

        // Key not present — insert before the closing ---
        var closingDash = frontMatter.LastIndexOf("\n---", StringComparison.Ordinal);
        if (closingDash >= 0)
            return frontMatter.Insert(closingDash, $"\n{key}: {value}");

        return frontMatter;
    }

    private static string RemoveFrontMatterKey(string frontMatter, string key)
    {
        var pattern = new Regex($@"^{Regex.Escape(key)}\s*:.*(\r?\n)?",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return pattern.Replace(frontMatter, string.Empty);
    }
}
