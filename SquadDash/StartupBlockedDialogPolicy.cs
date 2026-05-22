using System;

namespace SquadDash;

internal static class StartupBlockedDialogPolicy {
    public static bool HasPendingRestartRequest(string applicationRoot) {
        try {
            return HasPendingRestartRequest(applicationRoot, new RestartCoordinatorStateStore());
        }
        catch (Exception ex) {
            TraceCheckFailure(ex);
            return false;
        }
    }

    internal static bool HasPendingRestartRequest(
        string applicationRoot,
        RestartCoordinatorStateStore restartStateStore) {
        if (string.IsNullOrWhiteSpace(applicationRoot))
            return false;

        try {
            return restartStateStore.LoadRequest(applicationRoot) is not null;
        }
        catch (Exception ex) {
            TraceCheckFailure(ex);
            return false;
        }
    }

    private static void TraceCheckFailure(Exception ex) {
        SquadDashTrace.Write(
            "Startup",
            $"Failed to check pending restart request for startup blocked-dialog policy: {ex.Message}");
    }
}
