using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NUnit.Framework;

namespace SquadDash.Tests;

/// <summary>
/// Integration tests for <see cref="MaintenancePanelController.Refresh"/>.
/// All tests run on a dedicated STA thread via <see cref="WpfTestContext"/> because
/// WPF controls require an STA apartment.
/// </summary>
[TestFixture]
internal sealed class MaintenancePanelControllerTests {

    private string _stateDir = null!;

    [SetUp]
    public void SetUp() {
        _stateDir = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"maint_panel_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_stateDir);
    }

    [TearDown]
    public void TearDown() {
        if (Directory.Exists(_stateDir))
            Directory.Delete(_stateDir, recursive: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (MaintenancePanelController controller,
                    StackPanel listPanel,
                    TextBlock  statusLabel,
                    Button     runNowButton)
        CreateController() {
        var listPanel    = new StackPanel();
        var statusLabel  = new TextBlock();
        var runNowButton = new Button();
        var controller   = new MaintenancePanelController(
            listPanel,
            statusLabel,
            runNowButton,
            getWorkspacePath:  () => null,
            runNow:            () => { },
            toggleTaskEnabled: (_, _) => { });
        return (controller, listPanel, statusLabel, runNowButton);
    }

    private static MaintenanceMdConfig MakeConfig(IReadOnlyList<MaintenanceTask>? tasks = null) =>
        new(IdleTimeout: 15, MaxTasksPerSession: 5, Safety: "branch", Tasks: tasks ?? []);

    private static MaintenanceTask MakeTask(
        string id, string title, bool enabled = true, string frequency = "daily") =>
        new(id, enabled, frequency, "branch", title, "Do work.");

    /// <summary>
    /// Searches the logical tree of <paramref name="panel"/> for all CheckBox instances.
    /// Each task row is: Border → Grid → [CheckBox (col 0), StackPanel (col 1)].
    /// </summary>
    private static IEnumerable<CheckBox> FindCheckBoxes(StackPanel panel) {
        foreach (UIElement child in panel.Children) {
            if (child is Border border && border.Child is Grid grid)
                foreach (UIElement item in grid.Children)
                    if (item is CheckBox cb)
                        yield return cb;
        }
    }

    /// <summary>
    /// Recursively walks the logical tree rooted at <paramref name="parent"/> and
    /// collects all <see cref="TextBlock"/> instances.
    /// </summary>
    private static List<TextBlock> CollectTextBlocks(DependencyObject parent) {
        var result = new List<TextBlock>();
        CollectTextBlocksCore(parent, result);
        return result;
    }

    private static void CollectTextBlocksCore(DependencyObject parent, List<TextBlock> result) {
        if (parent is TextBlock tb)
            result.Add(tb);

        foreach (object child in LogicalTreeHelper.GetChildren(parent))
            if (child is DependencyObject dep)
                CollectTextBlocksCore(dep, result);
    }

    // ── Status header — idle ──────────────────────────────────────────────────

    [Test]
    public void Refresh_HeaderText_WhenIdle() {
        WpfTestContext.Run(() => {
            var (controller, _, statusLabel, _) = CreateController();
            var config = MakeConfig([MakeTask("t1", "Task One")]);

            controller.Refresh(config, stateStore: null);

            // With _nextMaintenanceAt == DateTimeOffset.MaxValue (the default) and no runner
            // active, the status label must be hidden.
            Assert.That(statusLabel.Visibility, Is.EqualTo(Visibility.Collapsed),
                "Status label must be collapsed when the runner is idle and no countdown is scheduled");
        });
    }

    // ── Status header — running ───────────────────────────────────────────────

    [Test]
    public void Refresh_HeaderText_WhenRunning() {
        WpfTestContext.Run(() => {
            var (controller, _, statusLabel, _) = CreateController();
            var config = MakeConfig([MakeTask("t1", "Build Checks")]);

            // Simulate runner starting before the next Refresh call
            controller.OnRunnerStarted("Build Checks");
            controller.Refresh(config, stateStore: null);

            Assert.That(statusLabel.Visibility, Is.EqualTo(Visibility.Visible),
                "Status label must be visible while the runner is active");
            Assert.That(statusLabel.Text, Does.Contain("Running"),
                "Status text must mention 'Running' while a task is executing");
            Assert.That(statusLabel.Text, Does.Contain("Build Checks"),
                "Status text must include the currently running task title");
        });
    }

    [Test]
    public void OnRunnerCompleted_ResetsHeaderToIdle() {
        WpfTestContext.Run(() => {
            var (controller, _, statusLabel, _) = CreateController();
            var config = MakeConfig([MakeTask("t1", "Lint")]);
            controller.Refresh(config, stateStore: null);

            controller.OnRunnerStarted("Lint");
            Assert.That(statusLabel.Text, Does.Contain("Running"),
                "Pre-condition: label must say Running");

            controller.OnRunnerCompleted();
            // After completion with MaxValue next time, label collapses again
            Assert.That(statusLabel.Visibility, Is.EqualTo(Visibility.Collapsed),
                "Status label must collapse again after the runner completes");
        });
    }

    // ── Checkbox state reflects config ────────────────────────────────────────

    [Test]
    public void Refresh_CheckboxState_ReflectsConfig() {
        WpfTestContext.Run(() => {
            var (controller, listPanel, _, _) = CreateController();
            var config = MakeConfig([
                MakeTask("task-a", "Enabled Task",  enabled: true),
                MakeTask("task-b", "Disabled Task", enabled: false),
            ]);

            controller.Refresh(config, stateStore: null);

            var checkBoxes = FindCheckBoxes(listPanel).ToList();
            Assert.That(checkBoxes, Has.Count.EqualTo(2),
                "Each configured task must produce exactly one checkbox");
            Assert.That(checkBoxes[0].IsChecked, Is.True,
                "Checkbox for an enabled task must be checked");
            Assert.That(checkBoxes[1].IsChecked, Is.False,
                "Checkbox for a disabled task must be unchecked");
        });
    }

    [Test]
    public void Refresh_EmptyConfig_ShowsNoCheckboxes() {
        WpfTestContext.Run(() => {
            var (controller, listPanel, _, _) = CreateController();
            var config = MakeConfig(tasks: []);  // no tasks

            controller.Refresh(config, stateStore: null);

            var checkBoxes = FindCheckBoxes(listPanel).ToList();
            Assert.That(checkBoxes, Is.Empty,
                "No checkboxes must appear when the config has no tasks");
        });
    }

    // ── Last-run info from state store ────────────────────────────────────────

    [Test]
    public void Refresh_LastRunInfo_ShownWhenAvailable() {
        WpfTestContext.Run(() => {
            var (controller, listPanel, _, _) = CreateController();
            var config = MakeConfig([MakeTask("tracked-task", "Tracked Task")]);

            var store = new MaintenanceStateStore(_stateDir);
            store.RecordRun("tracked-task", commitSha: null);

            controller.Refresh(config, store);

            var lastRunBlocks = CollectTextBlocks(listPanel)
                .Where(tb => tb.Text.Contains("Last run:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.That(lastRunBlocks, Is.Not.Empty,
                "A 'Last run:' TextBlock must appear when the state store has a recorded run");
        });
    }

    [Test]
    public void Refresh_LastRunInfo_AbsentWhenNoStateStore() {
        WpfTestContext.Run(() => {
            var (controller, listPanel, _, _) = CreateController();
            var config = MakeConfig([MakeTask("any-task", "Any Task")]);

            // Pass null state store — no last-run info can be resolved
            controller.Refresh(config, stateStore: null);

            var lastRunBlocks = CollectTextBlocks(listPanel)
                .Where(tb => tb.Text.Contains("Last run:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.That(lastRunBlocks, Is.Empty,
                "No 'Last run:' text should appear when no state store is provided");
        });
    }

    // ── First run — state file absent ─────────────────────────────────────────

    [Test]
    public void Refresh_FirstRun_WhenStateFileAbsent() {
        WpfTestContext.Run(() => {
            // Point to an empty sub-directory that contains no maintenance-state.json
            var emptyDir = Path.Combine(_stateDir, "empty");
            Directory.CreateDirectory(emptyDir);

            var (controller, listPanel, _, _) = CreateController();
            var config = MakeConfig([MakeTask("new-task", "Brand New Task")]);

            var store = new MaintenanceStateStore(emptyDir);
            store.Reload();  // no file exists — loads empty state gracefully

            Assert.DoesNotThrow(() => controller.Refresh(config, store),
                "Refresh must not throw when no maintenance-state.json exists yet");

            // The task has never run — no "Last run:" label must appear
            var lastRunBlocks = CollectTextBlocks(listPanel)
                .Where(tb => tb.Text.Contains("Last run:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.That(lastRunBlocks, Is.Empty,
                "No 'Last run:' info must appear for a task that has never run (first run scenario)");

            // At least one checkbox must be present (the task row was built)
            var checkBoxes = FindCheckBoxes(listPanel).ToList();
            Assert.That(checkBoxes, Has.Count.EqualTo(1),
                "The task row must still be rendered on first run");
        });
    }

    // ── Run Now button ────────────────────────────────────────────────────────

    [Test]
    public void RunNowButton_DisabledWhileRunning_ReEnabledOnComplete() {
        WpfTestContext.Run(() => {
            var (controller, _, _, runNowButton) = CreateController();

            Assert.That(runNowButton.IsEnabled, Is.True,
                "Run Now button must be enabled initially");

            controller.OnRunnerStarted("Some Task");
            Assert.That(runNowButton.IsEnabled, Is.False,
                "Run Now button must be disabled while a task is running");

            controller.OnRunnerCompleted();
            Assert.That(runNowButton.IsEnabled, Is.True,
                "Run Now button must be re-enabled after runner completes");
        });
    }
}
