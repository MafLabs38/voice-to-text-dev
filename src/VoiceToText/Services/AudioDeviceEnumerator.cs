using NAudio.CoreAudioApi;

namespace VoiceToText.Services;

public sealed record AudioDeviceInfo(string Id, string Name);

public static class AudioDeviceEnumerator
{
    public static IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(device => new AudioDeviceInfo(device.ID, device.FriendlyName))
            .ToList();
    }
}
