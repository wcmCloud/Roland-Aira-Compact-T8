using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using T8.Editor.Help;

namespace T8.Editor.Views;

/// <summary>The help window: every help topic in a list, the chosen one on the right. One window at a time.</summary>
public partial class HelpWindow : Window
{
    private static HelpWindow? _open;

    public HelpWindow()
    {
        InitializeComponent();
        TopicList.ItemsSource = HelpTopics.All;
        TopicList.SelectionChanged += (_, _) =>
        {
            if (TopicList.SelectedItem is HelpTopic topic)
            {
                DataContext = topic;
                TopicScroller.Offset = default;
            }
        };
        TopicList.SelectedIndex = 0;
    }

    /// <summary>Shows the help at a topic, reusing the help window if it is already open.</summary>
    public static HelpWindow Open(Window? owner, string? topic = null)
    {
        if (_open is null)
        {
            _open = new HelpWindow();
            _open.Closed += (_, _) => _open = null;
            if (owner is not null)
                _open.Show(owner);
            else
                _open.Show();
        }
        _open.ShowTopic(topic);
        _open.Activate();
        return _open;
    }

    public void ShowTopic(string? topic) => TopicList.SelectedItem = HelpTopics.Get(topic);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnOnlineGuide(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppInfo.Website + "#readme") { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser to open.
        }
    }
}
