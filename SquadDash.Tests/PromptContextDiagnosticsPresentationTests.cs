namespace SquadDash.Tests;

[TestFixture]
internal sealed class PromptContextDiagnosticsPresentationTests {

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns a zeroed-out diagnostics record as a baseline.</summary>
    private static PromptContextDiagnostics Zero(
        string?         sessionId              = null,
        DateTimeOffset? sessionUpdatedAt       = null,
        DateTimeOffset? transcriptStartedAt    = null,
        int             coordinatorTurnCount   = 0,
        int             agentThreadCount       = 0,
        int             agentThreadTurnCount   = 0,
        int             promptHistoryCount     = 0,
        int             recentSessionCount     = 0,
        int             coordinatorPromptChars = 0,
        int             coordinatorResponseChars = 0,
        int             coordinatorThinkingChars = 0,
        int             agentPromptChars       = 0,
        int             agentResponseChars     = 0,
        int             agentThinkingChars     = 0)
        => new(
            SessionId:                sessionId,
            SessionUpdatedAt:         sessionUpdatedAt,
            TranscriptStartedAt:      transcriptStartedAt,
            CoordinatorTurnCount:     coordinatorTurnCount,
            AgentThreadCount:         agentThreadCount,
            AgentThreadTurnCount:     agentThreadTurnCount,
            PromptHistoryCount:       promptHistoryCount,
            RecentSessionCount:       recentSessionCount,
            CoordinatorPromptChars:   coordinatorPromptChars,
            CoordinatorResponseChars: coordinatorResponseChars,
            CoordinatorThinkingChars: coordinatorThinkingChars,
            AgentPromptChars:         agentPromptChars,
            AgentResponseChars:       agentResponseChars,
            AgentThinkingChars:       agentThinkingChars);

    private static readonly DateTimeOffset _now =
        new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    // ── TotalChars ────────────────────────────────────────────────────────────

    [Test]
    public void TotalChars_IsZero_WhenAllCharFieldsAreZero() {
        Assert.That(Zero().TotalChars, Is.EqualTo(0));
    }

    [Test]
    public void TotalChars_SumsAllSixCharFields() {
        var d = Zero(
            coordinatorPromptChars:   1_000,
            coordinatorResponseChars: 2_000,
            coordinatorThinkingChars: 3_000,
            agentPromptChars:         4_000,
            agentResponseChars:       5_000,
            agentThinkingChars:       6_000);

        Assert.That(d.TotalChars, Is.EqualTo(21_000));
    }

    [Test]
    public void TotalChars_OnlyCountsCharFields_NotTurnOrThreadCounts() {
        // Turn counts and thread counts must not bleed into TotalChars.
        var d = Zero(
            coordinatorTurnCount:  100,
            agentThreadCount:      50,
            agentThreadTurnCount:  200,
            coordinatorPromptChars: 1);

        Assert.That(d.TotalChars, Is.EqualTo(1));
    }

    // ── GetRiskBand — score thresholds ────────────────────────────────────────

    [Test]
    public void GetRiskBand_ReturnsLow_WhenScoreIsZero() {
        // All dimensions below any threshold → score 0 → "low"
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(Zero(), _now);
        Assert.That(band, Is.EqualTo("low"));
    }

