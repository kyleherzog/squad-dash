namespace SquadDash;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

/// <summary>Manages content in the inline Maintenance panel.</summary>
internal sealed class MaintenancePanelController {

    private readonly StackPanel           _listPanel;
    private readonly TextBlock            _statusLabel;
    private readonly Button               _runNowButton;
    private readonly Func<string?>        _getWorkspacePath;
    private readonly Action               _runNow;
    private readonly Action<string, bool> _toggleTaskEnabled;

    private MaintenanceMdConfig?   _config;
    private MaintenanceStateStore? _stateStore;
    private bool                   _runnerActive;
    private string?                _runningTaskTitle;
    private DispatcherTimer?       _countdownTimer;
    private DateTimeOffset         _nextMaintenanceAt = DateTimeOffset.MaxValue;

    // ── Construction ─────────────────────────────────────────────────────────

    internal MaintenancePanelController(
        StackPanel           listPanel,
        TextBlock            statusLabel,
        Button               runNowButton,
        Func<string?>        getWorkspacePath,
        Action               runNow,
        Action<string, bool> toggleTaskEnabled) {

        _listPanel          = listPanel;
        _statusLabel        = statusLabel;
        _runNowButton       = runNowButton;
        _getWorkspacePath   = getWorkspacePath;
        _runNow             = runNow;
        _toggleTaskEnabled  = toggleTaskEnabled;

        _runNowButton.Click += (_, _) => _runNow();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    internal void Refresh(MaintenanceMdConfig? config, MaintenanceStateStore? stateStore) {
        _config     = config;
        _stateStore = stateStore;
        RebuildList();
    }

    /// <summary>
    /// Call when the runner starts a task. Updates the header to "Running now — [title]…"
    /// and disables the Run Now button.
    /// </summary>
    internal void OnRunnerStarted(string taskTitle) {
        _runnerActive      = true;
        _runningTaskTitle  = taskTitle;
        StopCountdown();
        SyncStatusLabel();
        _runNowButton.IsEnabled = false;
    }

    /// <summary>
    /// Call when the runner finishes. Re-enables the Run Now button and restarts the countdown.
    /// </summary>
    internal void OnRunnerCompleted() {
        _runnerActive      = false;
        _runningTaskTitle  = null;
        _runNowButton.IsEnabled = true;
        SyncStatusLabel();
    }

    /// <summary>
    /// Sets the next expected idle/maintenance time for the countdown display.
    /// Pass <see cref="DateTimeOffset.MaxValue"/> to hide the countdown.
    /// </summary>
    internal void SetNextMaintenanceAt(DateTimeOffset next) {
        _nextMaintenanceAt = next;
        if (!_runnerActive)
        {
            StopCountdown();
            StartCountdown();
        }
    }

    // ── List construction ─────────────────────────────────────────────────────

    private void RebuildList() {
        _listPanel.Children.Clear();

        if (_config is null || _config.Tasks is null || _config.Tasks.Count == 0) {
            var empty = new TextBlock {
                Text         = "No maintenance tasks configured.",
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(4, 6, 4, 4),
                FontStyle    = FontStyles.Italic,
            };
            empty.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeBody");
            empty.SetResourceReference(TextBlock.ForegroundProperty, "SubtleText");
            _listPanel.Children.Add(empty);
        } else {
            foreach (var task in _config.Tasks)
                _listPanel.Children.Add(BuildTaskRow(task));
        }

        SyncStatusLabel();
        AppendReportsSection();
    }

    private void AppendReportsSection() {
        var workspacePath = _getWorkspacePath();
        IReadOnlyList<string> reportPaths = workspacePath is null
            ? []
            : new MaintenanceReportWriter(workspacePath).GetReportPaths();

        var separator = new Separator { Margin = new Thickness(0, 8, 0, 4) };
        _listPanel.Children.Add(separator);

        var headerLabel = new TextBlock { Text = "Recent Reports" };
        headerLabel.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSmall");
        headerLabel.SetResourceReference(TextBlock.ForegroundProperty, "SubtleText");

        var expander = new Expander {
            Header     = "Recent Reports",
            IsExpanded = false,
            Margin     = new Thickness(0, 0, 0, 4),
        };

        var contentPanel = new StackPanel { Margin = new Thickness(8, 4, 0, 4) };

        if (reportPaths.Count == 0) {
            var noReports = new TextBlock {
                Text      = "No reports yet.",
                FontStyle = FontStyles.Italic,
            };
            noReports.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSmall");
            noReports.SetResourceReference(TextBlock.ForegroundProperty, "SubtleText");
            contentPanel.Children.Add(noReports);
        } else {
            foreach (var path in reportPaths) {
                var (dt, taskCount) = ParseReportSummary(path);
                contentPanel.Children.Add(BuildReportRow(path, dt, taskCount));
            }
        }

        expander.Content = contentPanel;
        _listPanel.Children.Add(expander);
    }

    private static (DateTimeOffset dt, int taskCount) ParseReportSummary(string path) {
        var baseName = Path.GetFileNameWithoutExtension(path);
        DateTimeOffset dt = DateTimeOffset.MinValue;
        if (baseName.Length >= 15 &&
            DateTime.TryParseExact(baseName[..15], "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            dt = new DateTimeOffset(parsed, TimeZoneInfo.Local.GetUtcOffset(parsed));

        int taskCount = 0;
        try {
            var lines = File.ReadAllLines(path);
            bool inTasksSection = false;
            foreach (var line in lines) {
                if (line.TrimEnd() == "## Tasks Run") {
                    inTasksSection = true;
                    continue;
                }
                if (inTasksSection) {
                    if (line.StartsWith("## ", StringComparison.Ordinal)) break;
                    if (line.StartsWith("- ", StringComparison.Ordinal) &&
                        !line.Contains("No tasks were run this session."))
                        taskCount++;
                }
            }
        } catch { /* ignore read errors */ }

        return (dt, taskCount);
    }

    private static Button BuildReportRow(string path, DateTimeOffset dt, int taskCount) {
        var taskWord = taskCount == 1 ? "task" : "tasks";
        var label = dt != DateTimeOffset.MinValue
            ? $"{dt.LocalDateTime:yyyy-MM-dd HH:mm}  •  {taskCount} {taskWord}"
            : $"{Path.GetFileNameWithoutExtension(path)}  •  {taskCount} {taskWord}";

        var btn = new Button {
            Content                    = label,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background                 = Brushes.Transparent,
            BorderThickness            = new Thickness(0),
            Padding                    = new Thickness(4, 3, 4, 3),
            Margin                     = new Thickness(0, 1, 0, 1),
            Cursor                     = Cursors.Hand,
        };
        btn.SetResourceReference(Button.FontSizeProperty, "FontSizeSmall");
        btn.SetResourceReference(Button.ForegroundProperty, "BodyText");
        btn.Click += (_, _) => OpenReport(path);
        return btn;
    }

    private static void OpenReport(string path) {
        try {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        } catch (Exception ex) {
            SquadDashTrace.Write(TraceCategory.General,
                $"MaintenancePanelController: failed to open report {path}: {ex.Message}");
        }
    }

    private Border BuildTaskRow(MaintenanceTask task) {
        var row = new Border {
            Padding    = new Thickness(0, 6, 0, 6),
            Background = Brushes.Transparent,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Child = grid;

        // ── Checkbox ─────────────────────────────────────────────────────────
        var check = new CheckBox {
            IsChecked         = task.Enabled,
            VerticalAlignment = VerticalAlignment.Top,
            Margin            = new Thickness(0, 1, 8, 0),
        };
        check.SetResourceReference(CheckBox.FontSizeProperty, "FontSizeBody");
        check.SetResourceReference(CheckBox.ForegroundProperty, "LabelText");
        Grid.SetColumn(check, 0);
        check.Checked   += (_, _) => _toggleTaskEnabled(task.Id, true);
        check.Unchecked += (_, _) => _toggleTaskEnabled(task.Id, false);
        grid.Children.Add(check);

        // ── Right column: title + chips + options ─────────────────────────────
        var rightPanel = new StackPanel { Margin = new Thickness(0) };
        Grid.SetColumn(rightPanel, 1);
        grid.Children.Add(rightPanel);

        // Title
        var titleBlock = new TextBlock {
            Text         = task.Title,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 0, 0, 2),
        };
        titleBlock.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeBody");
        titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "LabelText");
        rightPanel.Children.Add(titleBlock);

        // Chips: frequency + safety
        var chipRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        chipRow.Children.Add(BuildChip(task.Frequency, null));
        if (!string.Equals(task.Safety, "branch", StringComparison.OrdinalIgnoreCase))
            chipRow.Children.Add(BuildChip(task.Safety, null));
        rightPanel.Children.Add(chipRow);

        // Last-run status
        var lastRun = _stateStore?.GetLastRunAt(task.Id);
        if (lastRun.HasValue) {
            var relTime = StatusTimingPresentation.FormatRelativeTimestamp(
                new DateTimeOffset(lastRun.Value, TimeSpan.Zero));
            var lastRunBlock = new TextBlock {
                Text   = $"Last run: {relTime}",
                Margin = new Thickness(0, 3, 0, 0),
            };
            lastRunBlock.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSmall");
            lastRunBlock.SetResourceReference(TextBlock.ForegroundProperty, "SubtleText");
            rightPanel.Children.Add(lastRunBlock);
        }

        // Radio options (if present)
        if (task.Options is { Count: > 0 }) {
            var optionsPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var opt in task.Options) {
                var rb = new RadioButton {
                    Content         = opt.Label,
                    GroupName       = $"task-{task.Id}",
                    IsChecked       = false,
                    Margin          = new Thickness(0, 1, 0, 1),
                };
                rb.SetResourceReference(RadioButton.FontSizeProperty, "FontSizeSmall");
                rb.SetResourceReference(RadioButton.ForegroundProperty, "BodyText");
                optionsPanel.Children.Add(rb);
            }
            rightPanel.Children.Add(optionsPanel);
        }

        return row;
    }

    private static Border BuildChip(string text, string? tooltip) {
        var label = new TextBlock { Text = text };
        label.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeXSmall");
        label.SetResourceReference(TextBlock.ForegroundProperty, "SubtleText");

        var chip = new Border {
            Child         = label,
            Padding       = new Thickness(5, 1, 5, 1),
            Margin        = new Thickness(0, 0, 4, 2),
            CornerRadius  = new CornerRadius(8),
        };
        chip.SetResourceReference(Border.BackgroundProperty, "InputSurface");
        chip.SetResourceReference(Border.BorderBrushProperty, "InputBorder");
        chip.BorderThickness = new Thickness(1);
        if (tooltip is not null)
            chip.ToolTip = tooltip;
        return chip;
    }

    // ── Status header ─────────────────────────────────────────────────────────

    private void SyncStatusLabel() {
        if (_runnerActive) {
            var title = _runningTaskTitle ?? "task";
            _statusLabel.Text = $"● Running — {title}…";
            _statusLabel.Visibility = Visibility.Visible;
            return;
        }

        if (_config is null) {
            _statusLabel.Visibility = Visibility.Collapsed;
            return;
        }

        // If no next time set, hide the countdown
        if (_nextMaintenanceAt == DateTimeOffset.MaxValue) {
            _statusLabel.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateCountdownLabel();
    }

    private void UpdateCountdownLabel() {
        var remaining = _nextMaintenanceAt - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero) {
            _statusLabel.Text       = "Maintenance window — idle…";
            _statusLabel.Visibility = Visibility.Visible;
            return;
        }

        var text = remaining.TotalHours >= 1
            ? $"Next maintenance in: {(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
            : remaining.TotalMinutes >= 1
                ? $"Next maintenance in: {(int)remaining.TotalMinutes}m {remaining.Seconds:D2}s"
                : $"Next maintenance in: {(int)remaining.TotalSeconds}s";

        _statusLabel.Text       = text;
        _statusLabel.Visibility = Visibility.Visible;
    }

    // ── Countdown timer ───────────────────────────────────────────────────────

    private void StartCountdown() {
        if (_nextMaintenanceAt == DateTimeOffset.MaxValue) return;
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _countdownTimer.Tick += (_, _) => UpdateCountdownLabel();
        _countdownTimer.Start();
        UpdateCountdownLabel();
    }

    private void StopCountdown() {
        _countdownTimer?.Stop();
        _countdownTimer = null;
    }
}
