using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using T8.Editor.Help;

namespace T8.Editor.Views;

/// <summary>A small ? button that shows one help topic (<see cref="HelpTopics"/>) in a flyout.</summary>
public partial class HelpTip : UserControl
{
    public static readonly StyledProperty<string?> TopicProperty =
        AvaloniaProperty.Register<HelpTip, string?>(nameof(Topic));

    public HelpTip()
    {
        InitializeComponent();
    }

    /// <summary>The topic id, one of the <see cref="HelpTopics"/> constants.</summary>
    public string? Topic
    {
        get => GetValue(TopicProperty);
        set => SetValue(TopicProperty, value);
    }

    /// <summary>The flyout's button, for tests.</summary>
    public Button Button => TipButton;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TopicProperty)
        {
            var topic = HelpTopics.Get(Topic);
            TipTitle.Text = topic.Title;
            TipParagraphs.ItemsSource = topic.Paragraphs;
            ToolTip.SetTip(TipButton, $"Help: {topic.Title}");
        }
    }

    private void OnMoreHelp(object? sender, RoutedEventArgs e)
    {
        TipButton.Flyout?.Hide();
        HelpWindow.Open(TopLevel.GetTopLevel(this) as Window, Topic);
    }
}
