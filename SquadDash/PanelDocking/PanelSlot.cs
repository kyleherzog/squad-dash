#nullable enable

namespace SquadDash.PanelDocking;

/// <summary>
/// Describes where one panel lives within a <see cref="DockLayout"/>.
/// </summary>
/// <param name="PanelId">
/// Stable lower-case identifier for the panel (e.g. "tasks", "inbox", "docs").
/// Must not change across versions — it is persisted in saved layouts.
/// </param>
/// <param name="Zone">The dock zone this panel is assigned to.</param>
/// <param name="Order">
/// Zero-based position within the zone (lower = closer to origin).
/// For Top: left-to-right. For Left/Right: top-to-bottom.
/// </param>
public sealed record PanelSlot(string PanelId, DockZone Zone, int Order);
