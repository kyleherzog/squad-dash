using NUnit.Framework;
using SquadDash;
using static SquadDash.TextCaseHelper;

namespace SquadDash.Tests;

[TestFixture]
internal class TextCaseHelperTests
{
    // ──────────────────────────────────────────────────────────────
    // 1. DetectCase
    // ──────────────────────────────────────────────────────────────

    [TestCase("To A Tag And A Tag Filter",    TextCase.TitleCase)]
    [TestCase("To a tag and a tag filter",    TextCase.SentenceCase)]
    [TestCase("TO A TAG AND A TAG FILTER",    TextCase.UpperCase)]
    [TestCase("ToATagAndATagFilter",          TextCase.PascalCase)]
    [TestCase("to-a-tag-and-a-tag-filter",   TextCase.KebabCase)]
    [TestCase("to_a_tag_and_a_tag_filter",   TextCase.UnderscoreCase)]
    [TestCase("to a tag and a tag filter",   TextCase.None)]
    public void DetectCase_ReturnsExpected(string input, TextCase expected)
    {
        Assert.That(DetectCase(input), Is.EqualTo(expected));
    }

    // ──────────────────────────────────────────────────────────────
    // 2. ComputeVariants — known-case input stays at 6 items
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void ComputeVariants_TitleCaseInput_Returns6Items()
    {
        var variants = ComputeVariants("To A Tag And A Tag Filter");
        Assert.That(variants, Has.Count.EqualTo(6));
    }

    // ──────────────────────────────────────────────────────────────
    // 3. ComputeVariants — unrecognised input gets 7 items
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void ComputeVariants_NoneInput_Returns7ItemsWithOriginalLast()
    {
        const string original = "to a tag and a tag filter";
        var variants = ComputeVariants(original);
        Assert.That(variants, Has.Count.EqualTo(7));
        Assert.That(variants[6], Is.EqualTo(original));
    }

    // ──────────────────────────────────────────────────────────────
    // 4. Full cycle — None input returns to original after 6 presses
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void FullCycle_NoneInput_ReturnsToOriginalAfter6Presses()
    {
        const string original = "to a tag and a tag filter";
        var variants = ComputeVariants(original);
        int startIndex = GetFirstVariantIndex(original);

        Assert.That(startIndex, Is.EqualTo(0), "TextCase.None should start at index 0 (TitleCase)");

        var results = new List<string>();
        for (int i = 0; i < 7; i++)
            results.Add(variants[(startIndex + i) % variants.Count]);

        string[] expected =
        [
            "To A Tag And A Tag Filter",
            "To a tag and a tag filter",
            "TO A TAG AND A TAG FILTER",
            "ToATagAndATagFilter",
            "to-a-tag-and-a-tag-filter",
            "to_a_tag_and_a_tag_filter",
            "to a tag and a tag filter"
        ];

        Assert.That(results, Is.EqualTo(expected));
        Assert.That(results[6], Is.EqualTo(original));
    }

    // ──────────────────────────────────────────────────────────────
    // 5. Full cycle — TitleCase input wraps back to TitleCase
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void FullCycle_TitleCaseInput_WrapsBackToTitleCase()
    {
        const string text = "To A Tag And A Tag Filter";
        var variants = ComputeVariants(text);
        int startIndex = GetFirstVariantIndex(text);

        Assert.That(startIndex, Is.EqualTo(1), "TitleCase should start at index 1 (SentenceCase)");

        var results = new List<string>();
        for (int i = 0; i < 6; i++)
            results.Add(variants[(startIndex + i) % variants.Count]);

        Assert.That(results[5], Is.EqualTo(text),
            "After 6 presses the cycle should wrap back to TitleCase");
    }

    // ──────────────────────────────────────────────────────────────
    // 6. Individual transformers — spot checks
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void ToTitleCase_SpotCheck()
        => Assert.That(ToTitleCase("to a tag and a tag filter"), Is.EqualTo("To A Tag And A Tag Filter"));

    [Test]
    public void ToSentenceCase_SpotCheck()
        => Assert.That(ToSentenceCase("to a tag and a tag filter"), Is.EqualTo("To a tag and a tag filter"));

    [Test]
    public void ToUpperCase_SpotCheck()
        => Assert.That(ToUpperCase("to a tag and a tag filter"), Is.EqualTo("TO A TAG AND A TAG FILTER"));

    [Test]
    public void ToPascalCase_SpotCheck()
        => Assert.That(ToPascalCase("to a tag and a tag filter"), Is.EqualTo("ToATagAndATagFilter"));

    [Test]
    public void ToKebabCase_SpotCheck()
        => Assert.That(ToKebabCase("to a tag and a tag filter"), Is.EqualTo("to-a-tag-and-a-tag-filter"));

    [Test]
    public void ToUnderscorePreserveCase_SpotCheck()
        => Assert.That(ToUnderscorePreserveCase("to a tag and a tag filter"), Is.EqualTo("to_a_tag_and_a_tag_filter"));

    // ──────────────────────────────────────────────────────────────
    // 7. Leading/trailing punctuation — ToTitleCase
    // ──────────────────────────────────────────────────────────────

    [TestCase("\"hello world\"",   "\"Hello World\"")]
    [TestCase("(this is a test)",  "(This Is A Test)")]
    [TestCase("[my variable]",     "[My Variable]")]
    [TestCase("'single quoted'",   "'Single Quoted'")]
    [TestCase("...ellipsis text",  "...Ellipsis Text")]
    public void ToTitleCase_LeadingPunctuation_CapitalizesFirstLetter(string input, string expected)
        => Assert.That(ToTitleCase(input), Is.EqualTo(expected));

    // ──────────────────────────────────────────────────────────────
    // 8. Leading/trailing punctuation — ToSentenceCase
    // ──────────────────────────────────────────────────────────────

    [TestCase("\"hello world\"",   "\"Hello world\"")]
    [TestCase("(this is a test)",  "(This is a test)")]
    public void ToSentenceCase_LeadingPunctuation_CapitalizesFirstLetter(string input, string expected)
        => Assert.That(ToSentenceCase(input), Is.EqualTo(expected));

    // ──────────────────────────────────────────────────────────────
    // 9. Leading/trailing punctuation — DetectCase
    // ──────────────────────────────────────────────────────────────

    [TestCase("\"Hello World\"",   TextCase.TitleCase)]
    [TestCase("(This Is A Test)",  TextCase.TitleCase)]
    [TestCase("\"Hello world\"",   TextCase.SentenceCase)]
    [TestCase("(Hello world)",     TextCase.SentenceCase)]
    [TestCase("\"HELLO WORLD\"",   TextCase.UpperCase)]
    public void DetectCase_LeadingPunctuation_DetectsCorrectly(string input, TextCase expected)
        => Assert.That(DetectCase(input), Is.EqualTo(expected));

    // ──────────────────────────────────────────────────────────────
    // 10. Full cycle with leading quote — first press gives Title Case
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void ComputeVariants_QuotedInput_FirstVariantIsTitleCase()
    {
        const string input = "\"hello world\"";
        var variants = ComputeVariants(input);
        int startIndex = GetFirstVariantIndex(input);
        // First press should give title case
        Assert.That(variants[startIndex], Is.EqualTo("\"Hello World\""));
    }

    [Test]
    public void ComputeVariants_ParenInput_FirstVariantIsTitleCase()
    {
        const string input = "(this is a test)";
        var variants = ComputeVariants(input);
        int startIndex = GetFirstVariantIndex(input);
        Assert.That(variants[startIndex], Is.EqualTo("(This Is A Test)"));
    }
}
