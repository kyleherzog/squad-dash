namespace SquadDash;

/// <summary>
/// The four AI behavioural policy strings injected into every prompt by
/// <see cref="PromptExecutionController"/>. Extracted from inline constants
/// so they can be changed without recompiling the controller.
/// </summary>
internal sealed record PromptInstructionSet(
    string TurnSummary,
    string InboxMessage,
    string QuickReply,
    string CoordinatorDelegationAccountability);
