using System.ComponentModel;
using VoiceToText.Models;
using VoiceToText.Services;

namespace VoiceToText.ViewModels;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private readonly DictationCoordinator _coordinator;
    private bool _showRecModuleSetting = true;
    private bool _showLastTranscriptionModuleSetting = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public OverlayViewModel(DictationCoordinator coordinator)
    {
        _coordinator = coordinator;
        _coordinator.PropertyChanged += OnCoordinatorPropertyChanged;
    }

    public bool IsRecording => _coordinator.State == DictationState.Recording;

    public string StatusText => _coordinator.State switch
    {
        DictationState.Recording => "REC",
        DictationState.Transcribing => "Transcription en cours…",
        DictationState.Pasting => "Collage…",
        DictationState.Error => _coordinator.LastErrorMessage ?? "Erreur",
        _ => "Prêt",
    };

    public bool HasError => _coordinator.State == DictationState.Error;

    // Toujours visible (quand le réglage l'autorise) : sans ça, un utilisateur qui n'a encore
    // rien dicté ne voit strictement rien dans l'overlay et ne sait même pas que l'appli tourne.
    public bool ShowRecModule => _showRecModuleSetting;

    public string LastTranscriptionText => _coordinator.LastTranscriptionText;

    public bool ShowLastTranscriptionModule =>
        _showLastTranscriptionModuleSetting && !string.IsNullOrEmpty(_coordinator.LastTranscriptionText);

    public void UpdateModuleVisibilitySettings(bool showRecModule, bool showLastTranscriptionModule)
    {
        _showRecModuleSetting = showRecModule;
        _showLastTranscriptionModuleSetting = showLastTranscriptionModule;
        Raise(nameof(ShowRecModule));
        Raise(nameof(ShowLastTranscriptionModule));
    }

    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DictationCoordinator.State):
                Raise(nameof(IsRecording));
                Raise(nameof(HasError));
                Raise(nameof(StatusText));
                Raise(nameof(ShowRecModule));
                break;
            case nameof(DictationCoordinator.LastTranscriptionText):
                Raise(nameof(LastTranscriptionText));
                Raise(nameof(ShowLastTranscriptionModule));
                break;
            case nameof(DictationCoordinator.LastErrorMessage):
                Raise(nameof(StatusText));
                break;
        }
    }

    private void Raise(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
