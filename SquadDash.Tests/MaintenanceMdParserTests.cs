using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace SquadDash.Tests;

/// <summary>
/// Behavioral specs for <see cref="MaintenanceMdParser"/>.
/// Tests will compile once Arjun Sen's Phase 1 implementation lands.
/// </summary>
[TestFixture]
internal sealed class MaintenanceMdParserTests {

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string WriteTempFile(string content) {
        var path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"maintenance_{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return path;
    }

    private static void DeleteTempFile(string path) {
        try { File.Delete(path); } catch { /* best-effort cleanup */ }
    }

    // ── File-not-found ────────────────────────────────────────────────────────

    [Test]
    public void Parse_FileNotFound_ReturnsNull() {
        var result = MaintenanceMdParser.Parse(@"C:\does\not\exist\maintenance.md");
        Assert.That(result, Is.Null);
    }

    // ── Missing configured: true ──────────────────────────────────────────────

    [Test]
    public void Parse_ConfiguredKeyAbsentFromFrontmatter_ReturnsNull() {
        var path = WriteTempFile(
            """
            ---
            idle_timeout: 10
            max_tasks_per_session: 3
            ---
            """);
        try {
            Assert.That(MaintenanceMdParser.Parse(path), Is.Null);
        }
        finally { DeleteTempFile(path); }
    }

    [Test]
    public void Parse_ConfiguredFalse_ReturnsNull() {
        var path = WriteTempFile(
            """
            ---
            configured: false
            idle_timeout: 10
            ---
            """);
        try {
            Assert.That(MaintenanceMdParser.Parse(path), Is.Null);
        }
        finally { DeleteTempFile(path); }
    }

    // ── Global frontmatter fields ─────────────────────────────────────────────

    [Test]
    public void Parse_GlobalFields_ParsedCorrectly() {
        var path = WriteTempFile(
            """
            ---
            configured: true
            idle_timeout: 20
            max_tasks_per_session: 8
            safety: direct
            ---
            """);
        try {
            var config = MaintenanceMdParser.Parse(path);
            Assert.That(config, Is.Not.Null);
            Assert.Multiple(() => {
                Assert.That(config!.IdleTimeout,         Is.EqualTo(20));
                Assert.That(config.MaxTasksPerSession,   Is.EqualTo(8));
                Assert.That(config.Safety,               Is.EqualTo("direct"));
            });
        }
        finally { DeleteTempFile(path); }
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    [Test]
    public void Parse_MissingOptionalGlobalFields_UsesDefaults() {
        var path = WriteTempFile(
            """
            ---
            configured: true
            ---
            """);
        try {
            var config = MaintenanceMdParser.Parse(path);
            Assert.That(config, Is.Not.Null);
            Assert.Multiple(() => {
                Assert.That(config!.IdleTimeout,       Is.EqualTo(15),      "idle_timeout default");
                Assert.That(config.MaxTasksPerSession, Is.EqualTo(5),       "max_tasks_per_session default");
                Assert.That(config.Safety,             Is.EqualTo("branch"),"safety default");
            });
        }
        finally { DeleteTempFile(path); }
    }

    // ── Task block ────────────────────────────────────────────────────────────

    [Test]
    public void Parse_SingleTaskBlock_ParsedCorrectly() {
        var path = WriteTempFile(
            """
            ---
            configured: true
            tasks:
              - id: cleanup
                enabled: true
                frequency: daily
                safety: branch
                title: "Daily Cleanup"
                instructions: "Remove stale temp files and update the changelog."
            ---
            """);
        try {
            var config = MaintenanceMdParser.Parse(path);
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Tasks, Has.Count.EqualTo(1));

            var task = config.Tasks[0];
            Assert.Multiple(() => {
                Assert.That(task.Id,           Is.EqualTo("cleanup"));
                Assert.That(task.Enabled,      Is.True);
                Assert.That(task.Frequency,    Is.EqualTo("daily"));
                Assert.That(task.Safety,       Is.EqualTo("branch"));
                Assert.That(task.Title,        Is.EqualTo("Daily Cleanup"));
                Assert.That(task.Instructions, Does.Contain("Remove stale temp files"));
            });
        }
        finally { DeleteTempFile(path); }
    }

    // ── Task with options block ────────────────────────────────────────────────

