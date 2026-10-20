using Melanchall.DryWetMidi.Core;
using T8.Core.Patterns;
using T8.Editor.ViewModels;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Editor.Tests;

public class MainWindowViewModelTests : IDisposable
{
    private readonly FakePortProvider _ports = FakePortProvider.WithT8();
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _vm;

    public MainWindowViewModelTests() => _vm = new MainWindowViewModel(_ports, _dialogs);

    public void Dispose() => _vm.Dispose();

    [Fact]
    public void Starts_with_a_blank_untitled_library()
    {
        Assert.Null(_vm.FolderPath);
        Assert.False(_vm.IsDirty);
        Assert.Equal("Roland Aira Compact T-8 Editor - Untitled", _vm.Title);
        Assert.Equal(16, _vm.Rhythm.Length);
        Assert.Equal([1, 2, 3, 4], _vm.BankNumbers);
        Assert.Equal(16, _vm.PatternNumbers.Count);
    }

    [Fact]
    public void Picks_the_t8_port_automatically()
    {
        Assert.Equal(2, _vm.Ports.Count);
        Assert.Equal("T-8", _vm.SelectedPort?.Name);
    }

    [Fact]
    public void Opens_a_backup_folder()
    {
        Assert.True(_vm.OpenFolder(TestPaths.Backup));

        Assert.Equal(TestPaths.Backup, _vm.FolderPath);
        Assert.Equal(12, _vm.Bass.Length);
        Assert.Equal(32, _vm.Rhythm.Length);
        Assert.Contains("Loaded 64 patterns", _vm.Status);
        Assert.Equal("Roland Aira Compact T-8 Editor - BACKUP", _vm.Title);
    }

    [Fact]
    public void Opens_the_t8_drive_root_through_its_backup_folder()
    {
        Assert.True(_vm.OpenFolder(Path.GetDirectoryName(TestPaths.Backup)!));
        Assert.Equal(TestPaths.Backup, _vm.FolderPath);
    }

    [Fact]
    public void Rejects_a_folder_without_patterns()
    {
        using var temp = TestPaths.CreateTempFolder();
        Assert.False(_vm.OpenFolder(temp.Path));
        Assert.Null(_vm.FolderPath);
        Assert.Contains("No BASS and RHYTHM folders", _vm.Status);
    }

    [Fact]
    public void Reports_a_broken_pattern_file()
    {
        using var temp = TestPaths.CreateTempFolder();
        PatternLibrary.CreateBlank().Save(temp.Path);
        File.WriteAllText(PatternLibrary.BassPath(temp.Path, 1, 1), "garbage\n");

        Assert.False(_vm.OpenFolder(temp.Path));
        Assert.Contains("Couldn't load", _vm.Status);
    }

    [Fact]
    public async Task Open_command_uses_the_folder_picker()
    {
        _dialogs.Folders.Enqueue(TestPaths.Backup);
        await _vm.OpenFolderCommand.ExecuteAsync(null);
        Assert.Equal(TestPaths.Backup, _vm.FolderPath);
    }

    [Fact]
    public async Task Cancelled_picker_changes_nothing()
    {
        await _vm.OpenFolderCommand.ExecuteAsync(null);
        Assert.Null(_vm.FolderPath);
    }

    [Fact]
    public void Switching_pattern_loads_that_pattern()
    {
        _vm.OpenFolder(TestPaths.Backup);
        _vm.SelectedBank = 2;
        _vm.SelectedPattern = 3;

        Assert.Equal("Bank 2 · Pattern 3", _vm.PatternLabel);
        Assert.Same(_vm.Library.Bass[1, 2], _vm.CurrentBass);
        Assert.Equal(_vm.CurrentBass.Length, _vm.Bass.Length);
        Assert.Equal(_vm.CurrentRhythm.Length, _vm.Rhythm.Length);
    }

    [Fact]
    public void Edits_mark_the_library_dirty()
    {
        _vm.Rhythm[RhythmTrack.BD].Cells[0].On = true;
        Assert.True(_vm.IsDirty);
        Assert.EndsWith("*", _vm.Title);

        _vm.Bass.Steps[0].Accent = true;
        Assert.True(_vm.CurrentBass.Steps[0].Accent);
    }

    [Fact]
    public async Task Save_writes_back_to_the_opened_folder()
    {
        using var temp = TestPaths.CreateTempFolder();
        PatternLibrary.CreateBlank().Save(temp.Path);
        _vm.OpenFolder(temp.Path);
        _vm.SelectedBank = 3;
        _vm.Bass.Steps[0].CycleStateCommand.Execute(null);

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.False(_vm.IsDirty);
        Assert.Equal(BassStepState.Note, BassPattern.Load(PatternLibrary.BassPath(temp.Path, 3, 1)).Steps[0].State);
    }

