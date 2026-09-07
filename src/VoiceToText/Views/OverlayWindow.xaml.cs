using System.Windows;
using System.Windows.Input;
using VoiceToText.ViewModels;

namespace VoiceToText.Views;

public partial class OverlayWindow : Window
{
    private readonly Action<double, double> _onPositionChanged;

    public OverlayWindow(OverlayViewModel viewModel, double? left, double? top, Action<double, double> onPositionChanged)
    {
        InitializeComponent();
        DataContext = viewModel;
        _onPositionChanged = onPositionChanged;

        if (left is not null && top is not null)
        {
            Left = left.Value;
            Top = top.Value;
        }
        else
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - 160;
            Top = workArea.Bottom - 80;
        }
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
        _onPositionChanged(Left, Top);
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
