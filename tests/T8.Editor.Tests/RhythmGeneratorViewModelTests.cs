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
}
