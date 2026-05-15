using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SquadDash;

/// <summary>
/// Detects and cycles text through Title Case → Sentence case → UPPERCASE → PascalCase → kebab-case → preserve_underscores.
/// </summary>
internal static class TextCaseHelper
{
    internal enum TextCase { None, TitleCase, SentenceCase, UpperCase, PascalCase, KebabCase, UnderscoreCase }

    /// <summary>
    /// Detects which case the text matches, or <see cref="TextCase.None"/> if it matches none.
    /// </summary>
    internal static TextCase DetectCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return TextCase.None;

        // UPPERCASE: every letter is uppercase, at least one letter present (checked before PascalCase)
        if (text.Any(char.IsLetter) && text.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            return TextCase.UpperCase;

        // KebabCase: no whitespace, contains '-', all non-dash chars are lowercase
        if (!text.Any(char.IsWhiteSpace) && text.Contains('-')
            && text.All(c => c == '-' || !char.IsLetter(c) || char.IsLower(c)))
            return TextCase.KebabCase;

        // UnderscoreCase: no whitespace, contains '_', not also KebabCase
        if (!text.Any(char.IsWhiteSpace) && text.Contains('_'))
            return TextCase.UnderscoreCase;

        // PascalCase: no spaces, first char uppercase, at least one more uppercase after the first char
        if (!text.Contains(' ') && text.Length > 1 && char.IsUpper(text[0]) && text[1..].Any(char.IsUpper))
            return TextCase.PascalCase;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return TextCase.None;

        // Title Case: every word starts with uppercase, rest of letters lowercase
        if (words.All(w => w.Length > 0 && char.IsUpper(w[0])
                           && w.Skip(1).All(c => !char.IsLetter(c) || char.IsLower(c))))
            return TextCase.TitleCase;

        // Sentence case: first word starts uppercase (rest lowercase), all other words fully lowercase
        bool firstOk = words[0].Length > 0
                       && char.IsUpper(words[0][0])
                       && words[0].Skip(1).All(c => !char.IsLetter(c) || char.IsLower(c));
        bool restLower = words.Skip(1).All(w => w.All(c => !char.IsLetter(c) || char.IsLower(c)));
        if (firstOk && restLower) return TextCase.SentenceCase;

        return TextCase.None;
    }

    /// <summary>Every word's first letter uppercased, remaining letters lowercased (split on spaces).</summary>
    internal static string ToTitleCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder(text.Length);
        bool capitalizeNext = true;
        foreach (char c in text)
        {
            if (c == ' ')
            {
                capitalizeNext = true;
                sb.Append(c);
            }
            else if (capitalizeNext && char.IsLetter(c))
            {
                sb.Append(char.ToUpper(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(char.ToLower(c));
                capitalizeNext = false;
            }
        }
        return sb.ToString();
    }

    /// <summary>First letter of first word uppercased; everything else lowercased.</summary>
    internal static string ToSentenceCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder(text.Length);
        bool firstLetter = true;
        foreach (char c in text)
        {
            if (firstLetter && char.IsLetter(c))
            {
                sb.Append(char.ToUpper(c));
                firstLetter = false;
            }
            else
            {
                sb.Append(char.ToLower(c));
            }
        }
        return sb.ToString();
    }

    /// <summary>All letters uppercased.</summary>
    internal static string ToUpperCase(string text) => text.ToUpper();

    /// <summary>
    /// Split on spaces/underscores/hyphens; every word title-capped; joined with no separator.
    /// E.g. "hello world" → "HelloWorld".
    /// </summary>
    internal static string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var words = Regex.Split(text, @"[\s_\-]+")
                         .Where(w => w.Length > 0)
                         .ToArray();
        if (words.Length == 0) return text;
        var sb = new StringBuilder();
        foreach (var word in words)
        {
            sb.Append(char.ToUpper(word[0]));
            if (word.Length > 1)
                sb.Append(word[1..].ToLower());
        }
        return sb.ToString();
    }

    /// <summary>
    /// All letters lowercased; spaces and underscores replaced by a single dash.
    /// E.g. "Hello World" → "hello-world".
    /// </summary>
    internal static string ToKebabCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = Regex.Replace(text, @"[\s_]+", "-");
        return normalized.ToLower();
    }

    /// <summary>
    /// Spaces replaced with underscores; letter case preserved exactly.
    /// E.g. "Hello World" → "Hello_World".
    /// </summary>
    internal static string ToUnderscorePreserveCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, @"\s+", "_");
    }

    /// <summary>
    /// Returns the six case variants in cycle order:
    /// [0] Title Case, [1] Sentence case, [2] UPPERCASE, [3] PascalCase, [4] kebab-case, [5] preserve_underscores.
    /// </summary>
    internal static List<string> ComputeVariants(string text) =>
        [ToTitleCase(text), ToSentenceCase(text), ToUpperCase(text), ToPascalCase(text), ToKebabCase(text), ToUnderscorePreserveCase(text)];

    /// <summary>
    /// Returns the index into <see cref="ComputeVariants"/> of the variant to apply on the
    /// first Shift+F3 press — i.e. the case that follows the currently-detected case.
    /// </summary>
    internal static int GetFirstVariantIndex(string text) =>
        DetectCase(text) switch
        {
            TextCase.TitleCase     => 1,
            TextCase.SentenceCase  => 2,
            TextCase.UpperCase     => 3,
            TextCase.PascalCase    => 4,
            TextCase.KebabCase     => 5,
            TextCase.UnderscoreCase => 0,
            _                      => 0,
        };

    /// <summary>
    /// Detects the current case and returns the text transformed to the next case in the cycle:
    /// Title Case → Sentence case → UPPERCASE → PascalCase → kebab-case → preserve_underscores → (back to) Title Case.
    /// If the text matches no known case, starts from Title Case.
    /// </summary>
    internal static string CycleCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return DetectCase(text) switch
        {
            TextCase.TitleCase      => ToSentenceCase(text),
            TextCase.SentenceCase   => ToUpperCase(text),
            TextCase.UpperCase      => ToPascalCase(text),
            TextCase.PascalCase     => ToKebabCase(text),
            TextCase.KebabCase      => ToUnderscorePreserveCase(text),
            TextCase.UnderscoreCase => ToTitleCase(text),
            _                       => ToTitleCase(text),
        };
    }
}
