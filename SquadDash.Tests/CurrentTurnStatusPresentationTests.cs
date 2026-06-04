namespace SquadDash.Tests;

[TestFixture]
internal sealed class CurrentTurnStatusPresentationTests {
    [Test]
    public void BuildReportLine_ReturnsNull_WhenNoPromptIsRunning() {
        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: false,
                NoActivityWarningShown: false,
                StallWarningShown: false));

        Assert.That(line, Is.Null);
    }

    [Test]
    public void BuildReportLine_ReturnsWorkingLine_WhenPromptIsActive() {
        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: false,
                StallWarningShown: false));

        Assert.That(line, Is.EqualTo("Coordinator [Working] - Still responding to the current prompt."));
    }

    [Test]
    public void BuildReportLine_IncludesElapsedTime_WhenStartedAtIsKnown() {
        var startedAt = new DateTimeOffset(2026, 4, 16, 13, 0, 0, TimeSpan.FromHours(-4));
        var now = startedAt.AddMinutes(5).AddSeconds(1);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: false,
                StallWarningShown: false,
                StartedAt: startedAt),
            now);

        Assert.That(line, Is.EqualTo("Coordinator [Working (5m 01s)] - Still responding to the current prompt."));
    }

    [Test]
    public void BuildReportLine_ReturnsWaitingLine_WhenPromptHasGoneQuiet() {
        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: false));

        Assert.That(line, Is.EqualTo("Coordinator [Waiting] - No new bridge activity has arrived."));
    }

    [Test]
    public void BuildReportLine_ReturnsStallLine_WhenPromptLooksStalled() {
        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: true));

        Assert.That(line, Is.EqualTo("Coordinator [Stalled] - Bridge appears stalled; no new activity has arrived."));
    }

    [Test]
    public void BuildReportLine_IncludesStreamingDiagnostics_WhenResponseHasStarted() {
        var startedAt = new DateTimeOffset(2026, 4, 16, 13, 0, 0, TimeSpan.FromHours(-4));
        var firstResponseAt = startedAt.AddSeconds(4);
        var lastResponseAt = startedAt.AddSeconds(16);
        var now = startedAt.AddSeconds(20);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: true,
                StartedAt: startedAt,
                FirstResponseAt: firstResponseAt,
                LastResponseAt: lastResponseAt,
                ResponseDeltaCount: 12,
                ResponseCharacterCount: 840,
                LongestResponseGap: TimeSpan.FromSeconds(6)),
            now);

        Assert.That(
            line,
            Is.EqualTo("Coordinator [Stalled (20s)] - Bridge appears stalled; no new activity has arrived. First text in 4s; 12 chunks / 840 chars so far. Longest chunk gap 6s. Throughput 70.0 chars/s (~17.5 tok/s est.)."));
    }

    [Test]
    public void BuildReportLine_IncludesSessionReadyDiagnostics_WhenNoTextHasArrivedYet() {
        var startedAt = new DateTimeOffset(2026, 4, 16, 13, 0, 0, TimeSpan.FromHours(-4));
        var sessionReadyAt = startedAt.AddSeconds(2);
        var firstToolAt = startedAt.AddSeconds(5);
        var now = startedAt.AddSeconds(12);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: false,
                StartedAt: startedAt,
                SessionReadyAt: sessionReadyAt,
                FirstToolAt: firstToolAt),
            now);

        Assert.That(
            line,
            Is.EqualTo("Coordinator [Waiting (12s)] - No new bridge activity has arrived. Session ready in 2s, but no response text has arrived yet. First tool activity started 5s after launch."));
    }

    [Test]
    public void BuildReportLine_ClassifiesSlowUpstreamTokenArrival_WhenChunksAreTinyAndSlow() {
        var startedAt = new DateTimeOffset(2026, 4, 21, 11, 0, 0, TimeSpan.FromHours(-4));
        var firstResponseAt = startedAt.AddSeconds(4);
        var lastResponseAt = startedAt.AddSeconds(16);
        var now = startedAt.AddSeconds(20);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: false,
                StallWarningShown: false,
                StartedAt: startedAt,
                FirstResponseAt: firstResponseAt,
                LastResponseAt: lastResponseAt,
                ResponseDeltaCount: 12,
                ResponseCharacterCount: 84,
                LongestResponseGap: TimeSpan.FromSeconds(3),
                AverageResponseGap: TimeSpan.FromSeconds(1),
                ThinkingDeltaCount: 4,
                ToolStartCount: 0,
                ToolCompleteCount: 0),
            now);

        Assert.That(
            line,
            Is.EqualTo("Coordinator [Working (20s)] - Upstream response text is arriving slowly. First text in 4s; 12 chunks / 84 chars so far. Average chunk gap 1s. Average chunk size 7 chars. Longest chunk gap 3s. Throughput 7.0 chars/s (~1.8 tok/s est.)."));
    }

    [Test]
    public void BuildReportLine_ReportsThinkingToolLoop_WhenNoTextHasArrivedYet() {
        var startedAt = new DateTimeOffset(2026, 4, 21, 11, 0, 0, TimeSpan.FromHours(-4));
        var sessionReadyAt = startedAt.AddSeconds(2);
        var firstToolAt = startedAt.AddSeconds(6);
        var now = startedAt.AddMinutes(3);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: false,
                StartedAt: startedAt,
                SessionReadyAt: sessionReadyAt,
                FirstToolAt: firstToolAt,
                ThinkingDeltaCount: 42,
                ToolStartCount: 6,
                ToolCompleteCount: 5),
            now);

        Assert.That(
            line,
            Is.EqualTo("Coordinator [Waiting (3m 00s)] - No new bridge activity has arrived. Session ready in 2s, but no response text has arrived yet. First tool activity started 6s after launch. Model is still in a thinking/tool loop: 42 thinking updates, 5 completed tools so far."));
    }

    [Test]
    public void BuildReportLine_IncludesThinkingThroughput_WhenNoResponseTextHasArrivedYet() {
        var startedAt = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.FromHours(-4));
        var sessionReadyAt = startedAt.AddSeconds(2);
        var firstToolAt = startedAt.AddSeconds(4);
        var firstThinkingTextAt = startedAt.AddSeconds(8);
        var lastThinkingTextAt = startedAt.AddSeconds(18);
        var now = startedAt.AddSeconds(30);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: false,
                StartedAt: startedAt,
                SessionReadyAt: sessionReadyAt,
                FirstToolAt: firstToolAt,
                FirstThinkingTextAt: firstThinkingTextAt,
                LastThinkingTextAt: lastThinkingTextAt,
                ThinkingDeltaCount: 20,
                ThinkingTextDeltaCount: 10,
                ThinkingCharacterCount: 120,
                LongestThinkingGap: TimeSpan.FromSeconds(4),
                AverageThinkingGap: TimeSpan.FromSeconds(1)),
            now);

        Assert.That(
            line,
            Is.EqualTo("Coordinator [Waiting (30s)] - No new bridge activity has arrived. Session ready in 2s, but no response text has arrived yet. First tool activity started 4s after launch. Model is still thinking internally: 20 thinking updates so far. Thought stream 120 chars / 10 chunks. Thinking throughput 12.0 chars/s (~3.0 tok/s est.). Average thought gap 1s. Longest thought gap 4s."));
    }

    [Test]
    public void BuildReportLine_IncludesThinkingThroughput_WhenThinkingContinuesAfterResponseText() {
        var startedAt = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.FromHours(-4));
        var firstResponseAt = startedAt.AddSeconds(4);
        var lastResponseAt = startedAt.AddSeconds(12);
        var firstThinkingTextAt = startedAt.AddSeconds(14);
        var lastThinkingTextAt = startedAt.AddSeconds(24);
        var now = startedAt.AddSeconds(30);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: false,
                StartedAt: startedAt,
                FirstResponseAt: firstResponseAt,
                LastResponseAt: lastResponseAt,
                ResponseDeltaCount: 8,
                ResponseCharacterCount: 160,
                FirstThinkingTextAt: firstThinkingTextAt,
                LastThinkingTextAt: lastThinkingTextAt,
                ThinkingDeltaCount: 18,
                ThinkingTextDeltaCount: 12,
                ThinkingCharacterCount: 96,
                LongestThinkingGap: TimeSpan.FromSeconds(2),
                AverageThinkingGap: TimeSpan.FromMilliseconds(900)),
            now);

        Assert.That(
            line,
            Is.EqualTo("Coordinator [Waiting (30s)] - No new bridge activity has arrived. First text in 4s; 8 chunks / 160 chars so far. Throughput 20.0 chars/s (~5.0 tok/s est.). Thought stream 96 chars / 12 chunks. Thinking throughput 9.6 chars/s (~2.4 tok/s est.). Average thought gap 900ms."));
    }

    [Test]
    public void BuildReportLine_IncludesBridgeSilenceDuration_WhenLastActivityIsKnown() {
        var startedAt = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.FromHours(-4));
        var lastActivityAt = startedAt.AddSeconds(10);
        var now = startedAt.AddMinutes(2).AddSeconds(15);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: true,
                StartedAt: startedAt,
                LastActivityAt: lastActivityAt,
                SessionReadyAt: startedAt.AddSeconds(2),
                FirstThinkingTextAt: startedAt.AddSeconds(5),
                LastThinkingTextAt: lastActivityAt,
                ThinkingDeltaCount: 50,
                ThinkingTextDeltaCount: 50,
                ThinkingCharacterCount: 311),
            now);

        Assert.That(
            line,
            Is.EqualTo("Coordinator [Stalled (2m 15s)] - Bridge appears stalled; no activity for 2m 05s. Session ready in 2s, but no response text has arrived yet. Model is still thinking internally: 50 thinking updates so far. Thought stream 311 chars / 50 chunks. Thinking throughput 62.2 chars/s (~15.6 tok/s est.)."));
    }

    [Test]
    public void BuildReportLine_UsesDeadStatus_WhenPromptAppearsDead() {
        var startedAt = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.FromHours(-4));
        var lastActivityAt = startedAt.AddSeconds(13);
        var now = startedAt.AddMinutes(5).AddSeconds(15);

        var line = CurrentTurnStatusPresentation.BuildReportLine(
            new CurrentTurnStatusSnapshot(
                IsRunning: true,
                NoActivityWarningShown: true,
                StallWarningShown: true,
                DeadWarningShown: true,
                StartedAt: startedAt,
                LastActivityAt: lastActivityAt),
            now);

        Assert.That(
            line,
            Is.EqualTo("Coordinator [Stalled (5m 15s)] - Bridge appears dead; no activity for 5m 02s."));
    }
}
