using System.Collections.Generic;
using NUnit.Framework;

namespace SquadDash.Tests;

/// <summary>
/// Tests for loop-template preprocessing:
///   Task 1 — plain <c>{{key}}</c> substitution via <see cref="LoopMdParser.BuildMergedBody"/>
///   Task 2 — <c>{{#if}}</c>/<c>{{#unless}}</c> conditional block preprocessing via
///             <see cref="LoopMdParser.PreprocessConditionals"/>
/// </summary>
[TestFixture]
internal sealed class LoopTemplatePreprocessingTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LoopMdConfig MakeConfig(string instructions, params LoopOption[] options)
        => new(IntervalMinutes: 1, TimeoutMinutes: 5, Description: "", Instructions: instructions,
               Options: options.Length > 0 ? options : null);

    private static LoopOption Opt(string key, string value, string type = "string")
        => new(key, value, type, Label: null, Hint: null, Choices: null);

    private static LoopOption GroupOpt(string key)
        => new(key, RawValue: "", Type: "group", Label: null, Hint: null, Choices: null);

    // ── Task 1: plain {{key}} substitution ───────────────────────────────────

    [Test]
    public void BuildMergedBody_KnownKey_IsSubstituted()
    {
        var config = MakeConfig("Value is {{foo}}", Opt("foo", "bar"));
        Assert.That(LoopMdParser.BuildMergedBody(config), Is.EqualTo("Value is bar"));
    }

    [Test]
    public void BuildMergedBody_UnknownKey_LeftAsIs()
    {
        var config = MakeConfig("Value is {{unknown}}", Opt("foo", "bar"));
        Assert.That(LoopMdParser.BuildMergedBody(config), Is.EqualTo("Value is {{unknown}}"));
    }

    [Test]
    public void BuildMergedBody_GroupTypeOption_TokenLeftAsIs()
    {
        // Group-type options are UI-only headers — their (empty) RawValue must NOT
        // be substituted; the {{key}} token must survive unchanged.
        var config = MakeConfig("Header: {{section_header}}", GroupOpt("section_header"));
        Assert.That(LoopMdParser.BuildMergedBody(config),
            Is.EqualTo("Header: {{section_header}}"));
    }

    [Test]
    public void BuildMergedBody_IterationToken_UnaffectedByOptionSubstitution()
    {
        // {{iteration}} is a system variable resolved by LoopController, not by
        // BuildMergedBody.  It must survive the option-substitution pass untouched.
        var config = MakeConfig("Iter: {{iteration}}", Opt("foo", "bar"));
        Assert.That(LoopMdParser.BuildMergedBody(config), Does.Contain("{{iteration}}"));
    }

    // ── Task 2: {{#if}} conditional blocks ───────────────────────────────────

    [Test]
    public void PreprocessConditionals_IfTrue_IncludesContent()
    {
        var options = new[] { Opt("mode", "fast") };
        var text = "{{#if mode == \"fast\"}}\nGo fast!\n{{/if}}";
        var result = LoopMdParser.PreprocessConditionals(text, options);

        Assert.That(result, Does.Contain("Go fast!"));
        Assert.That(result, Does.Not.Contain("{{#if"));
        Assert.That(result, Does.Not.Contain("{{/if}}"));
    }

    [Test]
    public void PreprocessConditionals_IfFalse_RemovesBlockAndDelimiters()
    {
        var options = new[] { Opt("mode", "slow") };
        var text = "before\n{{#if mode == \"fast\"}}\nGo fast!\n{{/if}}\nafter";
        var result = LoopMdParser.PreprocessConditionals(text, options);

        Assert.That(result, Does.Not.Contain("Go fast!"));
        Assert.That(result, Does.Not.Contain("{{#if"));
        Assert.That(result, Does.Not.Contain("{{/if}}"));
        Assert.That(result, Does.Contain("before"));
        Assert.That(result, Does.Contain("after"));
    }

    // ── Task 2: {{#unless}} conditional blocks ───────────────────────────────

    [Test]
    public void PreprocessConditionals_UnlessTrue_RemovesBlock()
    {
        // Condition: skip == "true" is TRUE → #unless block is excluded
        var options = new[] { Opt("skip", "true") };
        var text = "{{#unless skip == \"true\"}}\nDo work!\n{{/unless}}";
        var result = LoopMdParser.PreprocessConditionals(text, options);

        Assert.That(result, Does.Not.Contain("Do work!"));
        Assert.That(result, Does.Not.Contain("{{#unless"));
        Assert.That(result, Does.Not.Contain("{{/unless}}"));
    }

    [Test]
    public void PreprocessConditionals_UnlessFalse_IncludesContent()
    {
        // Condition: skip == "true" is FALSE → #unless block is included
        var options = new[] { Opt("skip", "false") };
        var text = "{{#unless skip == \"true\"}}\nDo work!\n{{/unless}}";
        var result = LoopMdParser.PreprocessConditionals(text, options);

        Assert.That(result, Does.Contain("Do work!"));
        Assert.That(result, Does.Not.Contain("{{#unless"));
        Assert.That(result, Does.Not.Contain("{{/unless}}"));
    }

    // ── Mixed: conditionals + substitution ───────────────────────────────────

    [Test]
    public void BuildMergedBody_IfBlockContainingToken_TokenSubstituted()
    {
        // Conditionals run first, then substitution.
        // Tokens inside an included block must be substituted in the second pass.
        var config = MakeConfig(
            "{{#if mode == \"fast\"}}\nSpeed: {{speed}}\n{{/if}}",
            Opt("mode", "fast"),
            Opt("speed", "100mph"));

        var result = LoopMdParser.BuildMergedBody(config);

        Assert.That(result, Does.Contain("Speed: 100mph"));
        Assert.That(result, Does.Not.Contain("{{speed}}"));
        Assert.That(result, Does.Not.Contain("{{#if"));
    }

    [Test]
    public void BuildMergedBody_IfFalseBlockContainingToken_TokenNotPresent()
    {
        // Tokens inside an excluded block must never appear in output.
        var config = MakeConfig(
            "{{#if mode == \"fast\"}}\nSpeed: {{speed}}\n{{/if}}\ndone",
            Opt("mode", "slow"),
            Opt("speed", "100mph"));

        var result = LoopMdParser.BuildMergedBody(config);

        Assert.That(result, Does.Not.Contain("Speed:"));
        Assert.That(result, Does.Not.Contain("{{speed}}"));
        Assert.That(result, Does.Contain("done"));
    }

    // ── [**FILTER**] in BuildMergedBody ──────────────────────────────────────

    [Test]
    public void BuildMergedBody_FilterPlaceholder_ReplacedFromExtraSubs()
    {
        var config = MakeConfig("Task filter: [**FILTER**]");
        var extra  = new Dictionary<string, string> { ["[**FILTER**]"] = "Only bugs." };
        Assert.That(LoopMdParser.BuildMergedBody(config, extra),
            Is.EqualTo("Task filter: Only bugs."));
    }

    [Test]
    public void BuildMergedBody_FilterPlaceholder_NoExtraSubs_LeftAsIs()
    {
        var config = MakeConfig("Task filter: [**FILTER**]");
        Assert.That(LoopMdParser.BuildMergedBody(config),
            Is.EqualTo("Task filter: [**FILTER**]"));
    }

    [Test]
    public void BuildMergedBody_FilterPlaceholder_ExtraSubsWithoutFilterKey_LeftAsIs()
    {
        var config = MakeConfig("Task filter: [**FILTER**]");
        var extra  = new Dictionary<string, string> { ["iteration"] = "1" };
        Assert.That(LoopMdParser.BuildMergedBody(config, extra),
            Is.EqualTo("Task filter: [**FILTER**]"));
    }

    // ── BuildFilterInstruction ────────────────────────────────────────────────

    [Test]
    public void BuildFilterInstruction_NullOrEmpty_ReturnsNoFilterMessage()
    {
        Assert.That(LoopMdParser.BuildFilterInstruction(null),
            Is.EqualTo("No filter — process any unchecked task not owned by User."));
        Assert.That(LoopMdParser.BuildFilterInstruction(""),
            Is.EqualTo("No filter — process any unchecked task not owned by User."));
        Assert.That(LoopMdParser.BuildFilterInstruction("   "),
            Is.EqualTo("No filter — process any unchecked task not owned by User."));
    }

    [Test]
    public void BuildFilterInstruction_MentionOnly_ReturnsOwnerFilter()
    {
        var result = LoopMdParser.BuildFilterInstruction("@orion");
        Assert.That(result, Does.Contain("**@orion**"));
        Assert.That(result, Does.Contain("`*(Owner: orion)*`"));
        Assert.That(result, Does.Not.Contain("description or content"));
    }

    [Test]
    public void BuildFilterInstruction_KeywordOnly_ReturnsKeywordFilter()
    {
        var result = LoopMdParser.BuildFilterInstruction("authentication");
        Assert.That(result, Does.Contain("**authentication**"));
        Assert.That(result, Does.Not.Contain("**@"));
    }

    [Test]
    public void BuildFilterInstruction_MentionAndKeyword_ReturnsBothClauses()
    {
        var result = LoopMdParser.BuildFilterInstruction("@orion login bug");
        Assert.That(result, Does.Contain("**@orion**"));
        Assert.That(result, Does.Contain("**login bug**"));
    }

    [Test]
    public void BuildFilterInstruction_MultipleMentions_AllIncluded()
    {
        var result = LoopMdParser.BuildFilterInstruction("@alice @bob");
        Assert.That(result, Does.Contain("**@alice**"));
        Assert.That(result, Does.Contain("**@bob**"));
    }

    [Test]
    public void BuildFilterInstruction_DuplicateMention_DeduplicatedInOutput()
    {
        var result = LoopMdParser.BuildFilterInstruction("@alice @alice");
        // "**@alice**" should appear exactly once
        int count = 0;
        int idx   = 0;
        while ((idx = result.IndexOf("**@alice**", idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx++;
        }
        Assert.That(count, Is.EqualTo(1), "duplicate mention should be de-duplicated");
    }

    // ── Safety cleanup ────────────────────────────────────────────────────────

    [Test]
    public void PreprocessConditionals_StrayClosingTag_Stripped()
    {
        // A stray {{/if}} with no matching open tag must be removed.
        var options = new[] { Opt("foo", "bar") };
        var text = "Normal line\n{{/if}}\nEnd";
        var result = LoopMdParser.PreprocessConditionals(text, options);

        Assert.That(result, Does.Not.Contain("{{/if}}"));
        Assert.That(result, Does.Contain("Normal line"));
        Assert.That(result, Does.Contain("End"));
    }

    [Test]
    public void PreprocessConditionals_StrayOpeningTag_Stripped()
    {
        // A stray {{#if}} block with no closing tag: its content is consumed until
        // end-of-text, and any remaining syntax lines are stripped by safety cleanup.
        var options = new[] { Opt("foo", "bar") };
        var text = "Before\n{{#if orphan == \"x\"}}\nOrphaned content\nAfter";

        // Since there is no {{/if}}, "Orphaned content" and "After" are consumed.
        // Safety cleanup then removes any leftover syntax lines.
        var result = LoopMdParser.PreprocessConditionals(text, options);

        Assert.That(result, Does.Not.Contain("{{#if"));
        Assert.That(result, Does.Contain("Before"));
    }

    [Test]
    public void PreprocessConditionals_MultipleBlankLines_CollapsedToTwo()
    {
        var options = new[] { Opt("x", "y") };
        // A false if-block, when removed, may leave surrounding blank lines
        var text = "line1\n\n{{#if x == \"z\"}}\ncontent\n{{/if}}\n\n\n\nline2";
        var result = LoopMdParser.PreprocessConditionals(text, options);

        // Must not have 3+ consecutive newlines
        Assert.That(result, Does.Not.Match(@"\n{3,}"));
        Assert.That(result, Does.Contain("line1"));
        Assert.That(result, Does.Contain("line2"));
    }
}
