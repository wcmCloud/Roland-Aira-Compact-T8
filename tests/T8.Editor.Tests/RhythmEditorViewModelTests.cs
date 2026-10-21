using T8.Core.Patterns;
using T8.Editor.ViewModels;

namespace T8.Editor.Tests;

public class RhythmEditorViewModelTests
{
    private int _changes;
    private readonly List<RhythmTrack> _auditioned = new();
    private readonly RhythmPattern _pattern = new() { Length = 16 };

    private RhythmEditorViewModel Create() => new(_pattern, () => _changes++, _auditioned.Add);

    [Fact]
    public void Grid_shows_the_7_tracks_with_a_sound_in_file_order()
    {
        var vm = Create();
        Assert.Equal(Enum.GetValues<RhythmTrack>().Where(t => t != RhythmTrack.CY), vm.Tracks.Select(t => t.Track));
        Assert.All(vm.Tracks, t => Assert.Equal(32, t.Cells.Count));
        Assert.Equal("Tom", vm[RhythmTrack.LT].Name);
        Assert.Equal("Clap", vm[RhythmTrack.HT].Name);
    }

    [Fact]
    public void Turning_a_cell_on_writes_the_model_with_default_velocity()
    {
        var cell = Create()[RhythmTrack.BD].Cells[4];
        cell.On = true;

        Assert.True(_pattern.Steps[4][RhythmTrack.BD].On);
        Assert.Equal(7, cell.Velocity);
        Assert.Equal("170AA", _pattern.Steps[4][RhythmTrack.BD].ToString());
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void Turning_a_cell_on_keeps_an_existing_velocity()
    {
        _pattern.Steps[0][RhythmTrack.SD].Velocity = 3;
        var cell = Create()[RhythmTrack.SD].Cells[0];
        cell.On = true;
        Assert.Equal(3, cell.Velocity);
    }

    [Fact]
    public void Activating_a_cell_selects_it_and_auditions_playable_tracks()
    {
        var vm = Create();
        var snare = vm[RhythmTrack.SD].Cells[2];
        var accent = vm[RhythmTrack.AC].Cells[2];
        snare.On = true;
        accent.On = true;

        snare.ActivateCommand.Execute(null);
        Assert.Same(snare, vm.SelectedCell);
        Assert.True(snare.IsSelected);

        accent.ActivateCommand.Execute(null);
        Assert.False(snare.IsSelected);
        Assert.Same(accent, vm.SelectedCell);
        Assert.Equal([RhythmTrack.SD], _auditioned);
    }

    [Fact]
    public void Activating_an_off_cell_does_not_audition()
    {
        var vm = Create();
        vm[RhythmTrack.CH].Cells[0].ActivateCommand.Execute(null);
        Assert.Empty(_auditioned);
    }

    [Fact]
    public void Inspector_values_write_through()
    {
        var cell = Create()[RhythmTrack.CH].Cells[1];
        cell.On = true;
        cell.Velocity = 4;
        cell.ProbabilityPercent = 50;
        cell.SubStep = SubStep.Third;
        cell.SubStepProbabilityPercent = 30;

        Assert.Equal("14253", _pattern.Steps[1][RhythmTrack.CH].ToString());
        Assert.True(cell.HasSubStep);
    }

    [Theory]
    [InlineData(44, 40)]
    [InlineData(45, 50)]
    [InlineData(150, 100)]
    [InlineData(-5, 0)]
    public void Probability_rounds_to_the_t8s_10_percent_steps(int requested, int expected)
    {
        var cell = Create()[RhythmTrack.BD].Cells[0];
        cell.ProbabilityPercent = requested;
        Assert.Equal(expected, cell.ProbabilityPercent);
    }

    [Fact]
    public void Velocity_is_clamped()
    {
        var cell = Create()[RhythmTrack.BD].Cells[0];
        cell.Velocity = 20;
        Assert.Equal(10, cell.Velocity);
    }

    [Fact]
    public void Pattern_settings_write_through_and_clamp()
    {
        var vm = Create();
        vm.Length = 12;
        vm.Shuffle = 500;
        vm.Flam = 20;

        Assert.Equal(12, _pattern.Length);
        Assert.Equal(200, _pattern.Shuffle);
        Assert.Equal(20, _pattern.Flam);
        Assert.False(vm[RhythmTrack.BD].Cells[12].IsInLength);
        Assert.True(vm[RhythmTrack.BD].Cells[11].IsInLength);
        Assert.Equal(3, _changes);
    }

    [Fact]
    public void Clear_track_turns_all_its_steps_off()
    {
        foreach (var step in _pattern.Steps)
        {
            step[RhythmTrack.OH].On = true;
            step[RhythmTrack.BD].On = true;
        }
        var vm = Create();

        vm.ClearTrackCommand.Execute(vm[RhythmTrack.OH]);

        Assert.All(_pattern.Steps, s => Assert.False(s[RhythmTrack.OH].On));
        Assert.All(_pattern.Steps, s => Assert.True(s[RhythmTrack.BD].On));
    }

    [Fact]
    public void Sub_step_options_list_every_mode() =>
        Assert.Equal([SubStep.Off, SubStep.Half, SubStep.Third, SubStep.Quarter, SubStep.Flam], RhythmEditorViewModel.SubStepOptions);

    [Fact]
    public void Playing_step_lights_the_whole_column()
    {
        var vm = Create();
        vm.Length = 16;
        vm.ShowPlayingStep(17);

        Assert.Equal(2, vm.PlayingStepNumber);
        Assert.All(vm.Tracks, t => Assert.True(t.Cells[1].IsPlaying));
        Assert.Equal(vm.Tracks.Count, vm.Tracks.SelectMany(t => t.Cells).Count(c => c.IsPlaying));

        vm.ShowPlayingStep(null);
        Assert.Null(vm.PlayingStepNumber);
        Assert.DoesNotContain(vm.Tracks.SelectMany(t => t.Cells), c => c.IsPlaying);
    }

    [Fact]
    public void Cy_is_hidden_but_its_steps_are_kept()
    {
        _pattern.Steps[3][RhythmTrack.CY].On = true;
        var vm = Create();

        Assert.DoesNotContain(vm.Tracks, t => t.Track == RhythmTrack.CY);
        Assert.Equal(7, vm.Tracks.Count);
        Assert.True(vm[RhythmTrack.CY].Cells[3].On);
        Assert.Equal(RhythmTrack.OH, vm[RhythmTrack.OH].Track);
    }

    [Fact]
    public void Only_active_cells_can_be_multi_selected_and_picking_twice_unpicks()
    {
        var vm = Create();
        var on = vm[RhythmTrack.BD].Cells[0];
        on.On = true;
        on.Velocity = 4;
        var off = vm[RhythmTrack.SD].Cells[0];

        vm.ToggleMultiSelect(off);
        Assert.False(vm.HasMultiSelection);

        vm.ToggleMultiSelect(on);
        Assert.True(on.IsMultiSelected);
        Assert.Equal("1 step selected", vm.MultiSelectionLabel);
        Assert.Equal(4, vm.BatchVelocity); // starts from the first picked step

        vm.ToggleMultiSelect(on);
        Assert.False(on.IsMultiSelected);
        Assert.False(vm.HasMultiSelection);
    }

    [Fact]
    public void Apply_sets_velocity_probability_and_sub_step_on_every_selected_step_then_clears()
    {
        var vm = Create();
        var cells = new[] { vm[RhythmTrack.BD].Cells[0], vm[RhythmTrack.CH].Cells[2], vm[RhythmTrack.OH].Cells[3] };
        foreach (var cell in cells)
        {
            cell.On = true;
            vm.ToggleMultiSelect(cell);
        }
        var untouched = vm[RhythmTrack.SD].Cells[4];
        untouched.On = true;
        _changes = 0;

        vm.BatchVelocity = 9;
        vm.BatchProbabilityPercent = 60;
        vm.BatchSubStep = SubStep.Third;
        vm.BatchSubStepProbabilityPercent = 50;
        Assert.Equal("3 steps selected", vm.MultiSelectionLabel);
        vm.ApplyToSelectionCommand.Execute(null);

        Assert.All(cells, c =>
        {
            Assert.Equal(9, c.Velocity);
            Assert.Equal(60, c.ProbabilityPercent);
            Assert.Equal(SubStep.Third, c.SubStep);
            Assert.Equal(50, c.SubStepProbabilityPercent);
            Assert.False(c.IsMultiSelected);
        });
        Assert.Equal("19265", _pattern.Steps[2][RhythmTrack.CH].ToString());
        Assert.Equal(RhythmCell.DefaultNoteVelocity, untouched.Velocity);
        Assert.False(vm.HasMultiSelection);
        Assert.True(_changes > 0);
    }

    [Fact]
    public void Select_all_active_picks_every_active_step_in_the_length_and_clear_drops_them()
    {
        var vm = Create();
        vm.Length = 8;
        vm[RhythmTrack.BD].Cells[0].On = true;
        vm[RhythmTrack.CH].Cells[5].On = true;
        vm[RhythmTrack.CH].Cells[12].On = true; // past the last step

        vm.SelectAllActiveCommand.Execute(null);
        Assert.Equal(2, vm.MultiSelection.Count);

        vm.ClearMultiSelectionCommand.Execute(null);
        Assert.False(vm.HasMultiSelection);
        Assert.DoesNotContain(vm.Tracks.SelectMany(t => t.Cells), c => c.IsMultiSelected);
    }

    [Fact]
    public void Switching_a_selected_step_off_drops_it_from_the_selection()
    {
        var vm = Create();
        var cell = vm[RhythmTrack.BD].Cells[0];
        cell.On = true;
        vm.ToggleMultiSelect(cell);
        cell.On = false;
        Assert.False(vm.HasMultiSelection);
        Assert.False(cell.IsMultiSelected);
    }

    [Fact]
    public void Clicking_a_row_name_selects_it_and_clicking_again_deselects()
    {
        var vm = Create();
        var hats = vm[RhythmTrack.CH];

        vm.SelectTrackCommand.Execute(hats);
        Assert.Same(hats, vm.SelectedTrack);
        Assert.True(hats.IsSelected);

        vm.SelectTrackCommand.Execute(vm[RhythmTrack.SD]);
        Assert.False(hats.IsSelected);

        vm.SelectTrackCommand.Execute(vm[RhythmTrack.SD]);
        Assert.Null(vm.SelectedTrack);
    }

    [Fact]
    public void Copy_and_paste_a_row_copies_every_step_value()
    {
        var vm = Create();
        var source = vm[RhythmTrack.CH];
        source.Cells[0].On = true;
        source.Cells[0].Velocity = 9;
        source.Cells[0].SubStep = SubStep.Third;
        source.Cells[0].ProbabilityPercent = 60;
        source.Cells[2].On = true;
        var target = vm[RhythmTrack.OH];
        target.Cells[1].On = true; // replaced by the paste
        Assert.False(vm.Clipboard.HasContent);

        vm.CopyTrackCommand.Execute(source);
        Assert.True(vm.Clipboard.HasContent);
        Assert.Contains("Closed hat", vm.Clipboard.PasteTip);
        _changes = 0;
        vm.PasteTrackCommand.Execute(target);

        Assert.Equal(source.Cells.Select(c => c.Cell.ToString()), target.Cells.Select(c => c.Cell.ToString()));
        Assert.False(target.Cells[1].On);
        Assert.Same(target, vm.SelectedTrack);
        Assert.True(_changes > 0);

        source.Cells[0].Velocity = 2; // the copy is a snapshot
        Assert.Equal(9, target.Cells[0].Velocity);
    }

    [Fact]
    public void Keyboard_copy_and_paste_use_the_selected_row()
    {
        var vm = Create();
        vm[RhythmTrack.BD].Cells[4].On = true;
        vm.PasteToSelectedTrackCommand.Execute(null); // nothing selected, nothing copied: no effect

        vm.SelectTrackCommand.Execute(vm[RhythmTrack.BD]);
        vm.CopySelectedTrackCommand.Execute(null);
        vm.SelectTrackCommand.Execute(vm[RhythmTrack.LT]);
        vm.PasteToSelectedTrackCommand.Execute(null);

        Assert.True(vm[RhythmTrack.LT].Cells[4].On);
    }

    [Fact]
    public void The_row_clipboard_is_shared_between_patterns()
    {
        var clipboard = new RhythmTrackClipboard();
        var first = new RhythmEditorViewModel(new RhythmPattern(), () => { }, clipboard: clipboard);
        first[RhythmTrack.SD].Cells[3].On = true;
        first.CopyTrackCommand.Execute(first[RhythmTrack.SD]);

        var other = new RhythmEditorViewModel(new RhythmPattern(), () => { }, clipboard: clipboard);
        other.PasteTrackCommand.Execute(other[RhythmTrack.HT]);

        Assert.True(other[RhythmTrack.HT].Cells[3].On);
    }
}