    [Fact]
    public async Task Save_on_an_untitled_library_asks_for_a_folder()
    {
        using var temp = TestPaths.CreateTempFolder();
        _dialogs.Folders.Enqueue(temp.Path);

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(temp.Path, _vm.FolderPath);
        Assert.True(File.Exists(PatternLibrary.RhythmPath(temp.Path, 4, 16)));
    }

    [Fact]
    public async Task Save_as_writes_a_copy_and_switches_to_it()
    {
        _vm.OpenFolder(TestPaths.Backup);
        using var temp = TestPaths.CreateTempFolder();
        _dialogs.Folders.Enqueue(temp.Path);

        await _vm.SaveAsCommand.ExecuteAsync(null);

        Assert.Equal(temp.Path, _vm.FolderPath);
        Assert.Equal(
            File.ReadAllText(PatternLibrary.RhythmPath(TestPaths.Backup, 1, 1)),
            File.ReadAllText(PatternLibrary.RhythmPath(temp.Path, 1, 1)));
    }

    [Fact]
    public void Save_failure_is_reported()
    {
        using var temp = TestPaths.CreateTempFolder();
        var blocked = Path.Combine(temp.Path, "file");
        File.WriteAllText(blocked, "");

        Assert.False(_vm.SaveTo(blocked));
        Assert.Contains("Couldn't save", _vm.Status);
    }

    [Fact]
    public async Task Unsaved_changes_ask_before_being_discarded()
    {
        _vm.OpenFolder(TestPaths.Backup);
        _vm.Rhythm.Length = 4;
        _dialogs.ConfirmResult = false;

        await _vm.NewLibraryCommand.ExecuteAsync(null);
        Assert.Equal(1, _dialogs.ConfirmCount);
        Assert.Equal(TestPaths.Backup, _vm.FolderPath);

        _dialogs.ConfirmResult = true;
        await _vm.NewLibraryCommand.ExecuteAsync(null);
        Assert.Null(_vm.FolderPath);
        Assert.False(_vm.IsDirty);
    }

    [Fact]
    public async Task Clean_library_opens_without_asking()
    {
        _dialogs.Folders.Enqueue(TestPaths.Backup);
        await _vm.OpenFolderCommand.ExecuteAsync(null);
        Assert.Equal(0, _dialogs.ConfirmCount);
    }

    [Fact]
    public void Copy_and_paste_duplicates_a_pattern()
    {
        _vm.OpenFolder(TestPaths.Backup);
        Assert.False(_vm.PastePatternCommand.CanExecute(null));

        _vm.CopyPatternCommand.Execute(null);
        Assert.True(_vm.CanPaste);
        _vm.SelectedBank = 4;
        _vm.SelectedPattern = 16;
        _vm.PastePatternCommand.Execute(null);

        Assert.Equal(_vm.Library.Bass[0, 0].ToPrm().ToString(), _vm.Library.Bass[3, 15].ToPrm().ToString());
        Assert.Equal(_vm.Library.Rhythm[0, 0].ToPrm().ToString(), _vm.Library.Rhythm[3, 15].ToPrm().ToString());
        Assert.NotSame(_vm.Library.Bass[0, 0], _vm.Library.Bass[3, 15]);
        Assert.True(_vm.IsDirty);
        Assert.Equal(12, _vm.Bass.Length);
    }

    [Fact]
    public void Clear_blanks_the_current_pattern_only()
    {
        _vm.OpenFolder(TestPaths.Backup);
        _vm.ClearPatternCommand.Execute(null);

        Assert.Equal(new RhythmPattern().ToPrm().ToString(), _vm.Library.Rhythm[0, 0].ToPrm().ToString());
        Assert.NotEqual(new RhythmPattern().ToPrm().ToString(), _vm.Library.Rhythm[0, 1].ToPrm().ToString());
        Assert.Equal(16, _vm.Rhythm.Length);
        Assert.True(_vm.IsDirty);
    }

    [Fact]
    public void Select_on_t8_sends_program_change()
    {
        _vm.SelectedBank = 2;
        _vm.SelectedPattern = 4;
        _vm.SelectOnT8Command.Execute(null);

        var pc = Assert.IsType<ProgramChangeEvent>(Assert.Single(_ports.LastOpened!.Events));
        Assert.Equal(19, (int)pc.ProgramNumber);
    }

    [Fact]
    public void Play_and_stop_preview()
    {
        _vm.OpenFolder(TestPaths.Backup);
        _vm.PlayCommand.Execute(null);
        Assert.True(_vm.IsPlaying);
        Assert.False(_vm.PlayCommand.CanExecute(null));

        _vm.StopCommand.Execute(null);
        Assert.False(_vm.IsPlaying);
        Assert.True(_vm.PlayCommand.CanExecute(null));
        Assert.Contains(_ports.LastOpened!.Events, e => e is ControlChangeEvent c && c.ControlNumber == 123);
    }

