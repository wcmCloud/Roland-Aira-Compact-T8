using Avalonia.Controls;
using Avalonia.Threading;
using T8.Editor.ViewModels;

namespace T8.Editor.Views;

public partial class MainWindow : Window
{
    // About two updates per 16th note at 300 BPM.
    private readonly DispatcherTimer _playheadTimer = new() { Interval = TimeSpan.FromMilliseconds(20) };

    public MainWindow()
    {
        InitializeComponent();
        _playheadTimer.Tick += (_, _) => (DataContext as MainWindowViewModel)?.UpdatePlayhead();
        Opened += (_, _) => _playheadTimer.Start();
        Closed += (_, _) => _playheadTimer.Stop();
        // Coming back to the editor, e.g. after switching the T-8 into drive mode: look for its drive again.
        Activated += (_, _) => (DataContext as MainWindowViewModel)?.RefreshT8Drive();
    }
}
