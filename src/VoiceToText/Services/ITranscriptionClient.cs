using VoiceToText.Models;

namespace VoiceToText.Services;

public interface ITranscriptionClient
{
    Task<TranscriptionResult> TranscribeAsync(
        string filePath,
        string apiKey,
        string model,
        string? languageHint,
        CancellationToken cancellationToken);
}