    [Test]
    public void GetRiskBand_ReturnsLow_AtScoreOfOne_ViaSmallCharCount() {
        // 20 000–49 999 chars → score 1 → still "low"
        var d = Zero(coordinatorPromptChars: 20_000);
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("low"));
    }

    [Test]
    public void GetRiskBand_ReturnsMedium_AtScoreOfTwo_ViaModerateCharCount() {
        // 50 000–119 999 chars → score 2 → "medium"
        var d = Zero(coordinatorResponseChars: 50_000);
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("medium"));
    }

    [Test]
    public void GetRiskBand_ReturnsMedium_AtBoundary_ViaHighTurnCount() {
        // ≥30 total turns → score 2 → "medium"
        var d = Zero(coordinatorTurnCount: 15, agentThreadTurnCount: 15);
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("medium"));
    }

    [Test]
    public void GetRiskBand_ReturnsMedium_WhenTranscriptIsOlderThan30Minutes() {
        // 30-minute-old transcript → score 1; add 15 turns → score 2 → "medium"
        var d = Zero(
            transcriptStartedAt:  _now.AddMinutes(-31),
            coordinatorTurnCount: 8,
            agentThreadTurnCount: 7);       // 15 total turns → +1
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("medium"));
    }

    [Test]
    public void GetRiskBand_ReturnsHigh_WhenScoreReachesFive() {
        // 120 000+ chars (+3) + 30+ turns (+2) = score 5 → "high"
        var d = Zero(
            coordinatorPromptChars: 120_000,
            coordinatorTurnCount:   15,
            agentThreadTurnCount:   15);
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("high"));
    }

    [Test]
    public void GetRiskBand_ReturnsHigh_ForLargeOldTranscriptWithManyTurns() {
        // Reproduces the original acceptance scenario.
        var d = Zero(
            coordinatorPromptChars:   9_000,
            coordinatorResponseChars: 31_000,
            coordinatorThinkingChars: 18_000,
            agentPromptChars:         8_000,
            agentResponseChars:       42_000,
            agentThinkingChars:       16_000,    // 124 000 total → +3
            coordinatorTurnCount:     18,
            agentThreadTurnCount:     14,         // 32 total → +2
            promptHistoryCount:       22,         // ≥20 → +1
            transcriptStartedAt:      _now.AddHours(-2));  // ≥90 min → +2

        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("high"));
    }

    [Test]
    public void GetRiskBand_PromptHistoryCount_AddsOnePoint_AtTwentyOrMore() {
        // 20 history entries → +1; combined with 50k chars (+2) = score 3 → "medium"
        var d = Zero(agentResponseChars: 50_000, promptHistoryCount: 20);
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("medium"));
    }

    [Test]
    public void GetRiskBand_PromptHistoryCount_DoesNotScore_BelowTwenty() {
        // 19 entries contribute nothing. With 0 other score → "low".
        var d = Zero(promptHistoryCount: 19);
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("low"));
    }

    [Test]
    public void GetRiskBand_TranscriptStartedAt_Null_DoesNotScore() {
        // No transcript timestamp → no age-based score. Score stays 0 → "low".
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(Zero(), _now);
        Assert.That(band, Is.EqualTo("low"));
    }

    [Test]
    public void GetRiskBand_OldTranscript_ScoresTwo_WhenOlderThan90Minutes() {
        // 90+ minutes → +2; alone → score 2 → "medium"
        var d = Zero(transcriptStartedAt: _now.AddMinutes(-91));
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("medium"));
    }

    [Test]
    public void GetRiskBand_TotalTurns_IsCoordinatorPlusAgentThreadTurns() {
        // 14 coordinator + 15 agent = 29 total < 30 → +1 (not +2)
        // Score 1 → "low"
        var d = Zero(coordinatorTurnCount: 14, agentThreadTurnCount: 15);
        var band = PromptContextDiagnosticsPresentation.GetRiskBand(d, _now);
        Assert.That(band, Is.EqualTo("low"));
    }

    // ── BuildTraceSummary — field presence ────────────────────────────────────

    [Test]
    public void BuildTraceSummary_EmitsHighRiskBandForLargeOldTranscript() {
        var diagnostics = new PromptContextDiagnostics(
            SessionId: "session-9",
            SessionUpdatedAt: new DateTimeOffset(2026, 4, 22, 17, 0, 0, TimeSpan.Zero),
            TranscriptStartedAt: new DateTimeOffset(2026, 4, 22, 15, 0, 0, TimeSpan.Zero),
            CoordinatorTurnCount: 18,
            AgentThreadCount: 3,
            AgentThreadTurnCount: 14,
            PromptHistoryCount: 22,
            RecentSessionCount: 4,
            CoordinatorPromptChars: 9_000,
            CoordinatorResponseChars: 31_000,
            CoordinatorThinkingChars: 18_000,
            AgentPromptChars: 8_000,
            AgentResponseChars: 42_000,
            AgentThinkingChars: 16_000);

        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(
            diagnostics,
            new DateTimeOffset(2026, 4, 22, 17, 30, 0, TimeSpan.Zero));

        Assert.Multiple(() => {
            Assert.That(summary, Does.Contain("riskBand=high"));
            Assert.That(summary, Does.Contain("totalChars=124000"));
            Assert.That(summary, Does.Contain("transcriptAgeMs="));
        });
    }

    [Test]
    public void BuildTraceSummary_ContainsAllMandatoryFields() {
        var d = Zero(sessionId: "abc", coordinatorTurnCount: 2, agentThreadCount: 1,
                     agentThreadTurnCount: 3, promptHistoryCount: 5, recentSessionCount: 2);
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);

        Assert.Multiple(() => {
            Assert.That(summary, Does.Contain("riskBand="));
            Assert.That(summary, Does.Contain("sessionId=abc"));
            Assert.That(summary, Does.Contain("coordinatorTurns=2"));
            Assert.That(summary, Does.Contain("agentThreads=1"));
            Assert.That(summary, Does.Contain("agentTurns=3"));
            Assert.That(summary, Does.Contain("promptHistory=5"));
            Assert.That(summary, Does.Contain("recentSessions=2"));
            Assert.That(summary, Does.Contain("totalChars=0"));
            Assert.That(summary, Does.Contain("coordinatorPromptChars=0"));
            Assert.That(summary, Does.Contain("coordinatorResponseChars=0"));
            Assert.That(summary, Does.Contain("coordinatorThinkingChars=0"));
            Assert.That(summary, Does.Contain("agentPromptChars=0"));
            Assert.That(summary, Does.Contain("agentResponseChars=0"));
            Assert.That(summary, Does.Contain("agentThinkingChars=0"));
        });
    }

    [Test]
    public void BuildTraceSummary_ShowsNone_WhenSessionIdIsNull() {
        var d = Zero(sessionId: null);
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Contain("sessionId=(none)"));
    }

    [Test]
    public void BuildTraceSummary_ShowsActualSessionId_WhenProvided() {
        var d = Zero(sessionId: "sess-42");
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Contain("sessionId=sess-42"));
    }

    [Test]
    public void BuildTraceSummary_OmitsTranscriptAge_WhenTranscriptStartedAtIsNull() {
        var d = Zero(transcriptStartedAt: null);
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Not.Contain("transcriptAgeMs="));
    }

    [Test]
    public void BuildTraceSummary_IncludesTranscriptAge_WhenTranscriptStartedAtIsSet() {
        var d = Zero(transcriptStartedAt: _now.AddHours(-1));
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Contain("transcriptAgeMs=3600000"));
    }

    [Test]
    public void BuildTraceSummary_ClampsTranscriptAge_ToZeroWhenTimestampIsInTheFuture() {
        // A clock skew or stale record must never produce a negative age.
        var d = Zero(transcriptStartedAt: _now.AddMinutes(5));
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Contain("transcriptAgeMs=0"));
    }

    [Test]
    public void BuildTraceSummary_OmitsLastPersistedUpdate_WhenSessionUpdatedAtIsNull() {
        var d = Zero(sessionUpdatedAt: null);
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Not.Contain("lastPersistedUpdateMs="));
    }

    [Test]
    public void BuildTraceSummary_IncludesLastPersistedUpdate_WhenSessionUpdatedAtIsSet() {
        // 30 seconds ago → 30 000 ms
        var d = Zero(sessionUpdatedAt: _now.AddSeconds(-30));
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Contain("lastPersistedUpdateMs=30000"));
    }

    [Test]
    public void BuildTraceSummary_ClampsLastPersistedUpdate_ToZeroWhenTimestampIsInTheFuture() {
        var d = Zero(sessionUpdatedAt: _now.AddSeconds(10));
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Contain("lastPersistedUpdateMs=0"));
    }

    [Test]
    public void BuildTraceSummary_IncludesBothTimestampFields_WhenBothAreSet() {
        var d = Zero(
            transcriptStartedAt: _now.AddMinutes(-45),
            sessionUpdatedAt:    _now.AddMinutes(-5));
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.Multiple(() => {
            Assert.That(summary, Does.Contain("transcriptAgeMs="));
            Assert.That(summary, Does.Contain("lastPersistedUpdateMs="));
        });
    }

    [Test]
    public void BuildTraceSummary_TotalChars_ReflectsAllSixCharFields() {
        var d = Zero(
            coordinatorPromptChars:   100,
            coordinatorResponseChars: 200,
            coordinatorThinkingChars: 300,
            agentPromptChars:         400,
            agentResponseChars:       500,
            agentThinkingChars:       600);

        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(d, _now);
        Assert.That(summary, Does.Contain("totalChars=2100"));
    }

    [Test]
    public void BuildTraceSummary_FieldsAreSpaceSeparated() {
        // The summary is consumed by trace parsers expecting space-delimited key=value pairs.
        var summary = PromptContextDiagnosticsPresentation.BuildTraceSummary(Zero(), _now);
        var parts = summary.Split(' ');
        Assert.That(parts, Has.Length.GreaterThan(1));
        Assert.That(parts, Has.All.Matches<string>(p => p.Contains('=')));
    }
}
