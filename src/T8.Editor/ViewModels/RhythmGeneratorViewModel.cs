using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;

namespace T8.Editor.ViewModels;

/// <summary>
/// Generates drum patterns into the current rhythm pattern from a style and chances. Generate keeps
/// the seed, Randomize picks a new one, Undo puts back the pattern from before the last generate.
/// </summary>
public sealed partial class RhythmGeneratorViewModel : ObservableObject
{
    private readonly RhythmGeneratorOptions _options;
    private readonly Action<RhythmGeneratorOptions> _save;
    private readonly Action<string> _generated;
    private (RhythmEditorViewModel Target, RhythmCell[][] Cells)? _undo;
    private RhythmEditorViewModel? _target;

    [ObservableProperty]
    private string _summary = "";

    public RhythmGeneratorViewModel(RhythmGeneratorOptions options, Action<RhythmGeneratorOptions> save, Action<string> generated)
    {
        _options = options;
        _save = save;
        _generated = generated;
    }

    public static IReadOnlyList<Choice<RhythmStyle>> Styles { get; } =
        Enum.GetValues<RhythmStyle>().Select(s => new Choice<RhythmStyle>(s, RhythmGenerator.DisplayName(s))).ToList();

    /// <summary>The rhythm pattern being edited; set by the main window when the pattern changes.</summary>
    public RhythmEditorViewModel? Target
    {
        get => _target;
        set
        {
            if (SetProperty(ref _target, value))
                UndoCommand.NotifyCanExecuteChanged();
        }
    }

    public Choice<RhythmStyle> Style
    {
        get => Styles.First(s => s.Value == _options.Style);
        set => Set(value is null ? _options.Style : value.Value, _options.Style, v => _options.Style = v);
    }

    public int Density
    {
        get => _options.Density;
        set => Set(Percent(value), _options.Density, v => _options.Density = v);
    }

    public int Variation
    {
        get => _options.Variation;
        set => Set(Percent(value), _options.Variation, v => _options.Variation = v);
    }

    public int AccentChance
    {
        get => _options.AccentChance;
        set => Set(Percent(value), _options.AccentChance, v => _options.AccentChance = v);
    }

    public int GhostChance
    {
        get => _options.GhostChance;
        set => Set(Percent(value), _options.GhostChance, v => _options.GhostChance = v);
    }

    public int RollChance
    {
        get => _options.RollChance;
        set => Set(Percent(value), _options.RollChance, v => _options.RollChance = v);
    }

    public int LiveVariation
    {
        get => _options.LiveVariation;
        set => Set(Percent(value), _options.LiveVariation, v => _options.LiveVariation = v);
    }

    public bool BassDrum
    {
        get => _options.BassDrum;
        set => Set(value, _options.BassDrum, v => _options.BassDrum = v);
    }

    public bool Snare
    {
        get => _options.Snare;
        set => Set(value, _options.Snare, v => _options.Snare = v);
    }

    public bool Tom
    {
        get => _options.Tom;
        set => Set(value, _options.Tom, v => _options.Tom = v);
    }

    public bool Clap
    {
        get => _options.Clap;
        set => Set(value, _options.Clap, v => _options.Clap = v);
    }

    public bool ClosedHat
    {
        get => _options.ClosedHat;
        set => Set(value, _options.ClosedHat, v => _options.ClosedHat = v);
    }

    public bool OpenHat
    {
        get => _options.OpenHat;
        set => Set(value, _options.OpenHat, v => _options.OpenHat = v);
    }

    public bool Fill
    {
        get => _options.Fill;
        set => Set(value, _options.Fill, v => _options.Fill = v);
    }

    public int Seed
    {
        get => _options.Seed;
        set => Set(value, _options.Seed, v => _options.Seed = v);
    }

    /// <summary>Writes a new pattern with the current settings and seed.</summary>
    [RelayCommand]
    private void Generate()
    {
        if (Target is not { } target)
            return;
        _undo = (target, target.Pattern.Steps.Select(s => s.Cells.Select(c => c.Clone()).ToArray()).ToArray());
        RhythmGenerator.Generate(target.Pattern, _options);
        target.Refresh();
        UndoCommand.NotifyCanExecuteChanged();
        Summary = $"{Style.Name} drums · seed {Seed} · {target.Length} steps";
        _generated(Summary);
    }

    /// <summary>New seed, then generate: a different pattern in the same style.</summary>
    [RelayCommand]
    private void Randomize()
    {
        Seed = Random.Shared.Next(1, 100_000);
        Generate();
    }

    private bool CanUndo() => _undo is { } undo && undo.Target == Target;

    /// <summary>Puts back the pattern from before the last Generate or Randomize.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undo is not { } undo || undo.Target != Target)
            return;
        for (var s = 0; s < undo.Cells.Length; s++)
        {
            for (var t = 0; t < undo.Cells[s].Length; t++)
            {
                var cell = undo.Target.Pattern.Steps[s].Cells[t];
                var saved = undo.Cells[s][t];
                cell.On = saved.On;
                cell.Velocity = saved.Velocity;
                cell.SubStep = saved.SubStep;
                cell.Probability = saved.Probability;
                cell.SubStepProbability = saved.SubStepProbability;
            }
        }
        undo.Target.Refresh();
        _undo = null;
        UndoCommand.NotifyCanExecuteChanged();
        Summary = "Undone.";
        _generated(Summary);
    }

    private static int Percent(int value) => Math.Clamp(value, 0, 100);

    private void Set<T>(T value, T current, Action<T> apply, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(value, current))
            return;
        apply(value);
        OnPropertyChanged(name);
        _save(_options.Clone());
    }
}
