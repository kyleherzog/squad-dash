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

                // Runtime safety floor check — log a warning if the global floor overrides the task's declared safety.
                var effectiveSafety = ApplySafetyFloor(config.Safety, task.Safety);
                string? safetyOverrideNote = null;
                if (!string.Equals(effectiveSafety, task.Safety, StringComparison.OrdinalIgnoreCase)) {
                    safetyOverrideNote = $"Safety downgraded from '{task.Safety}' to '{effectiveSafety}' by global floor.";
                    SquadDashTrace.Write(TraceCategory.General,
                        $"MaintenanceRunner: task '{task.Id}' safety override — declared '{task.Safety}', effective '{effectiveSafety}' (global floor '{config.Safety}').");
                }

                var taskStart = Stopwatch.GetTimestamp();
                try {
                    var prompt = BuildPrompt(task, config.Safety, startedAt);
                    await _executePromptAsync(prompt, ct).ConfigureAwait(false);

                    var elapsed = Stopwatch.GetElapsedTime(taskStart);
                    _stateStore.RecordRun(task.Id, commitSha);
                    ranIds.Add(task.Id);
                    results.Add(new MaintenanceTaskResult(
                        Id:                 task.Id,
                        Title:              task.Title,
                        Outcome:            MaintenanceTaskOutcome.Completed,
                        Duration:           elapsed,
                        SafetyOverrideNote: safetyOverrideNote));
                    _onTaskCompleted(task.Id);
                }
                catch (OperationCanceledException) {
                    break;
                }
                catch (Exception ex) {
                    var elapsed = Stopwatch.GetElapsedTime(taskStart);
                    ranIds.Add(task.Id);
                    results.Add(new MaintenanceTaskResult(
                        Id:                 task.Id,
                        Title:              task.Title,
                        Outcome:            MaintenanceTaskOutcome.Error,
                        Duration:           elapsed,
                        ErrorMessage:       ex.Message,
                        SafetyOverrideNote: safetyOverrideNote));
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

    private static string BuildPrompt(MaintenanceTask task, string globalSafety, DateTimeOffset runDate) {
        var effectiveSafety = ApplySafetyFloor(globalSafety, task.Safety);
        var branchName      = $"maintenance/{runDate:yyyyMMdd}-{task.Id}";

        var safetyPrefix = effectiveSafety switch {
            "report-only" => "Do not modify any source files. Generate a report only.\n\n",
            "branch"      => $"Create branch `{branchName}` before making any code changes. Commit to that branch only.\n\n",
            "direct"      => "You may commit directly to the current branch.\n\n",
            _             => string.Empty,
        };

        return safetyPrefix + task.Instructions;
    }

    private static string ApplySafetyFloor(string globalSafety, string taskSafety) {
        static int Rank(string s) => s switch {
            "report-only" => 2,
            "branch"      => 1,
            "direct"      => 0,
            _             => 0,
        };
        return Rank(globalSafety) >= Rank(taskSafety) ? globalSafety : taskSafety;
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
