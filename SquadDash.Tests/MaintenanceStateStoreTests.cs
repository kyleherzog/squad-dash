using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace SquadDash.Tests;

/// <summary>
/// Behavioral specs for <see cref="MaintenanceStateStore"/>.
/// Tests will compile once Arjun Sen's Phase 1 implementation lands.
/// </summary>
[TestFixture]
internal sealed class MaintenanceStateStoreTests {

    private string _stateDir = null!;
    private MaintenanceStateStore _store = null!;

    [SetUp]
    public void SetUp() {
        _stateDir = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"maint_state_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_stateDir);
        _store = new MaintenanceStateStore(_stateDir);
    }

    [TearDown]
    public void TearDown() {
        if (Directory.Exists(_stateDir))
            Directory.Delete(_stateDir, recursive: true);
    }

    // ── IsEligible — unknown task ─────────────────────────────────────────────

    [Test]
    public void IsEligible_UnknownTask_ReturnsTrue() {
        // A task that has never run should always be eligible.
        var eligible = _store.IsEligible("never-run-task", "daily", commitSha: "abc123");
        Assert.That(eligible, Is.True, "A task with no prior run record must be eligible");
    }

    // ── IsEligible — frequency: daily ─────────────────────────────────────────

    [Test]
    public void IsEligible_DailyFrequency_NotEligibleAfterRunToday() {
        _store.RecordRun("daily-task", commitSha: "sha1");
        var eligible = _store.IsEligible("daily-task", "daily", commitSha: "sha2");
        Assert.That(eligible, Is.False, "daily task must not be eligible again on the same calendar day");
    }

    [Test]
    public void IsEligible_DailyFrequency_EligibleAfterRunYesterday() {
        // Seed the state file with a last-run timestamp from yesterday.
        WriteStateWithLastRunAt("daily-task", lastRunAt: DateTime.UtcNow.AddDays(-1), lastSha: "sha1");
        _store.Reload();

        var eligible = _store.IsEligible("daily-task", "daily", commitSha: "sha2");
        Assert.That(eligible, Is.True, "daily task must be eligible the day after it last ran");
    }

    // ── IsEligible — frequency: per-commit ────────────────────────────────────

    [Test]
    public void IsEligible_PerCommitFrequency_NotEligibleForSameCommitSha() {
        const string sha = "deadbeef";
        _store.RecordRun("per-commit-task", commitSha: sha);
        var eligible = _store.IsEligible("per-commit-task", "per-commit", commitSha: sha);
        Assert.That(eligible, Is.False, "per-commit task must not re-run for the same commit SHA");
    }

    [Test]
    public void IsEligible_PerCommitFrequency_EligibleForNewCommitSha() {
        _store.RecordRun("per-commit-task", commitSha: "old-sha");
        var eligible = _store.IsEligible("per-commit-task", "per-commit", commitSha: "new-sha");
        Assert.That(eligible, Is.True, "per-commit task must be eligible when the commit SHA changes");
    }

    // ── IsEligible — frequency: always ────────────────────────────────────────

    [Test]
    public void IsEligible_AlwaysFrequency_EligibleEvenAfterRecentRun() {
        _store.RecordRun("always-task", commitSha: "sha1");
        var eligible = _store.IsEligible("always-task", "always", commitSha: "sha1");
        Assert.That(eligible, Is.True, "always task must be eligible regardless of prior run history");
    }

    // ── RecordRun persists to disk ────────────────────────────────────────────

    [Test]
    public void RecordRun_PersistsStateToDisk() {
        _store.RecordRun("persist-task", commitSha: "sha-persist");

        // Verify a state file was written somewhere inside _stateDir.
        var files = Directory.GetFiles(_stateDir, "*", SearchOption.AllDirectories);
        Assert.That(files, Is.Not.Empty, "RecordRun must write at least one file to the state directory");
    }

    // ── State survives reload ─────────────────────────────────────────────────

    [Test]
    public void RecordRun_StateSurvivesReload() {
        _store.RecordRun("reload-task", commitSha: "sha-reload");

        // Create a fresh store over the same directory and reload.
        var freshStore = new MaintenanceStateStore(_stateDir);
        freshStore.Reload();

        var eligible = freshStore.IsEligible("reload-task", "daily", commitSha: "sha-other");
        Assert.That(eligible, Is.False,
            "After reload, a task run today must still be ineligible (state must survive across instances)");
    }

    // ── Atomic write ─────────────────────────────────────────────────────────

    [Test]
    public void RecordRun_ProducesCompleteStateFile() {
        _store.RecordRun("atomic-task", commitSha: "sha-atomic");

        // Ensure the final state file is valid JSON (proves the write was completed atomically
        // and did not leave a half-written or zero-byte file).
        var files = Directory.GetFiles(_stateDir, "*.json", SearchOption.AllDirectories);
        Assert.That(files, Is.Not.Empty, "A .json state file must exist after RecordRun");

        foreach (var file in files) {
            var text = File.ReadAllText(file);
            Assert.DoesNotThrow(
                () => JsonDocument.Parse(text),
                $"State file '{Path.GetFileName(file)}' must be valid JSON (no partial writes)");
        }
    }

    // ── Corrupt state file ────────────────────────────────────────────────────

    [Test]
    public void Load_CorruptStateFile_GracefulDegradation_ReturnsEmptyState() {
        // Write garbage directly to the expected state file.
        var stateFile = Path.Combine(_stateDir, "maintenance-state.json");
        File.WriteAllText(stateFile, "{ this is not valid json !!!");

        var store = new MaintenanceStateStore(_stateDir);
        store.Reload();

        // All tasks should appear as never-run (no crash, empty state).
        var eligible = store.IsEligible("any-task", "daily", commitSha: "sha");
        Assert.That(eligible, Is.True,
            "A corrupt state file must not crash the store; all tasks should be eligible (empty state)");
    }

    // ── Missing state file ────────────────────────────────────────────────────

    [Test]
    public void Load_MissingStateFile_GracefulDegradation_ReturnsEmptyState() {
        // Point to a directory that contains no state file at all.
        var emptyDir = Path.Combine(_stateDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var store = new MaintenanceStateStore(emptyDir);
        store.Reload();

        var eligible = store.IsEligible("any-task", "per-commit", commitSha: "sha");
        Assert.That(eligible, Is.True,
            "A missing state file must not crash the store; all tasks should be eligible");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a minimal state JSON file into <see cref="_stateDir"/> so tests can
    /// exercise date-dependent eligibility without waiting a real day.
    /// </summary>
    private void WriteStateWithLastRunAt(string taskId, DateTime lastRunAt, string lastSha) {
        var stateFile = Path.Combine(_stateDir, "maintenance-state.json");
        var json = $$"""
            {
              "tasks": {
                "{{taskId}}": {
                  "lastRunAt": "{{lastRunAt:O}}",
                  "lastCommitSha": "{{lastSha}}"
                }
              }
            }
            """;
        File.WriteAllText(stateFile, json);
    }
}
