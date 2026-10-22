using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using T8.Editor.Help;

namespace T8.Editor.Views;

public partial class MidiSettingsWindow : Window
{
    public MidiSettingsWindow()
    {
        InitializeComponent();
        // F1: the help at the MIDI settings topic.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F1)
            {
                ShowHelp();
                e.Handled = true;
            }
        };
    }

    /// <summary>Opens the help window at the MIDI settings topic (F1).</summary>
    public HelpWindow ShowHelp() => HelpWindow.Open(this, HelpTopics.MidiSettings);

    private void OnSave(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
