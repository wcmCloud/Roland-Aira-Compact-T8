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
    [Fact]
    public void Every_topic_has_a_unique_id_a_title_and_text()
    {
        Assert.Equal(HelpTopics.All.Count, HelpTopics.All.Select(t => t.Id).Distinct().Count());
        Assert.All(HelpTopics.All, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Title));
            Assert.NotEmpty(t.Paragraphs);
        });

        // Every topic constant resolves to its own topic.
        var ids = typeof(HelpTopics).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.Equal(HelpTopics.All.Count, ids.Count);
        Assert.All(ids, id => Assert.Equal(id, HelpTopics.Get(id).Id));
        Assert.Equal(HelpTopics.GettingStarted, HelpTopics.Get("no such topic").Id);
    }

    [AvaloniaFact]
    public void Each_tab_and_the_settings_have_help_icons_for_real_topics()
    {
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), new FakeDialogService(), driveCandidates: () => []);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var tabs = window.FindControl<TabControl>("Tabs")!;
        var ids = HelpTopics.All.Select(t => t.Id).ToHashSet();
        var seen = new HashSet<string>();
        for (var i = 0; i < 3; i++)
        {
            tabs.SelectedIndex = i;
            window.UpdateLayout();
            foreach (var tip in window.GetVisualDescendants().OfType<HelpTip>())
            {
                Assert.Contains(tip.Topic!, ids);
                seen.Add(tip.Topic!);
            }
        }
        Assert.Superset(new HashSet<string> { HelpTopics.Patterns, HelpTopics.Playback, HelpTopics.Rhythm, HelpTopics.MultiSelect,
            HelpTopics.RhythmGenerator, HelpTopics.Fills, HelpTopics.Bass, HelpTopics.StepEditor, HelpTopics.BassGenerator,
            HelpTopics.Song, HelpTopics.SongPlayback }, seen);
        window.Close();

        var settings = new MidiSettingsWindow { DataContext = new MidiSettingsViewModel(FakePortProvider.WithT8(), new T8.Editor.Services.EditorSettings()) };
        settings.Show();
        var settingTopics = settings.GetVisualDescendants().OfType<HelpTip>().Select(t => t.Topic).ToList();
        Assert.Equal([HelpTopics.Connection, HelpTopics.MidiChannels, HelpTopics.DrumNotes, HelpTopics.PlayModes, HelpTopics.OnTheT8], settingTopics);
        settings.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void A_help_icon_shows_its_topic_in_a_flyout()
    {
        var tip = new HelpTip { Topic = HelpTopics.Fills };
        var window = new Window { Content = tip };
        window.Show();
        tip.Button.Flyout!.ShowAt(tip.Button);
        window.UpdateLayout();

        var flyout = (Flyout)tip.Button.Flyout;
        var content = (Control)flyout.Content!;
        var texts = content.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Fills", texts);
        Assert.Contains(texts, t => t!.Contains("SHIFT + STEP 8"));
        Assert.Equal("Help: Fills", ToolTip.GetTip(tip.Button));
        window.Close();
    }

    [AvaloniaFact]
    public void F1_and_the_help_button_open_the_help()
    {
        var dialogs = new FakeDialogService();
        var vm = new MainWindowViewModel(FakePortProvider.WithT8(), dialogs, driveCandidates: () => []);
        var window = new MainWindow { DataContext = vm };
        window.Show();

        window.FindControl<Button>("HelpButton")!.Command!.Execute(null);
        Assert.Single(dialogs.HelpTopicsShown);
        Assert.Contains(window.KeyBindings, k => k.Gesture.Key == Avalonia.Input.Key.F1 && k.Command == vm.ShowHelpCommand);
        window.Close();
        vm.Dispose();
    }

    [AvaloniaFact]
    public void The_help_window_lists_every_topic_and_opens_at_one()
    {
        var help = new HelpWindow();
        help.Show();
        Assert.Equal(HelpTopics.All.Count, help.FindControl<ListBox>("TopicList")!.ItemCount);

        help.ShowTopic(HelpTopics.Song);
        help.UpdateLayout();
        Assert.Equal("Songs", help.FindControl<TextBlock>("TopicTitle")!.Text);
        Assert.Contains(help.FindControl<ItemsControl>("TopicParagraphs")!.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text!.Contains("COPY TO BANK"));
        help.Close();
    }
}
