namespace SquadDash;

using System.Collections.Generic;

internal sealed class InboxPanelViewModel {
    public List<InboxMessage> Messages { get; set; } = [];
    public string FilterText { get; set; } = string.Empty;
    public bool UnreadOnly { get; set; }
    public InboxMessage? SelectedMessage { get; set; }
}
