using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace T8.Editor.Views;

/// <summary>
/// Shows the small Markdown subset used by the help (docs/help): paragraphs, "## headings", "- " bullets
/// (continued by indented lines), **bold**, *italic* and `code`.
/// </summary>
public sealed class MarkdownView : StackPanel
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    public static readonly StyledProperty<double> TextSizeProperty =
        AvaloniaProperty.Register<MarkdownView, double>(nameof(TextSize), 12.5);

    public MarkdownView()
    {
        Spacing = 6;
    }

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public double TextSize
    {
        get => GetValue(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty || change.Property == TextSizeProperty)
            Build();
    }

    private void Build()
    {
        Children.Clear();
        foreach (var block in Blocks(Markdown ?? ""))
        {
            if (block.StartsWith("## "))
            {
                Children.Add(new TextBlock
                {
                    Text = block[3..].Trim().ToUpperInvariant(),
                    Classes = { "helpHeading" },
                    Margin = new Thickness(0, Children.Count == 0 ? 0 : 8, 0, 0),
                });
            }
            else if (block.StartsWith("- "))
            {
                var bullet = new DockPanel { Margin = new Thickness(4, 0, 0, 0) };
                var dot = new TextBlock { Text = "•", Margin = new Thickness(0, 0, 8, 0), FontSize = TextSize, Classes = { "helpBullet" } };
                DockPanel.SetDock(dot, Dock.Left);
                bullet.Children.Add(dot);
                bullet.Children.Add(Paragraph(block[2..]));
                Children.Add(bullet);
            }
            else
            {
                Children.Add(Paragraph(block));
            }
        }
    }

    /// <summary>Splits the text into headings, bullets (with their indented continuation lines) and paragraphs.</summary>
    private static IEnumerable<string> Blocks(string markdown)
    {
        var current = new List<string>();
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var startsBlock = line.Length == 0 || line.StartsWith("## ") || line.StartsWith("- ");
            var continues = line.StartsWith("  ") && current.Count > 0;
            if (startsBlock && !continues && current.Count > 0)
            {
                yield return string.Join(' ', current);
                current.Clear();
            }
            if (line.Length == 0)
                continue;
            current.Add(continues ? line.Trim() : line);
            if (line.StartsWith("## "))
            {
                yield return line;
                current.Clear();
            }
        }
        if (current.Count > 0)
            yield return string.Join(' ', current);
    }

    private TextBlock Paragraph(string text)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = TextSize, LineHeight = TextSize * 1.5 };
        var inlines = block.Inlines ??= new InlineCollection();
        foreach (var (part, style) in Spans(text))
        {
            var run = new Run(part);
            switch (style)
            {
                case '*':
                    run.FontWeight = FontWeight.SemiBold;
                    break;
                case '_':
                    run.FontStyle = FontStyle.Italic;
                    break;
                case '`':
                    run.FontFamily = new FontFamily("Menlo, Consolas, monospace");
                    run.Foreground = new SolidColorBrush(Color.Parse("#FF9A4A"));
                    break;
            }
            inlines.Add(run);
        }
        return block;
    }

    /// <summary>Plain text, **bold** ('*'), *italic* ('_') and `code` ('`') spans.</summary>
    internal static IEnumerable<(string Text, char Style)> Spans(string text)
    {
        var i = 0;
        var plain = new System.Text.StringBuilder();
        while (i < text.Length)
        {
            (string Open, char Style)? marker = text.AsSpan(i).StartsWith("**") ? ("**", '*')
                : text[i] == '`' ? ("`", '`')
                : text[i] == '*' ? ("*", '_')
                : null;
            if (marker is { } m)
            {
                var end = text.IndexOf(m.Open, i + m.Open.Length, StringComparison.Ordinal);
                if (end > i + m.Open.Length)
                {
                    if (plain.Length > 0)
                    {
                        yield return (plain.ToString(), ' ');
                        plain.Clear();
                    }
                    yield return (text[(i + m.Open.Length)..end], m.Style);
                    i = end + m.Open.Length;
                    continue;
                }
            }
            plain.Append(text[i]);
            i++;
        }
        if (plain.Length > 0)
            yield return (plain.ToString(), ' ');
    }
}
