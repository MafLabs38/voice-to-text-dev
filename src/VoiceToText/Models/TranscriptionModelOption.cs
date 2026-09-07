namespace VoiceToText.Models;

/// <summary>Une entrée du catalogue éditable (models.json) : l'id envoyé à l'API, et un libellé lisible.</summary>
public sealed record TranscriptionModelOption(string Id, string Label);
