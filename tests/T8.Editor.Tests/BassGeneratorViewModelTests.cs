using T8.Core.Patterns;
using T8.Editor.ViewModels;

namespace T8.Editor.Tests;

public class BassGeneratorViewModelTests
{
    private readonly BassPattern _pattern = new() { Length = 16 };
    private readonly List<BassGeneratorOptions> _saved = new();
    private int _generated;

    private (BassGeneratorViewModel Generator, BassEditorViewModel Bass) Create()
    {
        var bass = new BassEditorViewModel(_pattern, () => { });
        var generator = new BassGeneratorViewModel(new BassGeneratorOptions(), _saved.Add, _ => _generated++) { Target = bass };
        return (generator, bass);
    }

    [Fact]
    public void Generate_fills_the_pattern_and_refreshes_the_steps()
    {
        var (generator, bass) = Create();
        generator.Key = BassGeneratorViewModel.Keys[9]; // A
        generator.Scale = BassGeneratorViewModel.Scales.Single(s => s.Value == Scale.HarmonicMinor);
        var raised = new List<string?>();
        bass.Steps[0].PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        generator.GenerateCommand.Execute(null);

        Assert.Equal(BassStepState.Note, _pattern.Steps[0].State);
        Assert.Equal(9, _pattern.Steps[0].Note % 12);
        Assert.Contains(nameof(BassStepViewModel.NoteName), raised);
        Assert.Equal(1, _generated);
        Assert.Contains("A harmonic minor", generator.Summary);
        Assert.Equal(Scale.HarmonicMinor, _saved[^1].Scale);
    }

    [Fact]
    public void Randomize_changes_the_seed_and_the_line()
    {
        var (generator, _) = Create();
        generator.Seed = 7;
        generator.GenerateCommand.Execute(null);
        var first = _pattern.Steps.Select(s => (s.State, s.Note)).ToList();

        var differs = false;
        for (var i = 0; i < 5 && !differs; i++)
        {
            generator.RandomizeCommand.Execute(null);
            differs = !first.SequenceEqual(_pattern.Steps.Select(s => (s.State, s.Note)));
        }
        Assert.True(differs);
        Assert.NotEqual(7, generator.Seed);
    }

    [Fact]
    public void Undo_restores_the_line_from_before()
    {
        var (generator, bass) = Create();
        _pattern.Steps[3].State = BassStepState.Note;
        _pattern.Steps[3].Note = 61;
        var before = _pattern.Steps.Select(s => (s.State, s.Note, s.Accent, s.Slide)).ToList();
        Assert.False(generator.UndoCommand.CanExecute(null));

        generator.GenerateCommand.Execute(null);
        Assert.True(generator.UndoCommand.CanExecute(null));
        generator.UndoCommand.Execute(null);

        Assert.Equal(before, _pattern.Steps.Select(s => (s.State, s.Note, s.Accent, s.Slide)).ToList());
        Assert.Equal("C#4", bass.Steps[3].NoteName);
        Assert.False(generator.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void Undo_is_dropped_when_the_pattern_changes()
    {
        var (generator, _) = Create();
        generator.GenerateCommand.Execute(null);
        generator.Target = new BassEditorViewModel(new BassPattern(), () => { });
        Assert.False(generator.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void Chances_are_clamped_and_saved()
    {
        var (generator, _) = Create();
        generator.NoteChance = 150;
        generator.SlideChance = -5;
        Assert.Equal(100, generator.NoteChance);
        Assert.Equal(0, generator.SlideChance);
        Assert.Equal(100, _saved[^2].NoteChance);
    }

    [Fact]
    public void Without_a_target_generate_does_nothing()
    {
        var generator = new BassGeneratorViewModel(new BassGeneratorOptions(), _ => { }, _ => _generated++);
        generator.GenerateCommand.Execute(null);
        Assert.Equal(0, _generated);
    }
}
