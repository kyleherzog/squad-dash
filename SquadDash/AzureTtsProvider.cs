using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace SquadDash;

internal sealed class AzureTtsProvider : ITtsProvider
{
    private readonly string _subscriptionKey;
    private readonly string _region;
    private readonly string _voiceName;

    public AzureTtsProvider(string subscriptionKey, string region, string voiceName)
    {
        _subscriptionKey = subscriptionKey;
        _region          = region;
        _voiceName       = voiceName;
    }

    public async Task SpeakAsync(string phrase, CancellationToken ct = default)
    {
        try
        {
            var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
            speechConfig.SpeechSynthesisVoiceName = _voiceName;

            using var audioConfig  = AudioConfig.FromDefaultSpeakerOutput();
            using var synthesizer  = new SpeechSynthesizer(speechConfig, audioConfig);

            await synthesizer.SpeakTextAsync(phrase).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AzureTtsProvider: SpeakAsync error: {ex.Message}");
        }
    }
}
