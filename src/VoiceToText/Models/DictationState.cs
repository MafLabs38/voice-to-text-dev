namespace VoiceToText.Models;

public enum DictationState
{
    Ready,
    Recording,
    Transcribing,
    Pasting,
    Error,
}
