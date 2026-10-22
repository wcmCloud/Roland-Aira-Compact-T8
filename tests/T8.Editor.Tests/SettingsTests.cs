using T8.Core.Patterns;
using T8.Editor.Services;
using T8.Editor.ViewModels;
using T8.Midi;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class JsonSettingsStoreTests
{
    [Fact]
    public void Missing_file_gives_t8_defaults()
    {
        using var temp = TestPaths.CreateTempFolder();
        var settings = new JsonSettingsStore(Path.Combine(temp.Path, "settings.json")).Load();

        Assert.Equal(10, settings.Midi.RhythmChannel);
        Assert.Equal(PlayMode.PreviewNotes, settings.PlayMode);
        Assert.Equal(120, settings.Bpm);
    }

    [Fact]
    public void Saves_and_loads_every_setting()
    {
        using var temp = TestPaths.CreateTempFolder();
        var store = new JsonSettingsStore(Path.Combine(temp.Path, "sub", "settings.json"));
        var settings = new EditorSettings
        {
            OutputPortName = "T-8",
            PlayMode = PlayMode.SyncT8Sequencer,
            FollowSelection = true,
            AuditionEnabled = false,
            Bpm = 133,
            LastFolder = "/tmp/x",
            Midi = new T8MidiMap { BassChannel = 3, RhythmChannel = 9, ProgramChangeChannel = 1 },
        };
        settings.Midi.RhythmNotes[RhythmTrack.HT] = 48;

        store.Save(settings);
        var back = store.Load();

        Assert.Equal("T-8", back.OutputPortName);
        Assert.Equal(PlayMode.SyncT8Sequencer, back.PlayMode);
        Assert.True(back.FollowSelection);
        Assert.False(back.AuditionEnabled);
        Assert.Equal(133, back.Bpm);
        Assert.Equal("/tmp/x", back.LastFolder);
        Assert.Equal((3, 9, 1), (back.Midi.BassChannel, back.Midi.RhythmChannel, back.Midi.ProgramChangeChannel));
        Assert.Equal(48, back.Midi.RhythmNotes[RhythmTrack.HT]);
        Assert.Contains("\"SyncT8Sequencer\"", File.ReadAllText(store.Path));
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        using var temp = TestPaths.CreateTempFolder();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "{ not json");
        Assert.Equal(2, new JsonSettingsStore(path).Load().Midi.BassChannel);
    }

    [Fact]
    public void Invalid_midi_settings_are_reset()
    {
        using var temp = TestPaths.CreateTempFolder();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, """{ "Bpm": 99, "Midi": { "BassChannel": 40, "RhythmChannel": 10, "ProgramChangeChannel": 16 } }""");

        var settings = new JsonSettingsStore(path).Load();

        Assert.Equal(99, settings.Bpm);
        Assert.Equal(2, settings.Midi.BassChannel);
    }

    [Fact]
    public void Default_path_is_in_app_data() =>
        Assert.EndsWith(Path.Combine("T8Editor", "settings.json"), new JsonSettingsStore().Path);

    [Fact]
    public void Clone_is_deep()
    {
        var settings = new EditorSettings();
        var copy = settings.Clone();
        copy.Midi.BassChannel = 7;
        Assert.Equal(2, settings.Midi.BassChannel);
    }
}

public class MidiSettingsViewModelTests
{
    private readonly FakePortProvider _ports = FakePortProvider.WithT8();

    private MidiSettingsViewModel Create(EditorSettings? settings = null) => new(_ports, settings ?? new EditorSettings());

    [Fact]
    public void Starts_from_the_current_settings()
    {
        var settings = new EditorSettings { OutputPortName = "IAC Bus 1", PlayMode = PlayMode.SyncT8Sequencer };
        settings.Midi.BassChannel = 5;

        var vm = Create(settings);

        Assert.Equal("IAC Bus 1", vm.SelectedPort?.Name);
        Assert.Equal(5, vm.BassChannel);
        Assert.Equal(PlayMode.SyncT8Sequencer, vm.PlayMode);
        Assert.Equal(6, vm.DrumNotes.Count);
        Assert.Equal(16, MidiSettingsViewModel.Channels.Count);
    }

    [Fact]
    public void Picks_the_t8_when_no_port_is_saved() => Assert.Equal("T-8", Create().SelectedPort?.Name);

    [Fact]
    public void Edits_become_new_settings_without_touching_the_original()
    {
        var original = new EditorSettings { Bpm = 140 };
        var vm = Create(original);
        vm.BassChannel = 1;
        vm.RhythmChannel = 11;
        vm.ProgramChangeChannel = 15;
        vm.DrumNotes.Single(d => d.Track == RhythmTrack.BD).Note = 35;
        vm.PlayMode = PlayMode.SyncT8Sequencer;
        vm.FollowSelection = true;
        vm.SelectedPort = vm.Ports[0];

        var settings = vm.ToSettings();

        Assert.Equal((1, 11, 15), (settings.Midi.BassChannel, settings.Midi.RhythmChannel, settings.Midi.ProgramChangeChannel));
        Assert.Equal(35, settings.Midi.RhythmNotes[RhythmTrack.BD]);
        Assert.Equal(PlayMode.SyncT8Sequencer, settings.PlayMode);
        Assert.True(settings.FollowSelection);
        Assert.Equal("IAC Bus 1", settings.OutputPortName);
        Assert.Equal(140, settings.Bpm);
        Assert.Equal(2, original.Midi.BassChannel);
    }

    [Fact]
    public void Same_bass_and_rhythm_channel_is_invalid()
    {
        var vm = Create();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.BassChannel = 10;

        Assert.False(vm.IsValid);
        Assert.Contains("Bass and rhythm need different channels.", vm.Errors);
        Assert.Contains(nameof(vm.IsValid), changed);
    }

