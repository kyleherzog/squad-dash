namespace SquadDash;

internal interface ISquadBridgePromptBuilder {
    BuildResult Build(
        string prompt,
        string quickReplyInstruction,
        string? quickReplyRoutingInstruction,
        string? quickReplyRouteMode,
        string? supplementalInstruction,
        string? workspaceFolder,
        string? coordinatorDelegationAccountabilityInstruction = null);
}
