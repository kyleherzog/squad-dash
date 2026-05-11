using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SquadDash;

internal interface ISpeechRecognitionService : IDisposable
{
    event EventHandler<string>? PhraseRecognized;
    event EventHandler<double>? VolumeChanged;
    event EventHandler<string>? RecognitionError;

    Task StartAsync(string key, string regionOrEndpoint, IEnumerable<string>? phraseHints = null);
    Task StopAsync();
    void WriteAudioData(byte[] buffer, int count);
}