    [Fact]
    public void Playhead_follows_playback_and_can_be_turned_off()
    {
        _vm.OpenFolder(TestPaths.Backup);
        _vm.PlayCommand.Execute(null);
        Thread.Sleep(50);

        _vm.UpdatePlayhead();
        Assert.NotNull(_vm.Bass.PlayingStep);
        Assert.NotNull(_vm.Rhythm.PlayingStepNumber);

        _vm.ShowPlayhead = false;
        Assert.Null(_vm.Bass.PlayingStep);
        Assert.False(_vm.Settings.ShowPlayhead);

        _vm.ShowPlayhead = true;
        _vm.StopCommand.Execute(null);
        Assert.Null(_vm.Bass.PlayingStep);
        Assert.Null(_vm.Rhythm.PlayingStepNumber);
    }

    [Fact]
    public void Generated_bass_marks_the_library_dirty_and_follows_the_pattern()
    {
        _vm.OpenFolder(TestPaths.Backup);
        Assert.Same(_vm.Bass, _vm.BassGenerator.Target);

        _vm.BassGenerator.Scale = BassGeneratorViewModel.Scales.Single(s => s.Value == Scale.Dorian);
        _vm.BassGenerator.GenerateCommand.Execute(null);

        Assert.True(_vm.IsDirty);
        Assert.Contains("dorian", _vm.Status);
        Assert.Equal(Scale.Dorian, _vm.Settings.BassGenerator.Scale);

        _vm.SelectedPattern = 2;
        Assert.Same(_vm.Bass, _vm.BassGenerator.Target);
    }

    [Fact]
    public void Generated_rhythm_marks_the_library_dirty_and_follows_the_pattern()
    {
        _vm.OpenFolder(TestPaths.Backup);
        Assert.Same(_vm.Rhythm, _vm.RhythmGenerator.Target);

        _vm.RhythmGenerator.Style = RhythmGeneratorViewModel.Styles.Single(s => s.Value == RhythmStyle.Electro);
        _vm.RhythmGenerator.GenerateCommand.Execute(null);

        Assert.True(_vm.IsDirty);
        Assert.Contains("Electro drums", _vm.Status);
        Assert.Equal(RhythmStyle.Electro, _vm.Settings.RhythmGenerator.Style);

        _vm.SelectedBank = 2;
        Assert.Same(_vm.Rhythm, _vm.RhythmGenerator.Target);
    }

    [Fact]
    public void Commands_without_a_port_explain_what_to_do()
    {
        using var vm = new MainWindowViewModel(new FakePortProvider(), _dialogs);
        vm.PlayCommand.Execute(null);

        Assert.False(vm.IsPlaying);
        Assert.Contains("No MIDI output selected", vm.Status);
    }

    [Fact]
    public void Changing_port_closes_the_old_one()
    {
        _vm.SelectOnT8Command.Execute(null);
        var first = _ports.LastOpened!;

        _vm.SelectedPort = _vm.Ports[0];

        Assert.True(first.IsDisposed);
    }

    [Fact]
    public void Refresh_keeps_the_selected_port()
    {
        _vm.SelectedPort = _vm.Ports[0];
        _vm.RefreshPortsCommand.Execute(null);
        Assert.Equal("IAC Bus 1", _vm.SelectedPort?.Name);
    }

    [Fact]
    public void Editing_auditions_drums_and_bass_on_the_t8()
    {
        var cell = _vm.Rhythm[RhythmTrack.SD].Cells[0];
        cell.On = true;
        cell.ActivateCommand.Execute(null);
        _vm.Bass.Steps[0].NoteUpCommand.Execute(null);

        var notes = _ports.LastOpened!.Events.OfType<NoteOnEvent>().Select(n => ((int)n.NoteNumber, (int)n.Channel)).ToList();
        Assert.Contains((38, 9), notes);
        Assert.Contains((37, 1), notes);
    }

    [Fact]
    public void Audition_can_be_turned_off()
    {
        _vm.AuditionEnabled = false;
        var cell = _vm.Rhythm[RhythmTrack.SD].Cells[0];
        cell.On = true;
        cell.ActivateCommand.Execute(null);

        Assert.Empty(_ports.Opened);
    }

    [Fact]
    public void Dispose_closes_the_port()
    {
        _vm.SelectOnT8Command.Execute(null);
        _vm.Dispose();
        Assert.True(_ports.LastOpened!.IsDisposed);
    }
}
