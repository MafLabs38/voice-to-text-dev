using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using VoiceToText.Models;

namespace VoiceToText.Services;

public sealed class DictationCoordinator : INotifyPropertyChanged, IDisposable
{
    private readonly AudioRecorderService _audioRecorder;
    private readonly ITranscriptionClient _transcriptionClient;
    private readonly ApiKeyStore _apiKeyStore;
    private readonly TextPaster _textPaster;
    private readonly Func<AppSettings> _getSettings;
    private readonly DispatcherTimer _maxDurationTimer;

    private DictationState _state = DictationState.Ready;
    private string _lastTranscriptionText = "";
    private string? _lastErrorMessage;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DictationCoordinator(
        AudioRecorderService audioRecorder,
        ITranscriptionClient transcriptionClient,
        ApiKeyStore apiKeyStore,
        TextPaster textPaster,
        Func<AppSettings> getSettings)
    {
        _audioRecorder = audioRecorder;
        _transcriptionClient = transcriptionClient;
        _apiKeyStore = apiKeyStore;
        _textPaster = textPaster;
        _getSettings = getSettings;

        _maxDurationTimer = new DispatcherTimer();
        _maxDurationTimer.Tick += (_, _) => _ = StopAndProcessAsync();
    }

    public DictationState State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            OnPropertyChanged();
        }
    }

    public string LastTranscriptionText
    {
        get => _lastTranscriptionText;
        private set
        {
            if (_lastTranscriptionText == value)
            {
                return;
            }

            _lastTranscriptionText = value;
            OnPropertyChanged();
        }
    }

    public string? LastErrorMessage
    {
        get => _lastErrorMessage;
        private set
        {
            if (_lastErrorMessage == value)
            {
                return;
            }

            _lastErrorMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Appelé sur pression du raccourci global : démarre, arrête, ou acquitte une erreur affichée.</summary>
    public void ToggleRecording()
    {
        switch (State)
        {
            case DictationState.Ready:
                StartRecording();
                break;
            case DictationState.Recording:
                _ = StopAndProcessAsync();
                break;
            case DictationState.Error:
                State = DictationState.Ready;
                break;
            default:
                // Transcription ou Collage en cours : une nouvelle pression est ignorée.
                break;
        }
    }

    private void StartRecording()
    {
        var settings = _getSettings();
        try
        {
            _textPaster.RememberForegroundWindow();
            _audioRecorder.Start(settings.MicrophoneDeviceId);
            State = DictationState.Recording;

            _maxDurationTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, settings.MaxRecordingSeconds));
            _maxDurationTimer.Start();
        }
        catch (Exception ex)
        {
            LastErrorMessage = $"Microphone indisponible : {ex.Message}";
            State = DictationState.Error;
        }
    }

    private async Task StopAndProcessAsync()
    {
        _maxDurationTimer.Stop();

        if (State != DictationState.Recording)
        {
            return;
        }

        var settings = _getSettings();

        string filePath;
        try
        {
            filePath = await _audioRecorder.StopAsync();
        }
        catch (Exception ex)
        {
            LastErrorMessage = $"Échec de l'enregistrement : {ex.Message}";
            State = DictationState.Error;
            return;
        }

        var apiKey = _apiKeyStore.LoadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LastErrorMessage = "Aucune clé API OpenAI configurée — ouvre les Paramètres.";
            State = DictationState.Error;
            TryDeleteTempFile(filePath);
            return;
        }

        State = DictationState.Transcribing;
        try
        {
            var result = await _transcriptionClient.TranscribeAsync(
                filePath, apiKey, settings.TranscriptionModel, ResolveLanguageHint(settings.Language), CancellationToken.None);

            LastTranscriptionText = result.Text;

            State = DictationState.Pasting;
            _textPaster.PasteToRememberedWindow(result.Text);

            State = DictationState.Ready;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            State = DictationState.Error;
        }
        finally
        {
            TryDeleteTempFile(filePath);
        }
    }

    private static string? ResolveLanguageHint(string language) =>
        string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase) ? null : language;

    private static void TryDeleteTempFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
            // Suppression best-effort : un fichier verrouillé sera nettoyé au prochain démarrage.
        }
    }

    public void Dispose()
    {
        _maxDurationTimer.Stop();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
