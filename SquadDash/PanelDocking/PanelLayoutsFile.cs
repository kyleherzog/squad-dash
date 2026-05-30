#nullable enable

namespace SquadDash.PanelDocking;

/// <summary>
/// Root DTO for the per-workspace <c>.squad/panel-layouts.json</c> file.
/// Contains all named layouts and tracks which one is currently active.
/// </summary>
public sealed class PanelLayoutsFile
{
    /// <summary>Name of the layout that was active when the file was last saved.</summary>
    public string ActiveLayout { get; set; } = "Default";

    /// <summary>All layouts stored for this workspace.</summary>
    public List<DockLayout> Layouts { get; set; } = new();
}
