using System.Windows;
using System.Windows.Input;
using VoiceToText.Models;
using VoiceToText.Services;

namespace VoiceToText.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _workingCopy;
    private readonly SettingsStore _settingsStore;
    private readonly ApiKeyStore _apiKeyStore;

    private TriggerKind _capturedTriggerKind;
    private ModifierKeys _capturedModifiers;
    private Key _capturedKey;
    private MouseTriggerButton _capturedMouseButton;
    private bool _isCapturingTrigger;

    private ModifierKeys _capturedOverlayToggleModifiers;
    private Key _capturedOverlayToggleKey;
    private bool _isCapturingOverlayToggle;

    private AppTheme _selectedTheme;
    private string _selectedPrimaryColorHex = "#3B82F6";
    private readonly List<System.Windows.Controls.Border> _primaryColorSwatches = new();

    public bool SettingsSaved { get; private set; }

    public SettingsWindow(AppSettings currentSettings, SettingsStore settingsStore, ApiKeyStore apiKeyStore,
        TranscriptionModelCatalogService modelCatalogService)
    {
        InitializeComponent();

        _settingsStore = settingsStore;
        _apiKeyStore = apiKeyStore;

        // Copie de travail : les modifications ne s'appliquent qu'au clic sur Enregistrer.
        _workingCopy = new AppSettings
        {
            TriggerKind = currentSettings.TriggerKind,
            HotkeyModifiers = currentSettings.HotkeyModifiers,
            HotkeyKey = currentSettings.HotkeyKey,
            MouseButton = currentSettings.MouseButton,
            MaxRecordingSeconds = currentSettings.MaxRecordingSeconds,
            MicrophoneDeviceId = currentSettings.MicrophoneDeviceId,
            TranscriptionModel = currentSettings.TranscriptionModel,
            Language = currentSettings.Language,
            ShowRecIndicator = currentSettings.ShowRecIndicator,
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
            Theme = currentSettings.Theme,
            PrimaryColorHex = currentSettings.PrimaryColorHex,
        };

        _capturedTriggerKind = _workingCopy.TriggerKind;
        _capturedModifiers = _workingCopy.HotkeyModifiers;
        _capturedKey = _workingCopy.HotkeyKey;
        _capturedMouseButton = _workingCopy.MouseButton;
        UpdateTriggerDisplay();

        _capturedOverlayToggleModifiers = _workingCopy.OverlayToggleModifiers;
        _capturedOverlayToggleKey = _workingCopy.OverlayToggleKey;
        UpdateOverlayToggleDisplay();

        _selectedTheme = _workingCopy.Theme;
        _selectedPrimaryColorHex = _workingCopy.PrimaryColorHex;
        (_selectedTheme == AppTheme.Light ? LightThemeRadio : DarkThemeRadio).IsChecked = true;
        BuildPrimaryColorSwatches();

        ModelComboBox.ItemsSource = modelCatalogService.GetAvailableModels();
        ModelComboBox.SelectedValue = _workingCopy.TranscriptionModel;

        var devices = new List<AudioDeviceInfo> { new("", "(Périphérique par défaut du système)") };
        devices.AddRange(AudioDeviceEnumerator.GetInputDevices());
        MicrophoneComboBox.ItemsSource = devices;
        MicrophoneComboBox.SelectedValue = _workingCopy.MicrophoneDeviceId ?? "";

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
        ShowLastTranscriptionCheckBox.IsChecked = _workingCopy.ShowLastTranscription;
        AlwaysOnTopCheckBox.IsChecked = _workingCopy.AlwaysOnTop;
        LaunchAtStartupCheckBox.IsChecked = _workingCopy.LaunchAtStartup;
        OverlayOpacitySlider.Value = _workingCopy.OverlayOpacity;

        ApiKeyStatusText.Text = _apiKeyStore.HasApiKey() ? "Clé enregistrée ✓" : "Aucune clé enregistrée";
    }

    public AppSettings ResultSettings => _workingCopy;

    private void CaptureTrigger_Click(object sender, RoutedEventArgs e)
    {
        _isCapturingTrigger = true;
        TriggerCaptureButton.IsEnabled = false;
        TriggerText.Text = "Appuie sur une touche ou un bouton de souris (Échap pour annuler)…";
        PreviewKeyDown += SettingsWindow_PreviewKeyDownForCapture;
        PreviewMouseDown += SettingsWindow_PreviewMouseDownForCapture;
    }

    private void SettingsWindow_PreviewKeyDownForCapture(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isCapturingTrigger)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            EndCapture(applyCapture: false);
            return;
        }

        if (IsModifierKey(key))
        {
            return; // On attend une vraie touche, pas seulement le modificateur.
        }

        _capturedTriggerKind = TriggerKind.Keyboard;
        _capturedModifiers = Keyboard.Modifiers;
        _capturedKey = key;
        EndCapture(applyCapture: true);
    }

    private void SettingsWindow_PreviewMouseDownForCapture(object sender, MouseButtonEventArgs e)
    {
        if (!_isCapturingTrigger)
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
        _capturedTriggerKind = TriggerKind.Mouse;
        _capturedMouseButton = button.Value;
        EndCapture(applyCapture: true);
    }

    private void EndCapture(bool applyCapture)
    {
        _isCapturingTrigger = false;
        TriggerCaptureButton.IsEnabled = true;
        PreviewKeyDown -= SettingsWindow_PreviewKeyDownForCapture;
        PreviewMouseDown -= SettingsWindow_PreviewMouseDownForCapture;
        UpdateTriggerDisplay();
    }

    private void UpdateTriggerDisplay()
    {
        TriggerText.Text = _capturedTriggerKind == TriggerKind.Keyboard
            ? FormatKeyboardTrigger(_capturedModifiers, _capturedKey)
            : FormatMouseTrigger(_capturedMouseButton);
    }

    private void CaptureOverlayToggle_Click(object sender, RoutedEventArgs e)
    {
        _isCapturingOverlayToggle = true;
        OverlayToggleCaptureButton.IsEnabled = false;
        OverlayToggleText.Text = "Appuie sur une touche (Échap pour annuler)…";
        PreviewKeyDown += SettingsWindow_PreviewKeyDownForOverlayToggleCapture;
    }

    private void SettingsWindow_PreviewKeyDownForOverlayToggleCapture(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isCapturingOverlayToggle)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            EndOverlayToggleCapture();
            return;
        }

        if (IsModifierKey(key))
        {
            return;
        }

        _capturedOverlayToggleModifiers = Keyboard.Modifiers;
        _capturedOverlayToggleKey = key;
        EndOverlayToggleCapture();
    }

    private void EndOverlayToggleCapture()
    {
        _isCapturingOverlayToggle = false;
        OverlayToggleCaptureButton.IsEnabled = true;
        PreviewKeyDown -= SettingsWindow_PreviewKeyDownForOverlayToggleCapture;
        UpdateOverlayToggleDisplay();
    }

    private void UpdateOverlayToggleDisplay()
    {
        OverlayToggleText.Text = FormatKeyboardTrigger(_capturedOverlayToggleModifiers, _capturedOverlayToggleKey);
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

    private static string FormatKeyboardTrigger(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Maj");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key == Key.Space ? "Espace" : key.ToString());
        return string.Join("+", parts);
    }

    private static string FormatMouseTrigger(MouseTriggerButton button) => button switch
    {
        MouseTriggerButton.Middle => "Bouton milieu de la souris",
        MouseTriggerButton.XButton1 => "Bouton latéral 1 de la souris",
        MouseTriggerButton.XButton2 => "Bouton latéral 2 de la souris",
        _ => button.ToString(),
    };

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

        _workingCopy.TriggerKind = _capturedTriggerKind;
        _workingCopy.HotkeyModifiers = _capturedModifiers;
        _workingCopy.HotkeyKey = _capturedKey;
        _workingCopy.MouseButton = _capturedMouseButton;
        _workingCopy.MaxRecordingSeconds = maxDuration;
        _workingCopy.TranscriptionModel = (string)ModelComboBox.SelectedValue;
        _workingCopy.MicrophoneDeviceId = string.IsNullOrEmpty((string)MicrophoneComboBox.SelectedValue)
            ? null
            : (string)MicrophoneComboBox.SelectedValue;
        _workingCopy.Language = (string)((System.Windows.Controls.ComboBoxItem)LanguageComboBox.SelectedItem).Tag;
        _workingCopy.ShowRecIndicator = ShowRecIndicatorCheckBox.IsChecked ?? true;
        _workingCopy.ShowLastTranscription = ShowLastTranscriptionCheckBox.IsChecked ?? true;
        _workingCopy.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked ?? true;
        _workingCopy.OverlayOpacity = OverlayOpacitySlider.Value;
        _workingCopy.LaunchAtStartup = LaunchAtStartupCheckBox.IsChecked ?? false;
        _workingCopy.OverlayToggleModifiers = _capturedOverlayToggleModifiers;
        _workingCopy.OverlayToggleKey = _capturedOverlayToggleKey;
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
}
