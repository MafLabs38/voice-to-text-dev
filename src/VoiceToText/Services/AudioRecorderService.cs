using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceToText.Services;

public sealed class AudioRecorderService : IDisposable
{
    private WasapiCapture? _capture;
    private string? _capturedDeviceId;
    private WaveFileWriter? _writer;
    private string _filePath = "";
    private TaskCompletionSource<string>? _stopCompletion;

    public bool IsRecording { get; private set; }

    public event EventHandler<string>? RecordingError;

    public void Start(string? deviceId)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Un enregistrement est déjà en cours.");
        }

        Directory.CreateDirectory(AppPaths.TempRecordingsDirectory);
        _filePath = Path.Combine(AppPaths.TempRecordingsDirectory, $"dictation-{DateTime.Now:yyyyMMdd-HHmmssfff}.wav");

        var device = ResolveDevice(deviceId);
        EnsureCapture(device);

        _writer = new WaveFileWriter(_filePath, _capture!.WaveFormat);
        IsRecording = true;

        try
        {
            _capture.StartRecording();
        }
        catch
        {
            // Le flux (device débranché, invalidé...) est mort : on le jette pour forcer une
            // réinitialisation propre à la prochaine tentative plutôt que de rester bloqué dessus.
            _writer?.Dispose();
            _writer = null;
            IsRecording = false;
            DisposeCapture();
            throw;
        }
    }

    /// <summary>
    /// Initialise en avance le flux WASAPI pour le périphérique donné, sans démarrer de
    /// capture — appelé au repos (démarrage de l'app, retour à l'état prêt) pour que le
    /// prochain <see cref="Start"/> n'ait plus qu'à faire un Start() sur un flux déjà prêt.
    /// Best-effort : une erreur ici (périphérique indisponible) sera simplement retentée par
    /// le prochain <see cref="Start"/> réel.
    /// </summary>
    public void Prewarm(string? deviceId)
    {
        if (IsRecording)
        {
            return;
        }

        try
        {
            var device = ResolveDevice(deviceId);
            EnsureCapture(device);
        }
        catch
        {
        }
    }

    public Task<string> StopAsync()
    {
        if (!IsRecording || _capture is null)
        {
            return Task.FromResult(_filePath);
        }

        _stopCompletion = new TaskCompletionSource<string>();
        _capture.StopRecording();
        return _stopCompletion.Task;
    }

    /// <summary>
    /// Réutilise le flux WASAPI déjà initialisé pour ce même périphérique plutôt que d'en recréer
    /// un à chaque enregistrement : sur certains pilotes (ex. effets audio USB), l'initialisation
    /// d'un flux coûte facilement plus d'une seconde, ce qui se traduisait par un temps de latence
    /// perceptible à chaque pression du raccourci. On ne repaie ce coût que lorsque le périphérique
    /// change réellement (favoris, changement dans les paramètres, etc.).
    /// </summary>
    private void EnsureCapture(MMDevice device)
    {
        if (_capture is not null && _capturedDeviceId == device.ID)
        {
            return;
        }

        DisposeCapture();

        _capture = new WasapiCapture(device);
        _capturedDeviceId = device.ID;
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
    }

    private void DisposeCapture()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _capture = null;
        _capturedDeviceId = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _writer?.Dispose();
        _writer = null;
        IsRecording = false;

        if (e.Exception is not null)
        {
            // Flux potentiellement invalidé (périphérique débranché pendant l'enregistrement...) :
            // on le jette pour repartir propre au prochain essai.
            DisposeCapture();
            RecordingError?.Invoke(this, e.Exception.Message);
        }

        _stopCompletion?.TrySetResult(_filePath);
    }

    private static MMDevice ResolveDevice(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (!string.IsNullOrEmpty(deviceId))
        {
            return enumerator.GetDevice(deviceId);
        }

        return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
    }

    public void Dispose()
    {
        DisposeCapture();
        _writer?.Dispose();
    }
}
