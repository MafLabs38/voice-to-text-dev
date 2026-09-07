using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceToText.Services;

public sealed class AudioRecorderService : IDisposable
{
    private WasapiCapture? _capture;
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
        _capture = new WasapiCapture(device);
        _writer = new WaveFileWriter(_filePath, _capture.WaveFormat);

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;

        IsRecording = true;
        _capture.StartRecording();
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

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _writer?.Dispose();
        _writer = null;
        _capture?.Dispose();
        _capture = null;
        IsRecording = false;

        if (e.Exception is not null)
        {
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
        _capture?.Dispose();
        _writer?.Dispose();
    }
}
