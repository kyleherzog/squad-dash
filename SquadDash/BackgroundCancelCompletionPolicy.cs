namespace SquadDash;

internal static class BackgroundCancelCompletionPolicy {
    internal static bool ShouldForceFinalize(bool cancelAcknowledged) => cancelAcknowledged;
}
