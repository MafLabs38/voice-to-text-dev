using System.Windows;
using Velopack;
using Velopack.Sources;
using VoiceToText.Models;
using VoiceToText.Services;
using VoiceToText.ViewModels;
using VoiceToText.Views;

namespace VoiceToText;

public partial class App : System.Windows.Application
{
    private const string UpdateRepoUrl = "https://github.com/MafLabs38/voice-to-text-dev";

    // WPF génère automatiquement un Main(), mais Velopack doit s'exécuter avant tout le reste
    // (avant même la construction de l'objet App) pour intercepter correctement les hooks
    // d'installation/désinstallation/mise à jour. Nécessite <StartupObject> dans le .csproj.
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        // Une instance tourne déjà : l'action éventuellement demandée (ex. depuis la Jump List
        // de la barre des tâches) lui a été transmise par SingleInstance.TryAcquire — ce
        // processus-ci n'a rien de plus à faire (jamais de 2e tray/overlay en double).
        if (!SingleInstance.TryAcquire(args))
        {
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

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
    private TaskbarIndicatorWindow _taskbarIndicatorWindow = null!;
    private TaskbarBadgeService _taskbarBadgeService = null!;
    private UpdateManager? _updateManager;
    private UpdateInfo? _pendingUpdate;
    private readonly System.Windows.Threading.DispatcherTimer _updateCheckTimer = new()
    {
        Interval = TimeSpan.FromHours(4),
    };

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

        _overlayWindow = new OverlayWindow(_overlayViewModel, _settings.OverlayLeft, _settings.OverlayTop, _settings.OverlayScreenDeviceName, OnOverlayPositionChanged)
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
        _trayIconService.UpdateRequested += (_, _) => TryApplyPendingUpdate();

        // Fenêtre dédiée uniquement à obtenir un bouton dans la barre des tâches (visible sur
        // tous les écrans si l'option Windows correspondante est activée), sur lequel on superpose
        // une pastille pendant l'enregistrement — l'overlay flottant, lui, reste hors barre des
        // tâches et n'est visible que sur l'écran où il est positionné.
        _taskbarIndicatorWindow = new TaskbarIndicatorWindow(ToggleOverlay);
        var taskbarHwnd = new System.Windows.Interop.WindowInteropHelper(_taskbarIndicatorWindow).EnsureHandle();
        _taskbarBadgeService = new TaskbarBadgeService(taskbarHwnd);
        ApplyTaskbarIndicatorVisibility();

        _coordinator.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DictationCoordinator.State))
            {
                var active = _coordinator.State != DictationState.Ready;
                _trayIconService.SetActive(active);
                _taskbarBadgeService.SetState(_coordinator.State);
                if (_coordinator.State == DictationState.Ready)
                {
                    PrewarmMicrophone();
                    TryApplyPendingUpdate();
                }
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
        PrewarmMicrophone();
        CheckForUpdates();
        _updateCheckTimer.Tick += (_, _) => CheckForUpdates();
        _updateCheckTimer.Start();

        SingleInstance.ActionReceived += action => Dispatcher.BeginInvoke(new Action(() => HandleIpcAction(action)));
        SingleInstance.StartListening();
        SetupTaskbarJumpList();

        // Revérifie/réaffiche une fois le reste du démarrage terminé (priorité la plus basse du
        // dispatcher) : l'overlay se plaçait par défaut sur l'écran "primaire" Windows quand
        // aucune position mémorisée valide n'existait — presque toujours l'écran du portable sur
        // une config dockée, jamais regardé par l'utilisateur (cf. OverlayWindow.FallbackWorkArea).
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _overlayWindow.RevalidatePosition();
            _overlayWindow.Show();
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Exécute une action demandée par une seconde instance (ex. clic sur un item de la Jump
    /// List de la barre des tâches) — mêmes actions que le menu de la zone de notification.
    /// </summary>
    private void HandleIpcAction(string action)
    {
        switch (action)
        {
            case "ToggleOverlay":
                ToggleOverlay();
                break;
            case "OpenSettings":
                OpenSettings();
                break;
            case "ShowLastTranscription":
                _overlayWindow.Show();
                break;
            case "Exit":
                Shutdown();
                break;
        }
    }

    /// <summary>
    /// Enregistre la Jump List (clic droit sur le bouton de barre des tâches) avec les mêmes
    /// actions que le menu de la zone de notification. Chaque entrée relance l'exe avec un
    /// argument — Windows ne permet pas d'appeler directement du code du processus déjà en
    /// cours — d'où SingleInstance qui route cet argument vers <see cref="HandleIpcAction"/>.
    /// </summary>
    private void SetupTaskbarJumpList()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        var jumpList = new System.Windows.Shell.JumpList();
        jumpList.JumpItems.Add(CreateJumpTask("Afficher/masquer l'overlay", "ToggleOverlay", exePath));
        jumpList.JumpItems.Add(CreateJumpTask("Paramètres…", "OpenSettings", exePath));
        jumpList.JumpItems.Add(CreateJumpTask("Dernière transcription", "ShowLastTranscription", exePath));
        jumpList.JumpItems.Add(CreateJumpTask("Quitter", "Exit", exePath));
        System.Windows.Shell.JumpList.SetJumpList(this, jumpList);
        jumpList.Apply();
    }

    private static System.Windows.Shell.JumpTask CreateJumpTask(string title, string action, string exePath) => new()
    {
        Title = title,
        ApplicationPath = exePath,
        Arguments = action,
        IconResourcePath = exePath,
        IconResourceIndex = 0,
    };

    private void ApplyTaskbarIndicatorVisibility()
    {
        if (_settings.ShowTaskbarIndicator)
        {
            _taskbarIndicatorWindow.Show();
        }
        else
        {
            _taskbarIndicatorWindow.Hide();
        }
    }

    /// <summary>
    /// Vérifie en tâche de fond s'il existe une nouvelle version sur les releases GitHub du
    /// dépôt et la télécharge. Rappelée au démarrage puis périodiquement (<see cref="_updateCheckTimer"/>)
    /// tant que l'appli tourne, pour ne pas dépendre d'un redémarrage pour détecter une release.
    /// Une fois téléchargée, l'appli l'applique immédiatement si aucune dictée n'est en cours
    /// (<see cref="TryApplyPendingUpdate"/>) ; sinon elle reste en attente, visible dans le menu
    /// de la zone de notification et dans Paramètres, jusqu'au prochain retour à l'état prêt ou
    /// à un clic explicite. Silencieux et best-effort : pas de connexion, dépôt inaccessible, ou
    /// appli lancée hors installation Velopack (ex. depuis les sources en debug) → sans effet.
    /// </summary>
    private void CheckForUpdates()
    {
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            try
            {
                _updateManager ??= new UpdateManager(new GithubSource(UpdateRepoUrl, null, false));
                if (!_updateManager.IsInstalled)
                {
                    return;
                }

                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                if (updateInfo is null)
                {
                    return;
                }

                await _updateManager.DownloadUpdatesAsync(updateInfo);
                _pendingUpdate = updateInfo;
                _trayIconService.SetUpdateAvailable(updateInfo.TargetFullRelease.Version.ToString());

                TryApplyPendingUpdate();
            }
            catch
            {
            }
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Applique la mise à jour téléchargée et relance l'appli — soit automatiquement dès que
    /// l'état repasse à Prêt, soit sur clic explicite (menu de la zone de notification ou bouton
    /// dans Paramètres), jamais pendant une dictée en cours.
    /// </summary>
    private void TryApplyPendingUpdate()
    {
        if (_updateManager is null || _pendingUpdate is null)
        {
            return;
        }

        if (_coordinator.State != DictationState.Ready)
        {
            return;
        }

        _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
    }

    /// <summary>
    /// Initialise le microphone en avance, en tâche de fond, pour que la première pression du
    /// déclencheur (au démarrage, ou après un changement de périphérique) soit déjà instantanée
    /// plutôt que de payer le coût d'initialisation WASAPI au moment où l'utilisateur appuie.
    /// </summary>
    private void PrewarmMicrophone()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var deviceId = PreferredDeviceResolver.Resolve(_settings.MicrophoneDeviceId, _settings.SecondaryMicrophoneDeviceId);
            _audioRecorder.Prewarm(deviceId);
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
        var pendingUpdateVersion = _pendingUpdate?.TargetFullRelease.Version.ToString();
        var settingsWindow = new SettingsWindow(_settings, _settingsStore, _apiKeyStore, _modelCatalogService, pendingUpdateVersion, TryApplyPendingUpdate);

        // Quand la fenêtre est ouverte via la Jump List (clic droit sur la barre des tâches), la
        // demande arrive par IPC depuis un processus tiers déjà terminé au moment où on l'affiche
        // — Windows refuse alors silencieusement de donner le focus au premier plan (verrou
        // anti-vol de focus). Activate()/Show() seuls ne suffisent pas dans ce cas ; l'astuce
        // Topmost on/off force la fenêtre au-dessus malgré le verrou.
        settingsWindow.Loaded += (_, _) =>
        {
            settingsWindow.Activate();
            settingsWindow.Topmost = true;
            settingsWindow.Topmost = false;
            settingsWindow.Focus();
        };

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
            PrewarmMicrophone();
            ApplyTaskbarIndicatorVisibility();
        }
    }

    private void OnOverlayPositionChanged(double left, double top, string? screenDeviceName)
    {
        _settings.OverlayLeft = left;
        _settings.OverlayTop = top;
        _settings.OverlayScreenDeviceName = screenDeviceName;
        _settingsStore.Save(_settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updateCheckTimer.Stop();
        _hotkeyService.Dispose();
        _mouseTriggerService.Dispose();
        _overlayToggleHotkeyService.Dispose();
        _trayIconService.Dispose();
        _taskbarBadgeService.Dispose();
        _taskbarIndicatorWindow.ForceClose();
        _audioRecorder.Dispose();
        _coordinator.Dispose();
        base.OnExit(e);
    }
}
