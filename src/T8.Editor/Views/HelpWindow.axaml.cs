using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using T8.Editor.Help;

namespace T8.Editor.Views;

/// <summary>The help window: a searchable list of the help topics (docs/help) and the chosen one. One at a time.</summary>
public partial class HelpWindow : Window
{
    private static HelpWindow? _open;

    public HelpWindow()
    {
        InitializeComponent();
        TopicList.SelectionChanged += (_, _) =>
        {
            if (TopicList.SelectedItem is HelpTopic topic)
            {
                TopicTitle.Text = topic.Title.ToUpperInvariant();
                TopicText.Markdown = topic.Markdown;
                TopicScroller.Offset = default;
            }
        };
        SearchBox.TextChanged += (_, _) => Filter(SearchBox.Text);
        Filter(null);
    }

    /// <summary>The window that is open, if any.</summary>
    public static HelpWindow? Current => _open;

    /// <summary>The topic shown now.</summary>
    public HelpTopic? ShownTopic => TopicList.SelectedItem as HelpTopic;

    /// <summary>Shows the help at a topic (docs/help file name), reusing the help window if it is already open.</summary>
    public static HelpWindow Open(Window? owner, string? topic = null)
    {
        if (_open is null)
        {
            _open = new HelpWindow();
            _open.Closed += (_, _) => _open = null;
            if (owner is not null && owner.IsVisible)
                _open.Show(owner);
            else
                _open.Show();
        }
        _open.ShowTopic(topic);
        _open.Activate();
        return _open;
    }

    public void ShowTopic(string? topic)
    {
        if (!string.IsNullOrEmpty(SearchBox.Text))
        {
            SearchBox.Text = "";
            Filter(null);
        }
        TopicList.SelectedItem = HelpContent.Topic(topic);
    }

    /// <summary>Lists the topics matching the search; keeps the shown one if it still matches.</summary>
    public void Filter(string? query)
    {
        var shown = ShownTopic;
        var topics = HelpContent.Search(query);
        TopicList.ItemsSource = topics;
        NoResultsText.IsVisible = topics.Count == 0;
        TopicList.SelectedItem = shown is not null && topics.Contains(shown) ? shown : topics.FirstOrDefault();
    }

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
