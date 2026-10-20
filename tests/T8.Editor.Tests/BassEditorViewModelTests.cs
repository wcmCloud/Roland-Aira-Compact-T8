using T8.Core.Patterns;
using T8.Editor.ViewModels;

namespace T8.Editor.Tests;

public class BassEditorViewModelTests
{
    private int _changes;
    private readonly List<int> _auditioned = new();
    private readonly BassPattern _pattern = new() { Length = 8 };

    private BassEditorViewModel Create() => new(_pattern, () => _changes++, _auditioned.Add);

    [Fact]
    public void Shows_32_steps_and_marks_the_ones_past_the_length()
    {
        var vm = Create();
        Assert.Equal(32, vm.Steps.Count);
        Assert.True(vm.Steps[7].IsInLength);
        Assert.False(vm.Steps[8].IsInLength);
    }

    [Fact]
    public void Changing_length_updates_model_and_steps()
    {
        var vm = Create();
        vm.Length = 12;

        Assert.Equal(12, _pattern.Length);
        Assert.True(vm.Steps[11].IsInLength);
        Assert.Equal(1, _changes);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(40, 32)]
    public void Length_is_clamped(int requested, int expected)
    {
        var vm = Create();
        vm.Length = requested;
        Assert.Equal(expected, vm.Length);
    }

    [Fact]
    public void Triplet_maps_to_the_file_flag()
    {
        var vm = Create();
        vm.Triplet = true;
        Assert.Equal(1, _pattern.Triplet);
        vm.Triplet = false;
        Assert.Equal(0, _pattern.Triplet);
        Assert.Equal(2, _changes);
    }

    [Fact]
    public void Cycle_state_goes_off_note_tie_off_and_auditions_notes()
    {
        var step = Create().Steps[0];

        step.CycleStateCommand.Execute(null);
        Assert.Equal(BassStepState.Note, _pattern.Steps[0].State);
        Assert.Equal("Note", step.StateLabel);
        Assert.Equal([36], _auditioned);

        step.CycleStateCommand.Execute(null);
        Assert.Equal(BassStepState.Tie, _pattern.Steps[0].State);
        step.CycleStateCommand.Execute(null);
        Assert.Equal(BassStepState.Off, _pattern.Steps[0].State);
        Assert.False(step.HasNote);
        Assert.Single(_auditioned);
    }

    [Fact]
    public void Note_buttons_move_by_semitone_and_octave_and_turn_the_step_on()
    {
        var step = Create().Steps[2];

        step.NoteUpCommand.Execute(null);
        Assert.Equal(37, _pattern.Steps[2].Note);
        Assert.Equal(BassStepState.Note, _pattern.Steps[2].State);

        step.OctaveUpCommand.Execute(null);
        Assert.Equal(49, step.Note);
        step.NoteDownCommand.Execute(null);
        step.OctaveDownCommand.Execute(null);
        Assert.Equal(36, step.Note);
        Assert.Equal([37, 49, 48, 36], _auditioned);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(200, 96)]
    public void Notes_stay_in_the_t8_range(int requested, int expected)
    {
        var step = Create().Steps[0];
        step.Note = requested;
        Assert.Equal(expected, step.Note);
    }

    [Theory]
    [InlineData(12, "C0")]
    [InlineData(36, "C2")]
    [InlineData(46, "A#2")]
    [InlineData(96, "C7")]
    public void Note_names_match_the_t8_display(int note, string name) =>
        Assert.Equal(name, BassStepViewModel.FormatNote(note));

    [Fact]
    public void Accent_and_slide_write_through()
    {
        var step = Create().Steps[4];
        step.Accent = true;
        step.Slide = true;
        Assert.True(_pattern.Steps[4].Accent);
        Assert.True(_pattern.Steps[4].Slide);
        Assert.Equal(2, _changes);
    }

    [Fact]
    public void Setting_the_same_value_does_not_mark_changes()
    {
        var vm = Create();
        vm.Length = 8;
        vm.Steps[0].Accent = false;
        vm.Steps[0].Note = 36;
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void Transpose_moves_every_step()
    {
        _pattern.Steps[0].Note = 40;
        _pattern.Steps[1].Note = 90;
        var vm = Create();

        vm.TransposeCommand.Execute(12);

        Assert.Equal(52, _pattern.Steps[0].Note);
        Assert.Equal(96, _pattern.Steps[1].Note); // clamped at C7
    }

    [Fact]
    public void Beat_markers_fall_on_every_fourth_step()
    {
        var vm = Create();
        Assert.Equal([1, 5, 9, 13, 17, 21, 25, 29], vm.Steps.Where(s => s.IsBeat).Select(s => s.Number));
    }
}
