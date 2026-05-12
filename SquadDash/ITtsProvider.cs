namespace SquadDash;

internal interface ITtsProvider
{
    /// <summary>Speaks the phrase. Fire-and-forget; exceptions are swallowed.</summary>
    Task SpeakAsync(string phrase, CancellationToken ct = default);
}