    [Fact]
    public void Drum_notes_are_clamped_and_revalidate()
    {
        var vm = Create();
        var raised = false;
        vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(vm.Errors);

        var snare = vm.DrumNotes.Single(d => d.Track == RhythmTrack.SD);
        snare.Note = 300;

        Assert.Equal(127, snare.Note);
        Assert.True(raised);
        Assert.Equal("Snare drum", snare.Name);
    }

    [Fact]
    public void Reset_restores_t8_defaults()
    {
        var vm = Create();
        vm.BassChannel = 7;
        vm.DrumNotes[0].Note = 1;

        vm.ResetToT8DefaultsCommand.Execute(null);

        Assert.Equal((2, 10, 16), (vm.BassChannel, vm.RhythmChannel, vm.ProgramChangeChannel));
        Assert.Equal(36, vm.DrumNotes[0].Note);
    }

    [Fact]
    public void Setup_help_names_the_t8_menu_items_and_follows_the_channels()
    {
        var vm = Create();
        vm.RhythmChannel = 9;
        Assert.Contains("bACh to 2", vm.T8SetupHelp);
        Assert.Contains("rhCh to 9", vm.T8SetupHelp);
        Assert.Contains("Pc.Ch to 16", vm.T8SetupHelp);
        Assert.Contains("rXPc", vm.T8SetupHelp);
    }

    [Fact]
    public async Task Test_connection_reports_success()
    {
        var vm = Create();
        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.True(vm.TestPassed);
        Assert.Contains("the T-8 is answering", vm.TestResult);
        Assert.False(vm.IsTesting);
    }

    [Fact]
    public async Task Test_connection_uses_the_unsaved_channels()
    {
        var vm = Create();
        vm.RhythmChannel = 12;
        await vm.TestConnectionCommand.ExecuteAsync(null);

        var drums = _ports.LastOpened!.Events.OfType<Melanchall.DryWetMidi.Core.NoteOnEvent>().Take(4);
        Assert.All(drums, n => Assert.Equal(11, (int)n.Channel));
    }

    [Fact]
    public async Task Test_connection_reports_a_silent_t8()
    {
        _ports.InputsRespond = false;
        var vm = Create();
        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.False(vm.TestPassed);
        Assert.Contains("nothing came back", vm.TestResult);
    }

    [Fact]
    public async Task Test_connection_without_a_port_explains()
    {
        var vm = new MidiSettingsViewModel(new FakePortProvider(), new EditorSettings());
        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.False(vm.TestPassed);
        Assert.Contains("Pick a MIDI output first", vm.TestResult);
    }

    [Fact]
    public async Task Detect_fills_in_the_t8_channels()
    {
        _ports.InputEvents.Add(new Melanchall.DryWetMidi.Core.ProgramChangeEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)1)
        {
            Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)6,
        });
        var vm = new MidiSettingsViewModel(_ports, new EditorSettings()) { DetectTime = TimeSpan.FromMilliseconds(100) };

        await vm.DetectChannelsCommand.ExecuteAsync(null);

        Assert.Equal((2, 10, 7), (vm.BassChannel, vm.RhythmChannel, vm.ProgramChangeChannel));
        Assert.Contains("Detected pattern select 7.", vm.DetectResult);
        Assert.Contains("Press Save", vm.DetectResult);
        Assert.False(vm.IsDetecting);
    }

    [Fact]
    public async Task Detect_without_a_port_explains()
    {
        var vm = new MidiSettingsViewModel(new FakePortProvider(), new EditorSettings());
        await vm.DetectChannelsCommand.ExecuteAsync(null);
        Assert.Contains("Pick a MIDI output first", vm.DetectResult);
    }

    [Fact]
    public void Refresh_keeps_the_choice()
    {
        var vm = Create();
        vm.SelectedPort = vm.Ports[0];
        vm.RefreshPortsCommand.Execute(null);
        Assert.Equal("IAC Bus 1", vm.SelectedPort?.Name);
    }

    [Fact]
    public void A_fresh_settings_object_has_the_switches_on_that_the_app_turns_on_by_default()
    {
        var fresh = new EditorSettings();
        Assert.True(fresh.FollowSelection);
        Assert.True(fresh.FollowT8);
        Assert.True(fresh.AuditionEnabled);
        Assert.True(fresh.ShowPlayhead);
        Assert.False(fresh.ShowBassColours);
        Assert.True(fresh.CheckForUpdatesOnStartup);
        Assert.Equal(PlayMode.PreviewNotes, fresh.PlayMode);

        // First run (no settings file) and a file missing these keys give the same defaults.
        var path = Path.Combine(Path.GetTempPath(), $"t8-settings-{Guid.NewGuid():N}.json");
        Assert.True(new JsonSettingsStore(path).Load().FollowSelection);
        File.WriteAllText(path, "{}");
        try
        {
            var loaded = new JsonSettingsStore(path).Load();
            Assert.True(loaded.FollowSelection);
            Assert.True(loaded.CheckForUpdatesOnStartup);
            Assert.False(loaded.ShowBassColours);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_readme_screenshots_start_from_the_real_default_switches()
    {
        var fresh = new EditorSettings();
        var shots = DocsScreenshots.ScreenshotSettings();
        foreach (var property in typeof(EditorSettings).GetProperties().Where(p => p.PropertyType == typeof(bool) || p.PropertyType == typeof(PlayMode)))
            Assert.True(Equals(property.GetValue(fresh), property.GetValue(shots)), $"{property.Name} differs from its default in the screenshots");
    }
}
