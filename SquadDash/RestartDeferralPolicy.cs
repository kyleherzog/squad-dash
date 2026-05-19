namespace SquadDash;

internal enum RestartDeferralReason {
    None,
    PromptRunning,
    LoopRunning,
    BackgroundWork,
    DirectQuickReplyHandoff,
    VoiceInput,
    DocRevision,
    ClipboardEditor
}

internal static class RestartDeferralPolicy {
    internal static RestartDeferralReason GetDeferralReason(
        bool isPromptRunning,
        bool isLoopRunning,
        bool hasBackgroundWork,
        bool hasPendingDirectQuickReplyHandoff,
        bool isVoiceInputActiveOrDraining,
        bool hasDocRevisionInFlight,
        bool isClipboardEditorOpen,
        // When true the bridge has been silent for ≥5 min and is assumed dead.
        // We lift the restart gate so a pending build-restart can proceed without
        // requiring the user to manually cancel the unresponsive prompt first.
        bool promptAppearsStalled = false) {
        if (isPromptRunning && !promptAppearsStalled)
            return RestartDeferralReason.PromptRunning;
        if (isLoopRunning)
            return RestartDeferralReason.LoopRunning;
        if (hasBackgroundWork)
            return RestartDeferralReason.BackgroundWork;
        if (hasPendingDirectQuickReplyHandoff)
            return RestartDeferralReason.DirectQuickReplyHandoff;
        if (isVoiceInputActiveOrDraining)
            return RestartDeferralReason.VoiceInput;
        if (hasDocRevisionInFlight)
            return RestartDeferralReason.DocRevision;
        if (isClipboardEditorOpen)
            return RestartDeferralReason.ClipboardEditor;

        return RestartDeferralReason.None;
    }

    internal static string BuildStatusMessage(RestartDeferralReason reason) =>
        reason switch {
            RestartDeferralReason.PromptRunning =>
                "Build finished. Restart will happen after the current Squad turn completes.",
            RestartDeferralReason.LoopRunning =>
                "Build finished. Restart will happen after the active loop stops.",
            RestartDeferralReason.BackgroundWork =>
                "Build finished. Restart will happen after background agents finish.",
            RestartDeferralReason.DirectQuickReplyHandoff =>
                "Build finished. Restart will happen after the agent handoff completes.",
            RestartDeferralReason.VoiceInput =>
                "Build finished. Restart will happen after voice recording completes.",
            RestartDeferralReason.DocRevision =>
                "Build finished. Restart will happen after in-flight AI revisions complete.",
            RestartDeferralReason.ClipboardEditor =>
                "Build finished. Restart will happen after the image editor closes.",
            _ =>
                "Build finished. Restart pending."
        };
}
