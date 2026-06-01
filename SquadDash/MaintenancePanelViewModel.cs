namespace SquadDash;

using System;
using System.Collections.Generic;
using System.Windows.Controls;

internal sealed class MaintenancePanelViewModel {
    public MaintenanceMdConfig?   Config           { get; set; }
    public MaintenanceStateStore? StateStore       { get; set; }
    public bool                   RunnerActive     { get; set; }
    public string?                RunningTaskTitle { get; set; }
    public string                 FilterText       { get; set; } = string.Empty;
    public DateTimeOffset         NextMaintenanceAt { get; set; } = DateTimeOffset.MaxValue;
    public List<(string TaskId, StackPanel OptionsPanel)> TaskOptionsPanels { get; } = new();
}
