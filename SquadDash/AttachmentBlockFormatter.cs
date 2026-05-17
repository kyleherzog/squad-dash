namespace SquadDash;

/// <summary>
/// Pure-static helpers for building and parsing typed XML attachment blocks.
/// Format: <c>&lt;attachment type="…" title="…"&gt;\ncontent\n&lt;/attachment&gt;</c>.
/// </summary>
internal static class AttachmentBlockFormatter
{
    // Sentinel used to escape the close tag inside content so the parser never
    // sees a premature </attachment>.
    private const string EscapedCloseTag = "&lt;/attachment&gt;";
    private const string LiteralCloseTag = "</attachment>";

    /// <summary>
    /// Builds a typed XML-style attachment block.
    /// Any occurrence of <c>&lt;/attachment&gt;</c> in <paramref name="content"/> is
    /// escaped to <c>&amp;lt;/attachment&amp;gt;</c> so the block close tag cannot
    /// collide with content.
    /// </summary>
    internal static string BuildTypedAttachmentBlock(string type, string? title, string content)
    {
        content = content.Replace(LiteralCloseTag, EscapedCloseTag, StringComparison.Ordinal);
        var openTag = title is not null
            ? $"<attachment type=\"{type}\" title=\"{title.Replace("\"", "&quot;")}\">"
            : $"<attachment type=\"{type}\">";
        return $"{openTag}\n{content}\n{LiteralCloseTag}";
    }

    /// <summary>
    /// Given a <paramref name="prompt"/> that uses the typed attachment format (optional
    /// preamble line + one or more <c>&lt;attachment&gt;</c> blocks), returns the index of
    /// the first character of the user's actual message.
    /// Returns -1 if the header section cannot be parsed.
    /// </summary>
    internal static int StripTypedAttachmentHeaders(string prompt)
    {
        int pos = 0;

        // Skip optional preamble (single line).
        const string Preamble = "The user has attached the following context items. Please refer to them as needed.";
        if (prompt.StartsWith(Preamble, StringComparison.Ordinal))
        {
            pos = Preamble.Length;
            if (pos < prompt.Length && prompt[pos] == '\n') pos++;
        }

        // Scan past attachment blocks: XML <attachment> tags, bracket lines, and plain lines.
        const string AttachOpen  = "<attachment ";
        while (pos < prompt.Length)
        {
            // \n\n is the terminal separator between attachments and user text.
            if (pos + 1 < prompt.Length && prompt[pos] == '\n' && prompt[pos + 1] == '\n')
                return pos + 2;

            // XML typed block: <attachment ...>...</attachment>
            // Content with literal </attachment> is escaped, so IndexOf finds only the real close tag.
            if (pos + AttachOpen.Length <= prompt.Length &&
                prompt.AsSpan(pos, AttachOpen.Length).SequenceEqual(AttachOpen.AsSpan()))
            {
                var closeIdx = prompt.IndexOf(LiteralCloseTag, pos, StringComparison.Ordinal);
                if (closeIdx < 0) return -1;
                pos = closeIdx + LiteralCloseTag.Length;
                continue;
            }

            pos++;
        }
        return -1;
    }

    /// <summary>
    /// Strips the outer <c>&lt;attachment …&gt;…&lt;/attachment&gt;</c> wrapper from a typed
    /// attachment block and returns the inner content with any escaped close tags restored.
    /// Falls back to returning the original string unchanged if the format is not recognised
    /// (e.g. old SQUADCLIP blocks).
    /// </summary>
    internal static string ExtractAttachmentContent(string block)
    {
        if (!block.StartsWith("<attachment ", StringComparison.Ordinal))
            return block;
        var closeAngle = block.IndexOf('>', StringComparison.Ordinal);
        if (closeAngle < 0) return block;
        var closeIdx = block.LastIndexOf(LiteralCloseTag, StringComparison.Ordinal);
        if (closeIdx < 0) return block;
        // Content is between the '>' of the open tag and the start of </attachment>.
        var start = closeAngle + 1;
        if (start < block.Length && block[start] == '\n') start++; // skip leading newline
        var content = block[start..closeIdx].TrimEnd();
        return content.Replace(EscapedCloseTag, LiteralCloseTag, StringComparison.Ordinal);
    }
}
