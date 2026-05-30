#nullable enable

namespace SquadDash.PanelDocking;

/// <summary>
/// Identifies where a panel is docked within the main window.
/// </summary>
/// <remarks>
/// Top is the default zone — the existing horizontal panel strip (Row 2 of MainGrid).
/// Left and Right are vertical stacks added on either side of the main content area.
/// Future zones (Left2, Right2, …) can be appended without breaking existing serialized layouts,
/// provided unknown values are gracefully handled as Top during deserialization.
/// </remarks>
public enum DockZone
{
    Top,
    Left,
    Right
    // Future: Left2, Right2, Bottom, …
}
