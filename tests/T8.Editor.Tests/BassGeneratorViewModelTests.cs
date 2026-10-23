using T8.Core.Patterns;
using T8.Editor.ViewModels;

namespace T8.Editor.Tests;

public class BassGeneratorViewModelTests
{
    private readonly BassPattern _pattern = new() { Length = 16 };
    private readonly List<BassGeneratorOptions> _saved = new();
    private int _generated;
    private PatternLibrary _library = null!;
    private PatternHistory _history = null!;

    private (BassGeneratorViewModel Generator, BassEditorViewModel Bass) Create()
    {
        _library = PatternLibrary.CreateBlank();
        _library.Bass[0, 0] = _pattern;
        _history = new PatternHistory(() => _library);
        var bass = new BassEditorViewModel(_pattern, () => { }, history: _history);
        var generator = new BassGeneratorViewModel(new BassGeneratorOptions(), _saved.Add, _ => _generated++, history: _history) { Target = bass };
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
    public void Undo_keeps_the_earlier_line_when_the_pattern_changes()
    {
        var (generator, _) = Create();
        var before = _pattern.Steps.Select(s => (s.State, s.Note, s.Accent, s.Slide)).ToList();
        generator.GenerateCommand.Execute(null);
        generator.Target = new BassEditorViewModel(new BassPattern(), () => { });

        Assert.True(generator.UndoCommand.CanExecute(null));
        generator.UndoCommand.Execute(null);

        Assert.Equal(before, _pattern.Steps.Select(s => (s.State, s.Note, s.Accent, s.Slide)).ToList());
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

    [Fact]
    public void Vary_keeps_the_root_on_the_beat_and_can_be_undone()
    {
        var (generator, bass) = Create();
        generator.OctaveJumpChance = 100;
        generator.Octaves = 2;
        generator.Seed = 4;
        generator.GenerateCommand.Execute(null);
        var root = _pattern.Steps[0].Note;
        var beforeVary = _pattern.Steps.Select(s => (s.State, s.Note)).ToList();

        generator.VaryCommand.Execute(null);

        Assert.Equal(4, generator.Seed);
        Assert.Equal(root, _pattern.Steps[0].Note);
        Assert.Equal(root % 12, _pattern.Steps.Where(s => s.State == BassStepState.Note).First().Note % 12);
        Assert.Contains("Kept roots on the beat", generator.Summary);
        generator.UndoCommand.Execute(null);
        Assert.Equal(beforeVary, _pattern.Steps.Select(s => (s.State, s.Note)).ToList());
        Assert.False(string.IsNullOrEmpty(bass.Steps[0].NoteName));
    }

    [Fact]
    public void Fill_bank_writes_lines_into_empty_slots_and_copies_the_drums()
    {
        var library = PatternLibrary.CreateBlank();
        var options = new BassGeneratorOptions { OctaveJumpChance = 100, Octaves = 3, NoteChance = 100 };
        BassGenerator.Generate(library.Bass[0, 0], options);
        library.Rhythm[0, 0].Steps[0][RhythmTrack.BD].On = true;
        var root = library.Bass[0, 0].Steps[0].Note;
        var source = library.Bass[0, 0].Steps.Select(s => (s.State, s.Note)).ToList();
        var history = new PatternHistory(() => library);
        var bass = new BassEditorViewModel(library.Bass[0, 0], () => { }, history: history);
        var summaries = new List<string>();
        var generator = new BassGeneratorViewModel(options, _ => { }, summaries.Add,
            (bank, pattern) => library.Bass[bank - 1, pattern - 1],
            (bank, pattern) => library.Rhythm[bank - 1, pattern - 1],
            count => Enumerable.Range(2, count).Select(pattern => (1, pattern)).ToList(),
            () => library.Rhythm[0, 0],
            history)
        { Target = bass };

        generator.FillBankCommand.Execute(null);

        Assert.Equal(source, library.Bass[0, 0].Steps.Select(s => (s.State, s.Note)).ToList());
        Assert.Equal(root, library.Bass[0, 1].Steps[0].Note);
        Assert.True(library.Rhythm[0, 1].Steps[0][RhythmTrack.BD].On);
        Assert.True(library.Rhythm[0, 8].Steps[0][RhythmTrack.BD].On);
        Assert.False(library.Rhythm[0, 9].Steps[0][RhythmTrack.BD].On);
        Assert.Contains("bank 1 patterns 2–9", summaries[^1]);
        Assert.Contains("drums are copied", summaries[^1]);

        generator.UndoCommand.Execute(null);
        Assert.Equal(BassStepState.Off, library.Bass[0, 1].Steps[0].State);
        Assert.False(library.Rhythm[0, 1].Steps[0][RhythmTrack.BD].On);
    }
}
