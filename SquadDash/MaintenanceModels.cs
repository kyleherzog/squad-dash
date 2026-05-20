using System;
using System.Collections.Generic;

namespace SquadDash;

internal enum MaintenanceTaskOutcome { Completed, Skipped, Error, Interrupted }

internal sealed record MaintenanceTaskResult(
    string               Id,
    string               Title,
    MaintenanceTaskOutcome Outcome,
    TimeSpan             Duration,
    string?              BranchCreated  = null,
    IReadOnlyList<string>? FilesChanged = null,
    string?              ErrorMessage   = null);

internal sealed class MaintenanceReport {
    public required IReadOnlyList<string>                RanTaskIds     { get; init; }
    public required IReadOnlyList<string>                SkippedTaskIds { get; init; }
    public required IReadOnlyList<MaintenanceTaskResult> TaskResults    { get; init; }
    public required DateTimeOffset                       StartedAt      { get; init; }
    public required DateTimeOffset                       CompletedAt    { get; init; }
    public          string?                              Summary        { get; init; }

    public TimeSpan Duration => CompletedAt - StartedAt;
}