    [Test]
    public void Parse_TaskWithOptionsBlock_RadioOptionsParsedCorrectly() {
        var path = WriteTempFile(
            """
            ---
            configured: true
            tasks:
              - id: dependency-update
                enabled: true
                frequency: per-commit
                safety: branch
                title: "Update Dependencies"
                instructions: "Run dependency updates."
                options:
                  strategy:
                    type: radio
                    label: "Update strategy"
                    choices: [patch, minor, major]
            ---
            """);
        try {
            var config = MaintenanceMdParser.Parse(path);
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Tasks, Has.Count.EqualTo(1));

            var task = config.Tasks[0];
            Assert.That(task.Options, Is.Not.Null);
            Assert.That(task.Options, Has.Count.EqualTo(1));

            var opt = task.Options![0];
            Assert.Multiple(() => {
                Assert.That(opt.Key,     Is.EqualTo("strategy"));
                Assert.That(opt.Type,    Is.EqualTo("radio"));
                Assert.That(opt.Label,   Is.EqualTo("Update strategy"));
                Assert.That(opt.Choices, Is.Not.Null);
                Assert.That(opt.Choices, Has.Count.EqualTo(3));
                Assert.That(opt.Choices, Does.Contain("patch"));
                Assert.That(opt.Choices, Does.Contain("minor"));
                Assert.That(opt.Choices, Does.Contain("major"));
            });
        }
        finally { DeleteTempFile(path); }
    }

    // ── Safety floor: global report-only ─────────────────────────────────────

    [Test]
    public void Parse_GlobalReportOnly_PerTaskDirectBecomesReportOnly() {
        var path = WriteTempFile(
            """
            ---
            configured: true
            safety: report-only
            tasks:
              - id: aggressive-task
                enabled: true
                frequency: always
                safety: direct
                title: "Should be downgraded"
                instructions: "Make sweeping changes."
            ---
            """);
        try {
            var config = MaintenanceMdParser.Parse(path);
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Tasks, Has.Count.EqualTo(1));

            Assert.That(config.Tasks[0].Safety, Is.EqualTo("report-only"),
                "Global report-only floor must prevent per-task safety from escalating to direct");
        }
        finally { DeleteTempFile(path); }
    }

    // ── Safety floor: global branch ───────────────────────────────────────────

    [Test]
    public void Parse_GlobalBranch_PerTaskDirectDowngradedToBranch() {
        var path = WriteTempFile(
            """
            ---
            configured: true
            safety: branch
            tasks:
              - id: risky-task
                enabled: true
                frequency: always
                safety: direct
                title: "Should be capped at branch"
                instructions: "Directly commit changes."
            ---
            """);
        try {
            var config = MaintenanceMdParser.Parse(path);
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Tasks, Has.Count.EqualTo(1));

            Assert.That(config.Tasks[0].Safety, Is.EqualTo("branch"),
                "Global branch floor must prevent per-task safety from escalating to direct");
        }
        finally { DeleteTempFile(path); }
    }

    [Test]
    public void Parse_GlobalBranch_PerTaskReportOnlyKeptAsIs() {
        var path = WriteTempFile(
            """
            ---
            configured: true
            safety: branch
            tasks:
              - id: read-only-task
                enabled: true
                frequency: always
                safety: report-only
                title: "Read-only analysis"
                instructions: "Analyse the build output."
            ---
            """);
        try {
            var config = MaintenanceMdParser.Parse(path);
            Assert.That(config, Is.Not.Null);
            // report-only is already more restrictive than branch; must not be upgraded.
            Assert.That(config!.Tasks[0].Safety, Is.EqualTo("report-only"),
                "A per-task safety more restrictive than the global floor must not be weakened");
        }
        finally { DeleteTempFile(path); }
    }

    // ── type: radio options parsed as LoopOption entries ─────────────────────

    [Test]
    public void Parse_TaskTypeRadioOptions_ParsedAsLoopOptionEntries() {
        var path = WriteTempFile(
            """
            ---
            configured: true
            tasks:
              - id: code-review
                enabled: true
                frequency: always
                safety: report-only
                title: "Code Review Depth"
                instructions: "Review the latest diff."
                options:
                  depth:
                    type: radio
                    label: "Review depth"
                    choices: [quick, standard, thorough]
            ---
            """);
        try {
            var config = MaintenanceMdParser.Parse(path);
            Assert.That(config, Is.Not.Null);
            var task = config!.Tasks.Single(t => t.Id == "code-review");
            Assert.That(task.Options, Is.Not.Null);
            var depthOpt = task.Options!.Single(o => o.Key == "depth");
            Assert.Multiple(() => {
                Assert.That(depthOpt.Type,    Is.EqualTo("radio"));
                Assert.That(depthOpt.Choices, Is.Not.Null);
                Assert.That(depthOpt.Choices!.Count, Is.EqualTo(3));
            });
        }
        finally { DeleteTempFile(path); }
    }
}
