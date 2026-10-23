using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using T8.Editor.Help;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class HelpTests
{
    private static IEnumerable<string> Constants(Type type) =>
        type.GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Every_topic_loads_with_a_title_and_text_in_order()
    {
        Assert.Equal(HelpTopics.Order, HelpContent.Topics.Select(t => t.Id));
        Assert.Equal(Constants(typeof(HelpTopics)).OrderBy(x => x), HelpTopics.Order.OrderBy(x => x));
        Assert.All(HelpContent.Topics, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Title));
            Assert.True(t.Markdown.Length > 100, $"{t.Id} has almost no text");
            Assert.DoesNotContain("# ", t.Markdown.Split('\n')[0]); // the title line isn't repeated in the text
        });
        Assert.Equal(HelpTopics.GettingStarted, HelpContent.Topic("no such topic").Id);
    }

    [Fact]
    public void Every_popup_loads_short_text_and_links_to_a_real_topic()
    {
        var tipIds = Constants(typeof(HelpTips)).ToList();
        Assert.Equal(tipIds.OrderBy(x => x), HelpContent.Tips.Keys.OrderBy(x => x));
        foreach (var id in tipIds)
        {
            var tip = HelpContent.Tip(id)!;
            Assert.False(string.IsNullOrWhiteSpace(tip.Title));
            Assert.False(string.IsNullOrWhiteSpace(tip.Markdown));
            Assert.True(tip.Markdown.Length < 450, $"the {id} popup is long ({tip.Markdown.Length} chars)");
            Assert.Contains(tip.TopicId, HelpTopics.Order);
        }
    }

    [Fact]
    public void Search_finds_topics_by_title_and_text()
    {
        Assert.Equal(HelpContent.Topics.Count, HelpContent.Search("").Count);
        Assert.Contains(HelpContent.Search("transpose"), t => t.Id == HelpTopics.BassEditing);
        Assert.Contains(HelpContent.Search("DRIVE mode"), t => t.Id == HelpTopics.DriveMode);
        Assert.Equal([HelpTopics.Song], HelpContent.Search(".t8song copy bank").Select(t => t.Id));
        Assert.Empty(HelpContent.Search("zzzz"));
    }

    [Fact]
    public void Markdown_spans_are_split_into_bold_italic_and_code()
    {
        var spans = MarkdownView.Spans("Press **Save**, then `dOnE` *now*.").ToList();
        Assert.Equal([("Press ", ' '), ("Save", '*'), (", then ", ' '), ("dOnE", '`'), (" ", ' '), ("now", '_'), (".", ' ')], spans);
    }

    [AvaloniaFact]
    public void Every_help_icon_shows_its_popup_and_the_link_opens_its_topic()
    {
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), new FakeDialogService(), driveCandidates: () => []);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var tabs = window.FindControl<TabControl>("Tabs")!;
        var seen = new HashSet<string>();
        for (var i = 0; i < 3; i++)
        {
            tabs.SelectedIndex = i;
            if (i == 0)
                window.FindControl<Expander>("RhythmGeneratorExpander")!.IsExpanded = true;
            if (i == 1)
                window.FindControl<Expander>("BassGeneratorExpander")!.IsExpanded = true;
            vm.Rhythm.SelectedCell = vm.Rhythm.Tracks[0].Cells[0];
            window.UpdateLayout();
            foreach (var icon in window.GetVisualDescendants().OfType<HelpTip>().Where(t => t.IsEffectivelyVisible).ToList())
            {
                var tip = HelpContent.Tip(icon.Tip)!;
                Assert.NotNull(tip);
                seen.Add(icon.Tip!);

                icon.Button.Flyout!.ShowAt(icon.Button);
                var content = (Control)((Flyout)icon.Button.Flyout).Content!;
                Assert.Contains(tip.Title.ToUpperInvariant(), content.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                var help = icon.OpenMoreHelp();
                Assert.Equal(tip.TopicId, help.ShownTopic!.Id);
                help.Close();
            }
        }
        Assert.Superset(new HashSet<string>
        {
            HelpTips.Patterns, HelpTips.Playback, HelpTips.RhythmGrid, HelpTips.PatternKnobs, HelpTips.StepPanel,
            HelpTips.RhythmGenerator, HelpTips.Fills, HelpTips.BassPattern, HelpTips.BassStepEditor, HelpTips.BassGenerator,
            HelpTips.SongHeader, HelpTips.Arrangement, HelpTips.SongPlayback, HelpTips.CopyToBank, HelpTips.DriveMode,
        }, seen);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void The_midi_settings_cards_have_help_and_F1_opens_its_topic()
    {
        var settings = new MidiSettingsWindow { DataContext = new MidiSettingsViewModel(FakePortProvider.WithT8(), new T8.Editor.Services.EditorSettings()) };
        settings.Show();
        Assert.Equal([HelpTips.Connection, HelpTips.MidiChannels, HelpTips.DrumNotes, HelpTips.PlayButton, HelpTips.OnTheT8],
            settings.GetVisualDescendants().OfType<HelpTip>().Select(t => t.Tip));
        var help = settings.ShowHelp();
        Assert.Equal(HelpTopics.MidiSettings, help.ShownTopic!.Id);
        help.Close();
        settings.Close();
    }

    [AvaloniaFact]
    public void F1_opens_the_help_at_the_topic_for_the_open_tab()
    {
        var dialogs = new FakeDialogService();
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), dialogs, driveCandidates: () => []);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var f1 = window.KeyBindings.Single(k => k.Gesture.Key == Avalonia.Input.Key.F1);

        foreach (var (tab, topic) in new[] { (0, HelpTopics.RhythmEditing), (1, HelpTopics.BassEditing), (2, HelpTopics.Song) })
        {
            window.FindControl<TabControl>("Tabs")!.SelectedIndex = tab;
            f1.Command!.Execute(f1.CommandParameter);
            Assert.Equal(topic, dialogs.HelpTopicsShown[^1]);
        }
        window.FindControl<Button>("HelpButton")!.Command!.Execute(null);
        Assert.Equal(HelpTopics.Song, dialogs.HelpTopicsShown[^1]);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void The_help_window_searches_and_shows_markdown()
    {
        var help = new HelpWindow();
        help.Show();
        var list = help.FindControl<ListBox>("TopicList")!;
        Assert.Equal(HelpContent.Topics.Count, list.ItemCount);

        help.FindControl<TextBox>("SearchBox")!.Text = "triplet";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, list.ItemCount);
        Assert.Equal(HelpTopics.BassEditing, help.ShownTopic!.Id);
        help.FindControl<TextBox>("SearchBox")!.Text = "zzzz";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(help.FindControl<TextBlock>("NoResultsText")!.IsVisible);

        help.ShowTopic(HelpTopics.DriveMode);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        help.UpdateLayout();
        Assert.Equal(HelpContent.Topics.Count, list.ItemCount);
        Assert.Equal("LOADING AND SENDING PATTERNS", help.FindControl<TextBlock>("TopicTitle")!.Text);
        var texts = help.FindControl<MarkdownView>("TopicText")!.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(texts, t => t.Text == "DRIVE MODE" && t.Classes.Contains("helpHeading"));
        Assert.Contains(texts, t => t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text == "Hold PLAY and switch it on") == true);
        help.Close();
    }
}
