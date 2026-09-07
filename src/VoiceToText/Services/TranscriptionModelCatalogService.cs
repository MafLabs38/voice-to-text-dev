using System.IO;
using System.Text.Json;
using VoiceToText.Models;

namespace VoiceToText.Services;

public sealed class TranscriptionModelCatalogService
{
    private sealed class CatalogFile
    {
        public string DefaultModelId { get; set; } = "gpt-transcribe";
        public List<TranscriptionModelOption> Models { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _filePath;

    public TranscriptionModelCatalogService() : this(AppPaths.ModelsFilePath)
    {
    }

    public TranscriptionModelCatalogService(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<TranscriptionModelOption> GetAvailableModels() => LoadOrSeed().Models;

    public string GetDefaultModelId() => LoadOrSeed().DefaultModelId;

    private CatalogFile LoadOrSeed()
    {
        if (!File.Exists(_filePath))
        {
            var seeded = CreateDefaultCatalog();
            Save(seeded);
            return seeded;
        }

        var json = File.ReadAllText(_filePath);
        var catalog = JsonSerializer.Deserialize<CatalogFile>(json, JsonOptions);
        return catalog is { Models.Count: > 0 } ? catalog : CreateDefaultCatalog();
    }

    private void Save(CatalogFile catalog)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    private static CatalogFile CreateDefaultCatalog() => new()
    {
        DefaultModelId = "gpt-transcribe",
        Models = new List<TranscriptionModelOption>
        {
            new("gpt-transcribe", "gpt-transcribe (recommandé — le plus précis, moins cher que whisper-1)"),
            new("gpt-4o-mini-transcribe", "gpt-4o-mini-transcribe (le plus rapide, le moins cher)"),
            new("gpt-4o-transcribe", "gpt-4o-transcribe"),
            new("whisper-1", "whisper-1 (historique)"),
        },
    };
}
