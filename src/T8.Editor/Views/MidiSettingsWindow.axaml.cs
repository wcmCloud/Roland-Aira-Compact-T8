using Avalonia.Controls;
using Avalonia.Interactivity;

namespace T8.Editor.Views;

public partial class MidiSettingsWindow : Window
{
    public MidiSettingsWindow() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
