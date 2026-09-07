using System.Net;

namespace VoiceToText.Services;

public sealed class TranscriptionException : Exception
{
    public bool IsRetryable { get; }
    public HttpStatusCode? StatusCode { get; }

    public TranscriptionException(string message, bool isRetryable = false, HttpStatusCode? statusCode = null)
        : base(message)
    {
        IsRetryable = isRetryable;
        StatusCode = statusCode;
    }
}
