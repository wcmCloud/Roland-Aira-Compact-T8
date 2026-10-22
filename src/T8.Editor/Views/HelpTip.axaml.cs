using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using T8.Editor.Help;

namespace T8.Editor.Views;

/// <summary>A small (?) button showing one popup text from docs/help/tips (<see cref="HelpTips"/>).</summary>
public partial class HelpTip : UserControl
{
    public static readonly StyledProperty<string?> TipProperty =
        AvaloniaProperty.Register<HelpTip, string?>(nameof(Tip));

    public HelpTip()
    {
        InitializeComponent();
    }

    /// <summary>The popup's id, one of the <see cref="HelpTips"/> constants.</summary>
    public string? Tip
    {
        get => GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    /// <summary>The help topic "More in the Help window" opens.</summary>
    public string? TopicId => HelpContent.Tip(Tip)?.TopicId;

    /// <summary>The (?) button, for tests.</summary>
    public Button Button => TipButton;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TipProperty)
        {
            var tip = HelpContent.Tip(Tip);
            TipTitle.Text = tip?.Title.ToUpperInvariant() ?? "HELP";
            TipText.Markdown = tip?.Markdown;
            ToolTip.SetTip(TipButton, tip is null ? "Help" : $"Help: {tip.Title}");
        }
    }

    /// <summary>Opens the help window at this popup's topic.</summary>
    public HelpWindow OpenMoreHelp()
    {
        TipButton.Flyout?.Hide();
        return HelpWindow.Open(TopLevel.GetTopLevel(this) as Window, TopicId);
    }

    private void OnMoreHelp(object? sender, RoutedEventArgs e) => OpenMoreHelp();
}
