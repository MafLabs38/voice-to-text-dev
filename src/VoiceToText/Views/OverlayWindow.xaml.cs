using System.Linq;
using System.Windows;
using System.Windows.Input;
using VoiceToText.Services;
using VoiceToText.ViewModels;

namespace VoiceToText.Views;

public partial class OverlayWindow : Window
{
    private readonly Action<double, double, string?> _onPositionChanged;
    private double? _preferredLeft;
    private double? _preferredTop;
    private string? _preferredScreenDeviceName;

    public OverlayWindow(OverlayViewModel viewModel, double? left, double? top, string? screenDeviceName, Action<double, double, string?> onPositionChanged)
    {
        InitializeComponent();
        DataContext = viewModel;
        _onPositionChanged = onPositionChanged;

        ApplyPreferredPosition(left, top, screenDeviceName);

        // Se réaligne dès qu'une config d'écrans change (branchement/débranchement d'un moniteur
        // externe) plutôt qu'uniquement au démarrage — sans ça, la fenêtre reste bloquée sur son
        // repli tant que l'appli n'est pas relancée, même une fois l'écran préféré reconnecté.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void ApplyPreferredPosition(double? left, double? top, string? screenDeviceName)
    {
        _preferredLeft = left;
        _preferredTop = top;
        _preferredScreenDeviceName = screenDeviceName;

        var screens = System.Windows.Forms.Screen.AllScreens
            .Select(s => new OverlayPositioning.ScreenInfo(s.DeviceName, s.Bounds))
            .ToList();

        (Left, Top) = OverlayPositioning.ResolvePosition(left, top, screenDeviceName, screens, FallbackWorkArea());
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => ApplyPreferredPosition(_preferredLeft, _preferredTop, _preferredScreenDeviceName)));
    }

    /// <summary>
    /// Revérifie, au plus tard possible (après la fin du démarrage), que la position ACTUELLE de
    /// la fenêtre tombe bien sur un écran réellement connecté à cet instant précis, plutôt que de
    /// se fier à un calcul fait plus tôt.
    /// </summary>
    public void RevalidatePosition()
    {
        var onScreen = System.Windows.Forms.Screen.AllScreens
            .Any(s => s.Bounds.Contains(new System.Drawing.Point((int)Left, (int)Top)));
        if (onScreen)
        {
            return;
        }

        var fallback = FallbackWorkArea();
        Left = fallback.Right - 160;
        Top = fallback.Bottom - 80;
    }

    /// <summary>
    /// Écran de repli quand aucune position mémorisée valide n'est disponible : le plus grand
    /// écran connecté plutôt que l'écran "primaire" Windows — sur un portable docké à des écrans
    /// externes, l'écran primaire est presque toujours l'écran du portable, que l'utilisateur ne
    /// regarde jamais (le repli sur celui-ci rendait l'overlay perçu comme invisible).
    /// </summary>
    private static System.Drawing.Rectangle FallbackWorkArea()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var largest = screens.OrderByDescending(s => (long)s.Bounds.Width * s.Bounds.Height).First();
        return largest.WorkingArea;
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();

        var screenDeviceName = System.Windows.Forms.Screen.AllScreens
            .FirstOrDefault(s => s.Bounds.Contains(new System.Drawing.Point((int)Left, (int)Top)))
            ?.DeviceName;
        _preferredLeft = Left;
        _preferredTop = Top;
        _preferredScreenDeviceName = screenDeviceName;
        _onPositionChanged(Left, Top, screenDeviceName);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OverlayViewModel viewModel && !string.IsNullOrEmpty(viewModel.LastTranscriptionText))
        {
            System.Windows.Clipboard.SetText(viewModel.LastTranscriptionText);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
