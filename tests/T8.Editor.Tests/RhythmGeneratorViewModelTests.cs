using T8.Core.Patterns;
using T8.Editor.ViewModels;

namespace T8.Editor.Tests;

public class RhythmGeneratorViewModelTests
{
    private readonly RhythmPattern _pattern = new() { Length = 16 };
    private readonly List<RhythmGeneratorOptions> _saved = new();
    private readonly List<string> _summaries = new();

    private (RhythmGeneratorViewModel Generator, RhythmEditorViewModel Rhythm) Create()
    {
        var rhythm = new RhythmEditorViewModel(_pattern, () => { });
        var generator = new RhythmGeneratorViewModel(new RhythmGeneratorOptions(), _saved.Add, _summaries.Add) { Target = rhythm };
        return (generator, rhythm);
    }

    [Fact]
    public void Generate_fills_the_grid_and_refreshes_the_cells()
    {
        var (generator, rhythm) = Create();
        generator.Style = RhythmGeneratorViewModel.Styles.Single(s => s.Value == RhythmStyle.House);
        var kick = rhythm[RhythmTrack.BD].Cells[4];
        var raised = new List<string?>();
        kick.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        generator.GenerateCommand.Execute(null);

        Assert.True(kick.On);
        Assert.Contains(nameof(RhythmCellViewModel.On), raised);
        Assert.Contains("House drums", _summaries.Single());
        Assert.Equal(RhythmStyle.House, _saved[^1].Style);
    }

    [Fact]
    public void Undo_restores_the_pattern_from_before()
    {
        var (generator, rhythm) = Create();
        _pattern.Steps[1][RhythmTrack.CY].On = true;
        _pattern.Steps[1][RhythmTrack.CY].Velocity = 4;
        var before = string.Join(",", _pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString()));

