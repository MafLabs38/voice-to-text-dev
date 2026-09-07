using System.Windows;
using VoiceToText.Models;
using VoiceToText.Services;
using VoiceToText.ViewModels;
using VoiceToText.Views;

namespace VoiceToText;

public partial class App : System.Windows.Application
{
    private SettingsStore _settingsStore = null!;
    private ApiKeyStore _apiKeyStore = null!;
    private TranscriptionModelCatalogService _modelCatalogService = null!;
    private AppSettings _settings = null!;
    private DictationCoordinator _coordinator = null!;
    private OverlayViewModel _overlayViewModel = null!;
    private GlobalHotkeyService _hotkeyService = null!;
    private MouseTriggerService _mouseTriggerService = null!;
    private GlobalHotkeyService _overlayToggleHotkeyService = null!;
    private TrayIconService _trayIconService = null!;
    private OverlayWindow _overlayWindow = null!;
    private AudioRecorderService _audioRecorder = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsStore = new SettingsStore();
        _apiKeyStore = new ApiKeyStore();
        _modelCatalogService = new TranscriptionModelCatalogService();
        _settings = _settingsStore.Load();
        ThemeManager.Apply(_settings.Theme, _settings.PrimaryColorHex);

        _audioRecorder = new AudioRecorderService();
        var transcriptionClient = new OpenAiTranscriptionClient();
        var textPaster = new TextPaster();
        _coordinator = new DictationCoordinator(_audioRecorder, transcriptionClient, _apiKeyStore, textPaster, () => _settings);

        _overlayViewModel = new OverlayViewModel(_coordinator);
        _overlayViewModel.UpdateModuleVisibilitySettings(_settings.ShowRecIndicator, _settings.ShowLastTranscription);

        _overlayWindow = new OverlayWindow(_overlayViewModel, _settings.OverlayLeft, _settings.OverlayTop, OnOverlayPositionChanged)
        {
            Topmost = _settings.AlwaysOnTop,
            Opacity = _settings.OverlayOpacity,
        };
        _overlayWindow.Show();

        _trayIconService = new TrayIconService();
        _trayIconService.ToggleOverlayRequested += (_, _) => ToggleOverlay();
        _trayIconService.OpenSettingsRequested += (_, _) => OpenSettings();
        _trayIconService.ShowLastTranscriptionRequested += (_, _) => _overlayWindow.Show();
        _trayIconService.ExitRequested += (_, _) => Shutdown();
        _coordinator.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DictationCoordinator.State))
            {
                _trayIconService.SetActive(_coordinator.State != DictationState.Ready);
            }
        };

        _hotkeyService = new GlobalHotkeyService();
        _hotkeyService.HotkeyPressed += (_, _) => _coordinator.ToggleRecording();

        _mouseTriggerService = new MouseTriggerService();
        _mouseTriggerService.TriggerActivated += (_, _) => Dispatcher.Invoke(_coordinator.ToggleRecording);

        _overlayToggleHotkeyService = new GlobalHotkeyService();
        _overlayToggleHotkeyService.HotkeyPressed += (_, _) => ToggleOverlay();

        ApplyTrigger();
        ApplyOverlayToggleHotkey();
    }

    private void ApplyTrigger()
    {
        _hotkeyService.Unregister();
        _mouseTriggerService.Stop();

        try
        {
            if (_settings.TriggerKind == TriggerKind.Keyboard)
            {
                _hotkeyService.Register(_settings.HotkeyModifiers, _settings.HotkeyKey);
            }
            else
            {
                _mouseTriggerService.Start(_settings.MouseButton);
            }
        }
        catch (InvalidOperationException ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Dictée universelle", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyOverlayToggleHotkey()
    {
        _overlayToggleHotkeyService.Unregister();
        try
        {
            _overlayToggleHotkeyService.Register(_settings.OverlayToggleModifiers, _settings.OverlayToggleKey);
        }
        catch (InvalidOperationException ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Dictée universelle", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ToggleOverlay()
    {
        if (_overlayWindow.IsVisible)
        {
            _overlayWindow.Hide();
        }
        else
        {
            _overlayWindow.Show();
        }
    }

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_settings, _settingsStore, _apiKeyStore, _modelCatalogService);
        var result = settingsWindow.ShowDialog();

        if (result == true && settingsWindow.SettingsSaved)
        {
            _settings = settingsWindow.ResultSettings;
            _overlayViewModel.UpdateModuleVisibilitySettings(_settings.ShowRecIndicator, _settings.ShowLastTranscription);
            _overlayWindow.Topmost = _settings.AlwaysOnTop;
            _overlayWindow.Opacity = _settings.OverlayOpacity;
            ApplyTrigger();
            ApplyOverlayToggleHotkey();
            ThemeManager.Apply(_settings.Theme, _settings.PrimaryColorHex);
        }
    }

    private void OnOverlayPositionChanged(double left, double top)
    {
        _settings.OverlayLeft = left;
        _settings.OverlayTop = top;
        _settingsStore.Save(_settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService.Dispose();
        _mouseTriggerService.Dispose();
        _overlayToggleHotkeyService.Dispose();
        _trayIconService.Dispose();
        _audioRecorder.Dispose();
        _coordinator.Dispose();
        base.OnExit(e);
    }
}
