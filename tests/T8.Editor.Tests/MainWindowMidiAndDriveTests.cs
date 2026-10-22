using Melanchall.DryWetMidi.Core;
using T8.Core.Patterns;
using T8.Editor.Services;
using T8.Editor.ViewModels;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class MainWindowMidiAndDriveTests : IDisposable
{
    private readonly FakePortProvider _ports = FakePortProvider.WithT8();
    private readonly FakeDialogService _dialogs = new();
    private readonly MemorySettingsStore _store = new();
    private readonly TempFolder _temp = TestPaths.CreateTempFolder();
    private readonly List<string> _driveCandidates = new();
    private MainWindowViewModel? _vm;

    private MainWindowViewModel Vm => _vm ??= new MainWindowViewModel(_ports, _dialogs, _store, () => _driveCandidates);

    public void Dispose()
    {
        _vm?.Dispose();
        _temp.Dispose();
    }

    private string MountT8()
    {
        var drive = Path.Combine(_temp.Path, "T-8");
        PatternLibrary.Load(TestPaths.Backup).Save(Path.Combine(drive, "BACKUP"));
        Directory.CreateDirectory(Path.Combine(drive, "RESTORE"));
        _driveCandidates.Add(drive);
        return drive;
    }

    // ---- Settings ----

    [Fact]
    public void Restores_saved_settings_on_start()
    {
        _store.Stored = new EditorSettings { Bpm = 97, AuditionEnabled = false, OutputPortName = "IAC Bus 1" };

        Assert.Equal(97, Vm.Bpm);
        Assert.False(Vm.AuditionEnabled);
        Assert.Equal("IAC Bus 1", Vm.SelectedPort?.Name);
    }

    [Fact]
    public void Remembers_port_tempo_audition_and_folder()
    {
        Vm.SelectedPort = Vm.Ports[0];
        Vm.Bpm = 128;
        Vm.AuditionEnabled = false;
        Vm.OpenFolder(TestPaths.Backup);

        Assert.Equal("IAC Bus 1", _store.Stored.OutputPortName);
        Assert.Equal(128, _store.Stored.Bpm);
        Assert.False(_store.Stored.AuditionEnabled);
        Assert.Equal(TestPaths.Backup, _store.Stored.LastFolder);
    }

    [Fact]
    public async Task Saved_midi_settings_change_the_channels_used()
    {
        _dialogs.OnMidiSettings = settings =>
        {
            settings.RhythmChannel = 11;
            settings.ProgramChangeChannel = 1;
            return true;
        };

        await Vm.OpenMidiSettingsCommand.ExecuteAsync(null);
        Assert.Contains("MIDI settings saved", Vm.Status);
        Vm.SelectOnT8Command.Execute(null);
        var snare = Vm.Rhythm[RhythmTrack.SD].Cells[0];
        snare.On = true;
        snare.ActivateCommand.Execute(null);

        var events = _ports.LastOpened!.Events;
        Assert.Equal(0, (int)((ProgramChangeEvent)events[0]).Channel);
        Assert.Equal(10, (int)events.OfType<NoteOnEvent>().First().Channel);
        Assert.Equal(11, _store.Stored.Midi.RhythmChannel);
        Assert.Contains("Rhythm ch 11", Vm.MidiSummary);
    }

    [Fact]
    public async Task Cancelled_midi_settings_change_nothing()
    {
        _dialogs.OnMidiSettings = settings =>
        {
            settings.BassChannel = 5;
            return false;
        };

        await Vm.OpenMidiSettingsCommand.ExecuteAsync(null);

        Assert.Equal(2, Vm.Settings.Midi.BassChannel);
    }

    [Fact]
    public async Task Saving_settings_reopens_the_port_with_the_new_map()
    {
        Vm.SelectOnT8Command.Execute(null);
        var first = _ports.LastOpened!;
        _dialogs.OnMidiSettings = _ => true;

        await Vm.OpenMidiSettingsCommand.ExecuteAsync(null);

        Assert.True(first.IsDisposed);
    }

    // ---- Connection test ----

    [Fact]
    public async Task Test_connection_reports_in_the_status_bar()
    {
        await Vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.Contains("the T-8 is answering", Vm.Status);
        Assert.False(Vm.IsTestingConnection);
        Assert.Contains(_ports.LastOpened!.Events, e => e is NoteOnEvent n && n.NoteNumber == 36);
    }

    [Fact]
    public async Task Test_connection_without_a_port_explains()
    {
        using var vm = new MainWindowViewModel(new FakePortProvider(), _dialogs);
        await vm.TestConnectionCommand.ExecuteAsync(null);
        Assert.Contains("No MIDI output selected", vm.Status);
    }

    // ---- Play modes ----

    [Fact]
    public void Sync_mode_selects_the_pattern_and_runs_the_t8_clock()
    {
        _store.Stored = new EditorSettings { PlayMode = PlayMode.SyncT8Sequencer };
        Vm.SelectedBank = 2;

        Vm.PlayCommand.Execute(null);
        Thread.Sleep(100);
        Vm.SelectedPattern = 3;
        Vm.StopCommand.Execute(null);

        var events = _ports.LastOpened!.Events;
        Assert.Equal(16, (int)((ProgramChangeEvent)events[0]).ProgramNumber);
        Assert.IsType<StartEvent>(events[1]);
        Assert.Contains(events, e => e is TimingClockEvent);
        Assert.Contains(events, e => e is ProgramChangeEvent p && p.ProgramNumber == 18);
        Assert.IsType<StopEvent>(events.Last(e => e is not ControlChangeEvent));
        Assert.DoesNotContain(events, e => e is NoteOnEvent);
    }

    [Fact]
    public void Follow_selection_switches_the_t8_when_picking_patterns()
    {
        _store.Stored = new EditorSettings { FollowSelection = true };
        Vm.SelectedPattern = 9;

        Assert.Equal(8, (int)((ProgramChangeEvent)_ports.LastOpened!.Events.Single()).ProgramNumber);
    }

    [Fact]
    public void Without_follow_selection_picking_patterns_sends_nothing()
    {
        Vm.SelectedPattern = 9;
        Assert.Empty(_ports.Opened);
    }

    // ---- T-8 drive ----

    [Fact]
    public async Task Load_from_t8_opens_the_drive_backup()
    {
        var drive = MountT8();
        await Vm.LoadFromT8Command.ExecuteAsync(null);

        Assert.Equal(Path.Combine(drive, "BACKUP"), Vm.FolderPath);
        Assert.Equal(12, Vm.Bass.Length);
    }

    [Fact]
    public async Task Load_from_t8_explains_when_the_drive_is_missing()
    {
        await Vm.LoadFromT8Command.ExecuteAsync(null);
        Assert.Contains("T-8 drive not found", Vm.Status);
        Assert.Contains("holding PLAY", Vm.Status);
    }

    [Fact]
    public async Task Write_to_t8_writes_restore_after_confirming()
    {
        var drive = MountT8();
        Vm.OpenFolder(TestPaths.Backup);
        Vm.Bass.Steps[0].OctaveUpCommand.Execute(null);

        await Vm.WriteToT8Command.ExecuteAsync(null);

        Assert.Equal("Write", _dialogs.LastConfirmText);
        Assert.Equal(58, BassPattern.Load(PatternLibrary.BassPath(Path.Combine(drive, "RESTORE"), 1, 1)).Steps[0].Note);
        Assert.Contains("press ACCENT", Vm.Status);
    }

    [Fact]
    public async Task Write_to_t8_does_nothing_when_cancelled()
    {
        var drive = MountT8();
        _dialogs.ConfirmResult = false;

        await Vm.WriteToT8Command.ExecuteAsync(null);

        Assert.False(File.Exists(PatternLibrary.BassPath(Path.Combine(drive, "RESTORE"), 1, 1)));
    }

    [Fact]
    public async Task Write_to_t8_explains_when_the_drive_is_missing()
    {
        await Vm.WriteToT8Command.ExecuteAsync(null);
        Assert.Contains("T-8 drive not found", Vm.Status);
        Assert.Equal(0, _dialogs.ConfirmCount);
    }

    [Fact]
    public void Load_and_write_are_disabled_until_the_t8_drive_is_found()
    {
        Assert.False(Vm.IsT8DriveConnected);
        Assert.False(Vm.LoadFromT8Command.CanExecute(null));
        Assert.False(Vm.WriteToT8Command.CanExecute(null));
        Assert.Contains("not connected", Vm.T8DriveStatus);

        var drive = MountT8();
        Vm.CheckT8DriveCommand.Execute(null);

        Assert.True(Vm.IsT8DriveConnected);
        Assert.Equal(drive, Vm.T8DrivePath);
        Assert.True(Vm.LoadFromT8Command.CanExecute(null));
        Assert.True(Vm.WriteToT8Command.CanExecute(null));
        Assert.Contains("T-8 drive connected", Vm.Status);
    }

    [Fact]
    public void A_drive_mounted_before_start_is_found_straight_away()
    {
        MountT8();
        Assert.True(Vm.IsT8DriveConnected);
        Assert.True(Vm.LoadFromT8Command.CanExecute(null));
    }

    [Fact]
    public void Check_explains_drive_mode_when_the_drive_is_missing()
    {
        Vm.CheckT8DriveCommand.Execute(null);
        Assert.Contains("T-8 drive not found", Vm.Status);
        Assert.Contains("holding PLAY", Vm.Status);
        Assert.Contains("hold PLAY", T8.Editor.Help.HelpContent.Tip(T8.Editor.Help.HelpTips.DriveMode)!.Markdown);
    }

    [Fact]
    public async Task An_ejected_drive_disables_load_and_write_again()
    {
        MountT8();
        Assert.True(Vm.IsT8DriveConnected);
        _driveCandidates.Clear();

        await Vm.WriteToT8Command.ExecuteAsync(null);

        Assert.False(Vm.IsT8DriveConnected);
        Assert.False(Vm.WriteToT8Command.CanExecute(null));
        Assert.Contains("T-8 drive not found", Vm.Status);
        Assert.Equal(0, _dialogs.ConfirmCount);
    }

    [Fact]
    public void Refreshing_finds_a_drive_without_a_message()
    {
        Vm.Status = "";
        MountT8();
        Vm.RefreshT8Drive();
        Assert.True(Vm.IsT8DriveConnected);
        Assert.Equal("", Vm.Status);
    }
}
