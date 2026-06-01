namespace SquadDash;

using System.Collections.Generic;

internal sealed class NotesPanelViewModel {
    public List<NoteItem> Notes { get; set; } = [];
    public string FilterText { get; set; } = string.Empty;
    public NotesSortOrder SortOrder { get; set; } = NotesSortOrder.MostRecentOnTop;
}
