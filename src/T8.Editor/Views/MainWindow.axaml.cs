using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using T8.Editor.ViewModels;

namespace T8.Editor.Views;

public partial class MainWindow : Window
{
    // About two updates per 16th note at 300 BPM.
    private readonly DispatcherTimer _playheadTimer = new() { Interval = TimeSpan.FromMilliseconds(20) };

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        _playheadTimer.Tick += (_, _) => (DataContext as MainWindowViewModel)?.UpdatePlayhead();
        Opened += (_, _) => _playheadTimer.Start();
        Closed += (_, _) => _playheadTimer.Stop();
        // Coming back to the editor, e.g. after switching the T-8 into drive mode: look for its drive again.
        Activated += (_, _) => (DataContext as MainWindowViewModel)?.RefreshT8Drive();
    }

    /// <summary>
    /// Cmd/Ctrl+Z undoes the last change to the open patterns, and Shift+Cmd/Ctrl+Z redoes it.
    /// A text field keeps the key, so the seed box can still edit its own text.
    /// </summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Z || IsTextInput(e.Source))
            return;
        var mods = e.KeyModifiers;
        if (!mods.HasFlag(KeyModifiers.Control) && !mods.HasFlag(KeyModifiers.Meta))
            return;
        if (DataContext is not MainWindowViewModel vm)
            return;

        var command = mods.HasFlag(KeyModifiers.Shift) ? vm.RedoCommand : vm.UndoCommand;
        if (!command.CanExecute(null))
            return;
        command.Execute(null);
        e.Handled = true;
    }

    private static bool IsTextInput(object? source)
    {
        for (var current = source as Visual; current is not null; current = current.GetVisualParent())
        {
            if (current is TextBox or NumericUpDown)
                return true;
        }
        return false;
    }
}
