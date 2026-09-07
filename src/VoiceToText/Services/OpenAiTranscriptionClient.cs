using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using VoiceToText.Models;

namespace VoiceToText.Services;

public sealed class OpenAiTranscriptionClient : ITranscriptionClient
{
    private const string TranscriptionsEndpoint = "https://api.openai.com/v1/audio/transcriptions";

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

    public async Task<TranscriptionResult> TranscribeAsync(
        string filePath,
        string apiKey,
        string model,
        string? languageHint,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(filePath);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));
        content.Add(new StringContent(model), "model");
        content.Add(new StringContent("json"), "response_format");
        if (!string.IsNullOrWhiteSpace(languageHint))
        {
            content.Add(new StringContent(languageHint), "language");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, TranscriptionsEndpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw BuildException(response.StatusCode, body);
        }

        using var document = JsonDocument.Parse(body);
        var text = document.RootElement.GetProperty("text").GetString() ?? "";
        return new TranscriptionResult(text);
    }

    private static TranscriptionException BuildException(HttpStatusCode statusCode, string body)
    {
        var isRetryable = statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Clé API invalide — vérifie-la dans les Paramètres.",
            HttpStatusCode.TooManyRequests => "Limite de débit atteinte chez OpenAI — réessaie dans un instant.",
            _ => $"Échec de la transcription ({(int)statusCode}) : {body}",
        };

        return new TranscriptionException(message, isRetryable, statusCode);
    }
}
