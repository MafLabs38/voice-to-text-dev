using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using VoiceToText.Models;
using VoiceToText.Services;

namespace VoiceToText.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _workingCopy;
    private readonly SettingsStore _settingsStore;
    private readonly ApiKeyStore _apiKeyStore;

    /// <summary>Valeur capturée d'un déclencheur d'enregistrement (touche ou bouton de souris).</summary>
    private sealed class TriggerSlot
    {
        public TriggerKind Kind;
        public ModifierKeys Modifiers;
        public Key Key;
        public MouseTriggerButton MouseButton;
    }

    private readonly TriggerSlot _primaryTrigger = new();
    private readonly TriggerSlot _secondaryTrigger = new();

    // Déclencheur en cours de capture (null = aucune capture).
    private TriggerSlot? _capturingSlot;

    private readonly TriggerSlot _overlayToggleHotkey = new();
    private readonly TriggerSlot _secondaryToggleHotkey = new();
    private (TriggerSlot Slot, System.Windows.Controls.Button Button, System.Windows.Controls.TextBlock Text)? _capturingHotkey;

    private AppTheme _selectedTheme;
    private string _selectedPrimaryColorHex = "#3B82F6";
    private readonly List<System.Windows.Controls.Border> _primaryColorSwatches = new();

    public bool SettingsSaved { get; private set; }

    public SettingsWindow(AppSettings currentSettings, SettingsStore settingsStore, ApiKeyStore apiKeyStore,
        TranscriptionModelCatalogService modelCatalogService, string? pendingUpdateVersion = null, Action? onInstallUpdate = null)
    {
        InitializeComponent();

        if (!string.IsNullOrEmpty(pendingUpdateVersion))
        {
            UpdateAvailablePanel.Visibility = Visibility.Visible;
            UpdateAvailableText.Text = $"Nouvelle version disponible : v{pendingUpdateVersion}";
            InstallUpdateButton.Click += (_, _) =>
            {
                onInstallUpdate?.Invoke();
                Close();
            };
        }

        // WindowStartupLocation=CenterScreen centre sur l'écran PRIMAIRE quel que soit l'écran
        // sur lequel on a cliqué — sur une config multi-écrans où le primaire est le portable
        // (jamais regardé), la fenêtre semblait invisible. On la place plutôt sur l'écran du
        // curseur, via Win32 SetWindowPos en pixels physiques directs (PAS Window.Left/Top,
        // dont la valeur se fait retransformer de façon incontrôlable sous PerMonitorV2 dans
        // cet environnement — constaté empiriquement : la position lue après assignation ne
        // correspondait jamais à la valeur assignée, quelle que soit la formule employée).
        SourceInitialized += (_, _) =>
        {
            var cursorScreen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var workArea = cursorScreen.WorkingArea;
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, IntPtr.Zero, workArea.Left + 40, workArea.Top + 40, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        };

        _settingsStore = settingsStore;
        _apiKeyStore = apiKeyStore;

        // Copie de travail : les modifications ne s'appliquent qu'au clic sur Enregistrer.
        _workingCopy = new AppSettings
        {
            TriggerKind = currentSettings.TriggerKind,
            HotkeyModifiers = currentSettings.HotkeyModifiers,
            HotkeyKey = currentSettings.HotkeyKey,
            MouseButton = currentSettings.MouseButton,
            SecondaryTriggerEnabled = currentSettings.SecondaryTriggerEnabled,
            SecondaryTriggerKind = currentSettings.SecondaryTriggerKind,
            SecondaryHotkeyModifiers = currentSettings.SecondaryHotkeyModifiers,
            SecondaryHotkeyKey = currentSettings.SecondaryHotkeyKey,
            SecondaryMouseButton = currentSettings.SecondaryMouseButton,
            MaxRecordingSeconds = currentSettings.MaxRecordingSeconds,
            MicrophoneDeviceId = currentSettings.MicrophoneDeviceId,
            SecondaryMicrophoneDeviceId = currentSettings.SecondaryMicrophoneDeviceId,
            TranscriptionModel = currentSettings.TranscriptionModel,
            Language = currentSettings.Language,
            ShowRecIndicator = currentSettings.ShowRecIndicator,
            ShowTaskbarIndicator = currentSettings.ShowTaskbarIndicator,
            ShowLastTranscription = currentSettings.ShowLastTranscription,
            HistoryEnabled = currentSettings.HistoryEnabled,
            ShowHistoryModule = currentSettings.ShowHistoryModule,
            OverlayLeft = currentSettings.OverlayLeft,
            OverlayTop = currentSettings.OverlayTop,
            AlwaysOnTop = currentSettings.AlwaysOnTop,
            OverlayOpacity = currentSettings.OverlayOpacity,
            LaunchAtStartup = currentSettings.LaunchAtStartup,
            OverlayToggleModifiers = currentSettings.OverlayToggleModifiers,
            OverlayToggleKey = currentSettings.OverlayToggleKey,
            SecondaryToggleModifiers = currentSettings.SecondaryToggleModifiers,
            SecondaryToggleKey = currentSettings.SecondaryToggleKey,
            Theme = currentSettings.Theme,
            PrimaryColorHex = currentSettings.PrimaryColorHex,
        };

        _primaryTrigger.Kind = _workingCopy.TriggerKind;
        _primaryTrigger.Modifiers = _workingCopy.HotkeyModifiers;
        _primaryTrigger.Key = _workingCopy.HotkeyKey;
        _primaryTrigger.MouseButton = _workingCopy.MouseButton;
        UpdateTriggerDisplay();

        _secondaryTrigger.Kind = _workingCopy.SecondaryTriggerKind;
        _secondaryTrigger.Modifiers = _workingCopy.SecondaryHotkeyModifiers;
        _secondaryTrigger.Key = _workingCopy.SecondaryHotkeyKey;
        _secondaryTrigger.MouseButton = _workingCopy.SecondaryMouseButton;
        SecondaryTriggerEnabledCheckBox.IsChecked = _workingCopy.SecondaryTriggerEnabled;
        UpdateTriggerDisplay();

        _overlayToggleHotkey.Modifiers = _workingCopy.OverlayToggleModifiers;
        _overlayToggleHotkey.Key = _workingCopy.OverlayToggleKey;
        _secondaryToggleHotkey.Modifiers = _workingCopy.SecondaryToggleModifiers;
        _secondaryToggleHotkey.Key = _workingCopy.SecondaryToggleKey;
        UpdateHotkeyDisplays();

        _selectedTheme = _workingCopy.Theme;
        _selectedPrimaryColorHex = _workingCopy.PrimaryColorHex;
        (_selectedTheme == AppTheme.Light ? LightThemeRadio : DarkThemeRadio).IsChecked = true;
        BuildPrimaryColorSwatches();

        ModelComboBox.ItemsSource = modelCatalogService.GetAvailableModels();
        ModelComboBox.SelectedValue = _workingCopy.TranscriptionModel;

        var inputDevices = AudioDeviceEnumerator.GetInputDevices();

        var primaryDevices = new List<AudioDeviceInfo> { new("", "(Périphérique par défaut du système)") };
        primaryDevices.AddRange(inputDevices);
        MicrophoneComboBox.ItemsSource = primaryDevices;
        MicrophoneComboBox.SelectedValue = _workingCopy.MicrophoneDeviceId ?? "";

        var secondaryDevices = new List<AudioDeviceInfo> { new("", "(Aucun)") };
        secondaryDevices.AddRange(inputDevices);
        SecondaryMicrophoneComboBox.ItemsSource = secondaryDevices;
        SecondaryMicrophoneComboBox.SelectedValue = _workingCopy.SecondaryMicrophoneDeviceId ?? "";

        foreach (var item in LanguageComboBox.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem { Tag: string tag } comboBoxItem &&
                tag == _workingCopy.Language)
            {
                LanguageComboBox.SelectedItem = comboBoxItem;
                break;
            }
        }
        LanguageComboBox.SelectedItem ??= LanguageComboBox.Items[0];

        MaxDurationTextBox.Text = _workingCopy.MaxRecordingSeconds.ToString();
        ShowRecIndicatorCheckBox.IsChecked = _workingCopy.ShowRecIndicator;
        ShowTaskbarIndicatorCheckBox.IsChecked = _workingCopy.ShowTaskbarIndicator;
        ShowLastTranscriptionCheckBox.IsChecked = _workingCopy.ShowLastTranscription;
        AlwaysOnTopCheckBox.IsChecked = _workingCopy.AlwaysOnTop;
        LaunchAtStartupCheckBox.IsChecked = _workingCopy.LaunchAtStartup;
        OverlayOpacitySlider.Value = _workingCopy.OverlayOpacity;

        ApiKeyStatusText.Text = _apiKeyStore.HasApiKey() ? "Clé enregistrée ✓" : "Aucune clé enregistrée";
    }

    public AppSettings ResultSettings => _workingCopy;

    private void CaptureTrigger_Click(object sender, RoutedEventArgs e) => BeginTriggerCapture(_primaryTrigger);

    private void CaptureSecondaryTrigger_Click(object sender, RoutedEventArgs e) => BeginTriggerCapture(_secondaryTrigger);

    private void BeginTriggerCapture(TriggerSlot slot)
    {
        _capturingSlot = slot;
        TriggerCaptureButton.IsEnabled = false;
        SecondaryTriggerCaptureButton.IsEnabled = false;
        (slot == _primaryTrigger ? TriggerText : SecondaryTriggerText).Text =
            "Appuie sur une touche ou un bouton de souris (Échap pour annuler)…";
        PreviewKeyDown += SettingsWindow_PreviewKeyDownForCapture;
        PreviewMouseDown += SettingsWindow_PreviewMouseDownForCapture;
    }

    private void SettingsWindow_PreviewKeyDownForCapture(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_capturingSlot is not { } slot)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            EndCapture();
            return;
        }

        if (IsModifierKey(key))
        {
            return; // On attend une vraie touche, pas seulement le modificateur.
        }

        slot.Kind = TriggerKind.Keyboard;
        slot.Modifiers = Keyboard.Modifiers;
        slot.Key = key;
        EndCapture();
    }

    private void SettingsWindow_PreviewMouseDownForCapture(object sender, MouseButtonEventArgs e)
    {
        if (_capturingSlot is not { } slot)
        {
            return;
        }

        MouseTriggerButton? button = e.ChangedButton switch
        {
            System.Windows.Input.MouseButton.Middle => MouseTriggerButton.Middle,
            System.Windows.Input.MouseButton.XButton1 => MouseTriggerButton.XButton1,
            System.Windows.Input.MouseButton.XButton2 => MouseTriggerButton.XButton2,
            _ => null, // Clic gauche/droit ignoré : on continue d'écouter.
        };

        if (button is null)
        {
            return;
        }

        e.Handled = true;
        slot.Kind = TriggerKind.Mouse;
        slot.MouseButton = button.Value;
        EndCapture();
    }

    private void EndCapture()
    {
        _capturingSlot = null;
        TriggerCaptureButton.IsEnabled = true;
        SecondaryTriggerCaptureButton.IsEnabled = true;
        PreviewKeyDown -= SettingsWindow_PreviewKeyDownForCapture;
        PreviewMouseDown -= SettingsWindow_PreviewMouseDownForCapture;
        UpdateTriggerDisplay();
    }

    private void UpdateTriggerDisplay()
    {
        TriggerText.Text = FormatTrigger(_primaryTrigger);
        SecondaryTriggerText.Text = FormatTrigger(_secondaryTrigger);
    }

    private static string FormatTrigger(TriggerSlot slot) => slot.Kind == TriggerKind.Keyboard
        ? TriggerFormatter.FormatKeyboard(slot.Modifiers, slot.Key)
        : TriggerFormatter.FormatMouse(slot.MouseButton);

    // --- Raccourcis clavier simples (overlay, déclencheur secondaire) : touche uniquement, sans souris ---

    private void CaptureOverlayToggle_Click(object sender, RoutedEventArgs e) =>
        BeginHotkeyCapture(_overlayToggleHotkey, OverlayToggleCaptureButton, OverlayToggleText);

    private void CaptureSecondaryToggle_Click(object sender, RoutedEventArgs e) =>
        BeginHotkeyCapture(_secondaryToggleHotkey, SecondaryToggleCaptureButton, SecondaryToggleText);

    private void BeginHotkeyCapture(TriggerSlot slot, System.Windows.Controls.Button button, System.Windows.Controls.TextBlock text)
    {
        _capturingHotkey = (slot, button, text);
        button.IsEnabled = false;
        text.Text = "Appuie sur une touche (Échap pour annuler)…";
        PreviewKeyDown += SettingsWindow_PreviewKeyDownForHotkeyCapture;
    }

    private void SettingsWindow_PreviewKeyDownForHotkeyCapture(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_capturingHotkey is not { } capture)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            EndHotkeyCapture();
            return;
        }

        if (IsModifierKey(key))
        {
            return;
        }

        capture.Slot.Modifiers = Keyboard.Modifiers;
        capture.Slot.Key = key;
        EndHotkeyCapture();
    }

    private void EndHotkeyCapture()
    {
        if (_capturingHotkey is { } capture)
        {
            capture.Button.IsEnabled = true;
        }

        _capturingHotkey = null;
        PreviewKeyDown -= SettingsWindow_PreviewKeyDownForHotkeyCapture;
        UpdateHotkeyDisplays();
    }

    private void UpdateHotkeyDisplays()
    {
        OverlayToggleText.Text = TriggerFormatter.FormatKeyboard(_overlayToggleHotkey.Modifiers, _overlayToggleHotkey.Key);
        SecondaryToggleText.Text = TriggerFormatter.FormatKeyboard(_secondaryToggleHotkey.Modifiers, _secondaryToggleHotkey.Key);
    }

    private void BuildPrimaryColorSwatches()
    {
        _primaryColorSwatches.Clear();
        PrimaryColorPanel.Children.Clear();

        foreach (var (name, hex) in ThemeManager.PrimaryColorChoices)
        {
            var swatch = new System.Windows.Controls.Border
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0),
                CornerRadius = new CornerRadius(4),
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!),
                BorderThickness = new Thickness(2),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                ToolTip = name,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = hex,
            };
            swatch.MouseLeftButtonDown += (_, _) =>
            {
                _selectedPrimaryColorHex = hex;
                HighlightSelectedSwatch();
                ThemeManager.Apply(_selectedTheme, _selectedPrimaryColorHex);
            };
            _primaryColorSwatches.Add(swatch);
            PrimaryColorPanel.Children.Add(swatch);
        }

        HighlightSelectedSwatch();
    }

    private void HighlightSelectedSwatch()
    {
        foreach (var swatch in _primaryColorSwatches)
        {
            var isSelected = (string)swatch.Tag == _selectedPrimaryColorHex;
            swatch.BorderBrush = isSelected
                ? System.Windows.Media.Brushes.White
                : System.Windows.Media.Brushes.Transparent;
        }
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        _selectedTheme = ReferenceEquals(sender, LightThemeRadio) ? AppTheme.Light : AppTheme.Dark;
        ThemeManager.Apply(_selectedTheme, _selectedPrimaryColorHex);
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private void OverlayOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OverlayOpacityText is not null)
        {
            OverlayOpacityText.Text = $"{e.NewValue:P0}";
        }
    }

    private void SaveApiKey_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = ApiKeyPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        _apiKeyStore.SaveApiKey(apiKey);
        ApiKeyPasswordBox.Clear();
        ApiKeyStatusText.Text = "Clé enregistrée ✓";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxDurationTextBox.Text, out var maxDuration) || maxDuration <= 0)
        {
            System.Windows.MessageBox.Show(
                "La durée maximale doit être un nombre entier de secondes supérieur à 0.",
                "Paramètres", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var secondaryEnabled = SecondaryTriggerEnabledCheckBox.IsChecked ?? false;
        var candidate = new AppSettings
        {
            TriggerKind = _primaryTrigger.Kind,
            HotkeyModifiers = _primaryTrigger.Modifiers,
            HotkeyKey = _primaryTrigger.Key,
            MouseButton = _primaryTrigger.MouseButton,
            SecondaryTriggerKind = _secondaryTrigger.Kind,
            SecondaryHotkeyModifiers = _secondaryTrigger.Modifiers,
            SecondaryHotkeyKey = _secondaryTrigger.Key,
            SecondaryMouseButton = _secondaryTrigger.MouseButton,
        };
        if (secondaryEnabled && TriggerFormatter.SecondaryDuplicatesPrimary(candidate))
        {
            System.Windows.MessageBox.Show(
                "Le déclencheur secondaire est identique au principal : chaque pression démarrerait puis arrêterait aussitôt l'enregistrement. Choisis un déclencheur différent.",
                "Paramètres", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _workingCopy.TriggerKind = _primaryTrigger.Kind;
        _workingCopy.HotkeyModifiers = _primaryTrigger.Modifiers;
        _workingCopy.HotkeyKey = _primaryTrigger.Key;
        _workingCopy.MouseButton = _primaryTrigger.MouseButton;
        _workingCopy.SecondaryTriggerEnabled = secondaryEnabled;
        _workingCopy.SecondaryTriggerKind = _secondaryTrigger.Kind;
        _workingCopy.SecondaryHotkeyModifiers = _secondaryTrigger.Modifiers;
        _workingCopy.SecondaryHotkeyKey = _secondaryTrigger.Key;
        _workingCopy.SecondaryMouseButton = _secondaryTrigger.MouseButton;
        _workingCopy.MaxRecordingSeconds = maxDuration;
        _workingCopy.TranscriptionModel = (string)ModelComboBox.SelectedValue;
        _workingCopy.MicrophoneDeviceId = string.IsNullOrEmpty((string)MicrophoneComboBox.SelectedValue)
            ? null
            : (string)MicrophoneComboBox.SelectedValue;
        _workingCopy.SecondaryMicrophoneDeviceId = string.IsNullOrEmpty((string)SecondaryMicrophoneComboBox.SelectedValue)
            ? null
            : (string)SecondaryMicrophoneComboBox.SelectedValue;
        _workingCopy.Language = (string)((System.Windows.Controls.ComboBoxItem)LanguageComboBox.SelectedItem).Tag;
        _workingCopy.ShowRecIndicator = ShowRecIndicatorCheckBox.IsChecked ?? true;
        _workingCopy.ShowTaskbarIndicator = ShowTaskbarIndicatorCheckBox.IsChecked ?? true;
        _workingCopy.ShowLastTranscription = ShowLastTranscriptionCheckBox.IsChecked ?? true;
        _workingCopy.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked ?? true;
        _workingCopy.OverlayOpacity = OverlayOpacitySlider.Value;
        _workingCopy.LaunchAtStartup = LaunchAtStartupCheckBox.IsChecked ?? false;
        _workingCopy.OverlayToggleModifiers = _overlayToggleHotkey.Modifiers;
        _workingCopy.OverlayToggleKey = _overlayToggleHotkey.Key;
        _workingCopy.SecondaryToggleModifiers = _secondaryToggleHotkey.Modifiers;
        _workingCopy.SecondaryToggleKey = _secondaryToggleHotkey.Key;
        _workingCopy.Theme = _selectedTheme;
        _workingCopy.PrimaryColorHex = _selectedPrimaryColorHex;

        _settingsStore.Save(_workingCopy);
        StartupRegistration.SetEnabled(_workingCopy.LaunchAtStartup);

        SettingsSaved = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // L'aperçu en direct du thème a pu modifier les ressources globales : on les restaure
        // puisque rien de ce dialogue ne doit s'appliquer après Annuler.
        ThemeManager.Apply(_workingCopy.Theme, _workingCopy.PrimaryColorHex);
        DialogResult = false;
        Close();
    }

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
