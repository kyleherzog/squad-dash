using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SquadDash;

/// <summary>Orchestrates maintenance task execution against the configured task list.</summary>
internal sealed class MaintenanceRunner {

    private readonly Func<string, CancellationToken, Task> _executePromptAsync;
    private readonly MaintenanceStateStore                  _stateStore;
    private readonly Action<string>                         _onTaskStarted;
    private readonly Action<string>                         _onTaskCompleted;
    private readonly Action<MaintenanceReport>              _onCompleted;

    private volatile bool _isRunning;

    public bool IsRunning => _isRunning;

    public MaintenanceRunner(
        Func<string, CancellationToken, Task> executePromptAsync,
        MaintenanceStateStore                  stateStore,
        Action<string>                         onTaskStarted,
        Action<string>                         onTaskCompleted,
        Action<MaintenanceReport>              onCompleted) {

        _executePromptAsync = executePromptAsync;
        _stateStore         = stateStore;
        _onTaskStarted      = onTaskStarted;
        _onTaskCompleted    = onTaskCompleted;
        _onCompleted        = onCompleted;
    }

    /// <summary>
    /// Runs eligible maintenance tasks in order. Awaitable — completes when all tasks have run.
    /// </summary>
    public async Task StartAsync(
        MaintenanceMdConfig  config,
        string               workspacePath,
        CancellationToken    ct) {

        _isRunning = true;
        var startedAt = DateTimeOffset.UtcNow;
        var ranIds     = new List<string>();
        var skippedIds = new List<string>();
        var results    = new List<MaintenanceTaskResult>();

        try {
            // Try to obtain the HEAD commit SHA (best-effort; null if unavailable).
            string? commitSha = TryGetCommitSha(workspacePath);

            int runCount = 0;

            foreach (var task in config.Tasks ?? []) {
                if (ct.IsCancellationRequested)
                    break;

                if (!task.Enabled)
                    continue;

                if (!_stateStore.IsEligible(task.Id, task.Frequency, commitSha)) {
                    skippedIds.Add(task.Id);
                    continue;
                }

                if (runCount >= config.MaxTasksPerSession)
                    break;

                _onTaskStarted(task.Title);
                runCount++;

                var taskStart = Stopwatch.GetTimestamp();
                try {
                    var prompt = BuildPrompt(task);
                    await _executePromptAsync(prompt, ct).ConfigureAwait(false);

                    var elapsed = Stopwatch.GetElapsedTime(taskStart);
                    _stateStore.RecordRun(task.Id, commitSha);
                    ranIds.Add(task.Id);
                    results.Add(new MaintenanceTaskResult(
                        Id:       task.Id,
                        Title:    task.Title,
                        Outcome:  MaintenanceTaskOutcome.Completed,
                        Duration: elapsed));
                    _onTaskCompleted(task.Id);
                }
                catch (OperationCanceledException) {
                    break;
                }
                catch (Exception ex) {
                    var elapsed = Stopwatch.GetElapsedTime(taskStart);
                    ranIds.Add(task.Id);
                    results.Add(new MaintenanceTaskResult(
                        Id:           task.Id,
                        Title:        task.Title,
                        Outcome:      MaintenanceTaskOutcome.Error,
                        Duration:     elapsed,
                        ErrorMessage: ex.Message));
                    _onTaskCompleted(task.Id);
                }
            }

            var report = new MaintenanceReport {
                RanTaskIds     = ranIds,
                SkippedTaskIds = skippedIds,
                TaskResults    = results,
                StartedAt      = startedAt,
                CompletedAt    = DateTimeOffset.UtcNow,
            };

            _onCompleted(report);
        }
        finally {
            _isRunning = false;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string BuildPrompt(MaintenanceTask task) {
        var safetyPrefix = task.Safety switch {
            "report-only" =>
                "REPORT ONLY: You are running in read-only, reporting mode. " +
                "Do not make any changes to files, branches, or commits. " +
                "Only observe and report what you find.\n\n",
            "branch" =>
                "Create a new branch before making any changes. " +
                "Do not commit directly to the default branch.\n\n",
            _ => string.Empty,
        };

        return safetyPrefix + task.Instructions;
    }

    private static string? TryGetCommitSha(string workspacePath) {
        try {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD") {
                WorkingDirectory       = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return null;
            var sha = proc.StandardOutput.ReadLine()?.Trim();
            proc.WaitForExit(3000);
            return string.IsNullOrEmpty(sha) ? null : sha;
        }
        catch {
            return null;
        }
    }
}
