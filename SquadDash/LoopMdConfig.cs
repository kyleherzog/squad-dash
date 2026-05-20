using System.Collections.Generic;

namespace SquadDash;

/// <summary>
/// A single typed option defined in the <c>options:</c> block of a loop file's frontmatter.
/// </summary>
internal sealed record LoopOption(
    string Key,
    string RawValue,
    string Type,
    string? Label,
    string? Hint,
    IReadOnlyList<string>? Choices);

/// <summary>
/// A typed option parsed from a maintenance.md task's <c>options:</c> block.
/// Unlike <see cref="LoopOption"/>, each choice carries an optional tooltip.
/// </summary>
internal sealed record MaintenanceOption(
    string Key,
    string RawValue,
    string Type,
    string? Label,
    string? Tooltip,
    IReadOnlyList<MaintenanceOptionChoice>? Choices);

/// <summary>
/// Parsed configuration from a loop.md frontmatter block.
/// </summary>
internal sealed record LoopMdConfig(
    double IntervalMinutes,
    double TimeoutMinutes,
    string Description,
    string Instructions,
    IReadOnlyList<string>? Commands = null,
    IReadOnlyList<LoopOption>? Options = null);