        generator.RandomizeCommand.Execute(null);
        Assert.NotEqual(before, string.Join(",", _pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString())));
        generator.UndoCommand.Execute(null);

        Assert.Equal(before, string.Join(",", _pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString())));
        Assert.True(rhythm[RhythmTrack.CY].Cells[1].On);
        Assert.False(generator.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void Instrument_switches_and_chances_are_saved()
    {
        var (generator, _) = Create();
        generator.Snare = false;
        generator.Density = 130;
        generator.Fill = true;

        Assert.False(_saved[^1].Snare);
        Assert.Equal(100, generator.Density);
        Assert.True(_saved[^1].Fill);
    }

    [Fact]
    public void Undo_is_dropped_when_the_pattern_changes()
    {
        var (generator, _) = Create();
        generator.GenerateCommand.Execute(null);
        generator.Target = new RhythmEditorViewModel(new RhythmPattern(), () => { });
        Assert.False(generator.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void Make_fill_writes_into_the_fill_pattern_and_can_be_undone()
    {
        var library = PatternLibrary.CreateBlank();
        var current = library.Rhythm[1, 2]; // bank 2 pattern 3
        RhythmGenerator.Generate(current, new RhythmGeneratorOptions { Style = RhythmStyle.Techno });
        var rhythm = new RhythmEditorViewModel(current, () => { });
        var summaries = new List<string>();
        var generator = new RhythmGeneratorViewModel(new RhythmGeneratorOptions(), _ => { }, summaries.Add,
            () => (2, 3), (bank, pattern) => library.Rhythm[bank - 1, pattern - 1]) { Target = rhythm, FillPattern = 12 };

        generator.MakeFillCommand.Execute(null);

        var fill = library.Rhythm[1, 11];
        Assert.True(fill.Steps[15][RhythmTrack.OH].On);
        Assert.Contains("bank 2 pattern 12", summaries[^1]);
        Assert.Contains("SHIFT + STEP 8", summaries[^1]);

        generator.UndoCommand.Execute(null);
        Assert.DoesNotContain(fill.Steps, s => s.Cells.Any(c => c.On));
    }

    [Fact]
    public void Make_fill_refuses_to_overwrite_the_pattern_being_edited()
    {
        var library = PatternLibrary.CreateBlank();
        var rhythm = new RhythmEditorViewModel(library.Rhythm[0, 15], () => { });
        var generator = new RhythmGeneratorViewModel(new RhythmGeneratorOptions(), _ => { }, _ => { },
            () => (1, 16), (bank, pattern) => library.Rhythm[bank - 1, pattern - 1]) { Target = rhythm };

        generator.MakeFillCommand.Execute(null);

        Assert.Contains("can't go into the pattern you're editing", generator.Summary);
        Assert.False(generator.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void Vary_keeps_the_locked_kick_and_can_be_undone_without_changing_the_seed()
    {
        var (generator, rhythm) = Create();
        _pattern.Steps[0][RhythmTrack.BD].On = true;
        _pattern.Steps[0][RhythmTrack.BD].Velocity = 4;
        generator.Seed = 11;
        generator.GhostChance = 100;

        generator.VaryCommand.Execute(null);
        var varied = _pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString()).ToList();

        Assert.Equal(11, generator.Seed);
        Assert.Equal("140AA", _pattern.Steps[0][RhythmTrack.BD].ToString());
        Assert.Contains(rhythm[RhythmTrack.SD].Cells, c => c.On);
        Assert.Contains("Kept the kick", generator.Summary);

        generator.VaryCommand.Execute(null);
        Assert.NotEqual(varied, _pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString()).ToList());
        generator.UndoCommand.Execute(null);
        Assert.Equal(varied, _pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString()).ToList());
    }

    [Fact]
    public void Locks_are_saved()
    {
        var (generator, _) = Create();
        Assert.True(generator.LockBassDrum);
        generator.LockClosedHat = true;
        Assert.True(_saved[^1].LockClosedHat);
        Assert.True(_saved[^1].LockBassDrum);
    }

    [Fact]
    public void Fill_bank_writes_eight_variations_into_empty_slots_and_undoes_them()
    {
        var library = PatternLibrary.CreateBlank();
        RhythmGenerator.Generate(library.Rhythm[0, 0], new RhythmGeneratorOptions { Style = RhythmStyle.Techno, Variation = 0 });
        library.Rhythm[0, 1].Steps[0][RhythmTrack.BD].On = true; // pattern 2 is taken
        library.Bass[0, 0].Steps[0].State = BassStepState.Note;
        library.Bass[0, 0].Steps[0].Note = 40;
        var source = Describe(library.Rhythm[0, 0]);
        var rhythm = new RhythmEditorViewModel(library.Rhythm[0, 0], () => { });
        var generator = new RhythmGeneratorViewModel(new RhythmGeneratorOptions { Variation = 100, GhostChance = 100 }, _ => { }, _summaries.Add,
            () => (1, 1),
            (bank, pattern) => library.Rhythm[bank - 1, pattern - 1],
            (bank, pattern) => library.Bass[bank - 1, pattern - 1],
            count => FreeSlots(library, count),
            () => library.Bass[0, 0])
        { Target = rhythm };

        generator.FillBankCommand.Execute(null);

        Assert.Equal(source, Describe(library.Rhythm[0, 0]));
        Assert.False(library.Rhythm[0, 1].Steps[4][RhythmTrack.BD].On);
        Assert.Equal(40, library.Bass[0, 2].Steps[0].Note);
        Assert.True(library.Rhythm[0, 2].Steps[0][RhythmTrack.BD].On);
        Assert.True(library.Rhythm[0, 9].Steps[0][RhythmTrack.BD].On);
        Assert.False(library.Rhythm[0, 10].Steps[0][RhythmTrack.BD].On);
        Assert.Contains("bank 1 patterns 3–10", _summaries[^1]);
        Assert.Contains("bass line is copied", _summaries[^1]);

        generator.UndoCommand.Execute(null);
        Assert.False(library.Rhythm[0, 2].Steps[0][RhythmTrack.BD].On);
        Assert.Equal(BassStepState.Off, library.Bass[0, 2].Steps[0].State);
        Assert.True(library.Rhythm[0, 1].Steps[0][RhythmTrack.BD].On);
    }

    [Fact]
    public void Fill_bank_says_when_nothing_is_empty()
    {
        var library = PatternLibrary.CreateBlank();
        foreach (var pattern in Enumerable.Range(0, 16))
            library.Rhythm[0, pattern].Steps[0][RhythmTrack.BD].On = true;
        var rhythm = new RhythmEditorViewModel(library.Rhythm[0, 0], () => { });
        var generator = new RhythmGeneratorViewModel(new RhythmGeneratorOptions(), _ => { }, _summaries.Add,
            patternAt: (bank, pattern) => library.Rhythm[bank - 1, pattern - 1],
            bassAt: (bank, pattern) => library.Bass[bank - 1, pattern - 1],
            freeSlots: _ => [],
            currentBass: () => library.Bass[0, 0])
        { Target = rhythm };

        generator.FillBankCommand.Execute(null);

        Assert.Contains("No empty patterns", generator.Summary);
        Assert.False(generator.UndoCommand.CanExecute(null));
    }

    private static List<(int Bank, int Pattern)> FreeSlots(PatternLibrary library, int count)
    {
        var slots = new List<(int, int)>();
        for (var pattern = 2; pattern <= 16 && slots.Count < count; pattern++)
        {
            var empty = library.Bass[0, pattern - 1].Steps.All(s => s.State == BassStepState.Off)
                && library.Rhythm[0, pattern - 1].Steps.All(s => s.Cells.All(c => !c.On));
            if (empty)
                slots.Add((1, pattern));
        }
        return slots;
    }

    private static string Describe(RhythmPattern pattern) =>
        string.Join(",", pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString()));
}
