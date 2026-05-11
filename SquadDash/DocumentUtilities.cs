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
}
