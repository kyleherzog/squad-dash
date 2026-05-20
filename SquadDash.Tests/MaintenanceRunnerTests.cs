using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SquadDash.Tests;

/// <summary>
/// Behavioral specs for <see cref="MaintenanceRunner"/>.
/// Tests will compile once Arjun Sen's Phase 1 implementation lands.
/// </summary>
[TestFixture]
internal sealed class MaintenanceRunnerTests {

    // ── Fixtures ──────────────────────────────────────────────────────────────

    private string _stateDir = null!;
    private string _workspaceDir = null!;

    [SetUp]
    public void SetUp() {
        var root = TestContext.CurrentContext.WorkDirectory;
        _stateDir    = Path.Combine(root, $"maint_runner_state_{Guid.NewGuid():N}");
        _workspaceDir = Path.Combine(root, $"maint_runner_ws_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_stateDir);
        Directory.CreateDirectory(_workspaceDir);
    }

    [TearDown]
    public void TearDown() {
        foreach (var dir in new[] { _stateDir, _workspaceDir }) {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static MaintenanceMdConfig MakeConfig(
        IReadOnlyList<MaintenanceTask>? tasks = null,
        int maxTasksPerSession = 10,
        string safety = "branch") =>
        new(
            IdleTimeout: 15,
            MaxTasksPerSession: maxTasksPerSession,
            Safety: safety,
            Tasks: tasks ?? []);

    private static MaintenanceTask MakeTask(
        string id,
        bool enabled = true,
        string frequency = "always",
        string safety = "branch",
        string title = "",
        string instructions = "Do work.") =>
        new(id, enabled, frequency, safety, title.Length > 0 ? title : id, instructions);

    private MaintenanceRunner MakeRunner(
        Func<string, CancellationToken, Task>? executePromptAsync = null,
        MaintenanceStateStore? stateStore = null,
        Action<string>? onTaskStarted = null,
        Action<string>? onTaskCompleted = null,
        Action<MaintenanceReport>? onCompleted = null) {
        return new MaintenanceRunner(
            executePromptAsync: executePromptAsync ?? ((_, _) => Task.CompletedTask),
            stateStore:         stateStore ?? new MaintenanceStateStore(_stateDir),
            onTaskStarted:      onTaskStarted  ?? (_ => { }),
            onTaskCompleted:    onTaskCompleted ?? (_ => { }),
            onCompleted:        onCompleted    ?? (_ => { }));
    }

    // ── Skips disabled tasks ──────────────────────────────────────────────────

    [Test]
    public async Task StartAsync_SkipsDisabledTasks() {
        var executed = new List<string>();
        var config = MakeConfig([
            MakeTask("disabled-task", enabled: false),
            MakeTask("enabled-task",  enabled: true),
        ]);

        var runner = MakeRunner(
            executePromptAsync: (prompt, _) => { executed.Add(prompt); return Task.CompletedTask; });

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(executed, Has.Count.EqualTo(1), "Only the enabled task should be executed");
    }

    // ── Skips ineligible tasks ────────────────────────────────────────────────

    [Test]
    public async Task StartAsync_SkipsIneligibleTasks() {
        const string taskId = "ran-today";
        var stateStore = new MaintenanceStateStore(_stateDir);
        stateStore.RecordRun(taskId, commitSha: null);

        var executed = new List<string>();
        var config = MakeConfig([
            MakeTask(taskId, frequency: "daily"),
            MakeTask("always-task", frequency: "always"),
        ]);

        var runner = MakeRunner(
            executePromptAsync: (prompt, _) => { executed.Add(prompt); return Task.CompletedTask; },
            stateStore: stateStore);

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(executed, Has.Count.EqualTo(1), "The daily task run today must be skipped");
    }

    // ── Runs eligible tasks in order ─────────────────────────────────────────

    [Test]
    public async Task StartAsync_RunsEligibleTasksInOrder() {
        var order = new List<string>();
        var config = MakeConfig([
            MakeTask("task-a", instructions: "Instructions A"),
            MakeTask("task-b", instructions: "Instructions B"),
            MakeTask("task-c", instructions: "Instructions C"),
        ]);

        var runner = MakeRunner(
            onTaskStarted: title => order.Add(title));

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(order, Is.EqualTo(new[] { "task-a", "task-b", "task-c" }),
            "Tasks must be started in the order they appear in the config");
    }

    // ── Respects MaxTasksPerSession ───────────────────────────────────────────

    [Test]
    public async Task StartAsync_RespectsMaxTasksPerSessionCap() {
        var started = new List<string>();
        var config = MakeConfig(
            tasks: [
                MakeTask("t1"), MakeTask("t2"), MakeTask("t3"),
                MakeTask("t4"), MakeTask("t5"), MakeTask("t6"),
            ],
            maxTasksPerSession: 3);

        var runner = MakeRunner(onTaskStarted: id => started.Add(id));

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(started, Has.Count.EqualTo(3),
            "Runner must stop after MaxTasksPerSession tasks regardless of how many are eligible");
    }

    // ── RecordRun called after each success ───────────────────────────────────

    [Test]
    public async Task StartAsync_CallsRecordRunAfterEachSuccessfulTask() {
        var stateStore = new MaintenanceStateStore(_stateDir);
        var config = MakeConfig([
            MakeTask("rec-task-1", frequency: "daily"),
            MakeTask("rec-task-2", frequency: "daily"),
        ]);

        var runner = MakeRunner(stateStore: stateStore);

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        // After running both daily tasks they must now be ineligible.
        Assert.That(stateStore.IsEligible("rec-task-1", "daily", null), Is.False,
            "rec-task-1 must be marked as run");
        Assert.That(stateStore.IsEligible("rec-task-2", "daily", null), Is.False,
            "rec-task-2 must be marked as run");
    }

    // ── Cancellation token interrupt ──────────────────────────────────────────

    [Test]
    public async Task StartAsync_CancellationToken_StopsAfterCurrentTask() {
        var startedIds = new List<string>();
        using var cts = new CancellationTokenSource();

        // Cancel after the first task begins.
        var firstTaskTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var config = MakeConfig([
            MakeTask("cancel-task-1"),
            MakeTask("cancel-task-2"),
        ]);

        var runner = MakeRunner(
            executePromptAsync: async (_, ct) => {
                firstTaskTcs.TrySetResult();
                await Task.Delay(50, ct);
            },
            onTaskStarted: id => {
                startedIds.Add(id);
                if (id == "cancel-task-1")
                    cts.Cancel();
            });

        await runner.StartAsync(config, _workspaceDir, cts.Token).ConfigureAwait(false);

        Assert.That(startedIds, Does.Not.Contain("cancel-task-2"),
            "Runner must not start the next task after cancellation");
    }

    // ── onTaskStarted callback ────────────────────────────────────────────────

    [Test]
    public async Task StartAsync_OnTaskStarted_FiredWithTaskTitle() {
        var titles = new List<string>();
        var config = MakeConfig([
            MakeTask("greet-task", title: "Say Hello"),
        ]);

        var runner = MakeRunner(onTaskStarted: title => titles.Add(title));

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(titles, Has.Count.EqualTo(1));
        Assert.That(titles[0], Is.EqualTo("Say Hello"),
            "onTaskStarted must receive the task title");
    }

    // ── onTaskCompleted callback ──────────────────────────────────────────────

    [Test]
    public async Task StartAsync_OnTaskCompleted_FiredWithTaskId() {
        var completedIds = new List<string>();
        var config = MakeConfig([
            MakeTask("complete-me"),
        ]);

        var runner = MakeRunner(onTaskCompleted: id => completedIds.Add(id));

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(completedIds, Has.Count.EqualTo(1));
        Assert.That(completedIds[0], Is.EqualTo("complete-me"),
            "onTaskCompleted must receive the task id");
    }

    // ── onCompleted (MaintenanceReport) ──────────────────────────────────────

    [Test]
    public async Task StartAsync_OnCompleted_FiredWithRunAndSkippedTaskLists() {
        MaintenanceReport? report = null;

        var stateStore = new MaintenanceStateStore(_stateDir);
        stateStore.RecordRun("skipped-daily", commitSha: null);

        var config = MakeConfig([
            MakeTask("run-task",     frequency: "always"),
            MakeTask("skipped-daily", frequency: "daily"),
        ]);

        var runner = MakeRunner(
            stateStore: stateStore,
            onCompleted: r => report = r);

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(report, Is.Not.Null, "onCompleted must be invoked");
        Assert.Multiple(() => {
            Assert.That(report!.RanTaskIds,     Does.Contain("run-task"));
            Assert.That(report.SkippedTaskIds,  Does.Contain("skipped-daily"));
        });
    }

    // ── Safety prompt injection — report-only ─────────────────────────────────

    [Test]
    public async Task StartAsync_ReportOnlyTask_PromptContainsReportOnlyPrefix() {
        var capturedPrompt = string.Empty;
        var config = MakeConfig(
            tasks: [MakeTask("analysis", safety: "report-only", instructions: "Analyse the diff.")],
            safety: "report-only");

        var runner = MakeRunner(
            executePromptAsync: (prompt, _) => { capturedPrompt = prompt; return Task.CompletedTask; });

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(capturedPrompt, Does.Contain("report").IgnoreCase.Or.Contains("read-only").IgnoreCase,
            "report-only safety must inject a read-only / reporting prefix into the prompt");
        Assert.That(capturedPrompt, Does.Not.Contain("direct commit").IgnoreCase,
            "report-only prompt must not instruct the agent to commit directly");
    }

    // ── Safety prompt injection — branch ─────────────────────────────────────

    [Test]
    public async Task StartAsync_BranchTask_PromptContainsBranchInstruction() {
        var capturedPrompt = string.Empty;
        var config = MakeConfig(
            tasks: [MakeTask("refactor", safety: "branch", instructions: "Refactor the module.")],
            safety: "branch");

        var runner = MakeRunner(
            executePromptAsync: (prompt, _) => { capturedPrompt = prompt; return Task.CompletedTask; });

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(capturedPrompt, Does.Contain("branch").IgnoreCase,
            "branch safety must inject a branch-creation instruction into the prompt");
    }

    // ── IsRunning ────────────────────────────────────────────────────────────

    [Test]
    public async Task IsRunning_TrueWhileRunning_FalseAfterComplete() {
        bool? isRunningDuringTask = null;
        var config = MakeConfig([MakeTask("running-probe")]);

        MaintenanceRunner? runner = null;
        runner = MakeRunner(
            executePromptAsync: (_, _) => {
                isRunningDuringTask = runner!.IsRunning;
                return Task.CompletedTask;
            });

        Assert.That(runner.IsRunning, Is.False, "IsRunning must be false before StartAsync");

        await runner.StartAsync(config, _workspaceDir, CancellationToken.None);

        Assert.That(isRunningDuringTask, Is.True,  "IsRunning must be true while a task executes");
        Assert.That(runner.IsRunning,    Is.False,  "IsRunning must be false after completion");
    }
}
