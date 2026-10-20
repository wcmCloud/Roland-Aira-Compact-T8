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
    private (RhythmEditorViewModel Target, Action Restore)? _undo;
    private readonly Func<(int Bank, int Pattern)>? _currentSlot;
    private readonly Func<int, int, RhythmPattern>? _patternAt;
    private RhythmEditorViewModel? _target;

    [ObservableProperty]
    private string _summary = "";

    /// <param name="currentSlot">Bank and pattern being edited, for Make fill.</param>
    /// <param name="patternAt">The library's rhythm pattern for a bank and pattern, for Make fill.</param>
    public RhythmGeneratorViewModel(
        RhythmGeneratorOptions options,
        Action<RhythmGeneratorOptions> save,
        Action<string> generated,
        Func<(int Bank, int Pattern)>? currentSlot = null,
        Func<int, int, RhythmPattern>? patternAt = null)
    {
        _options = options;
        _save = save;
        _generated = generated;
        _currentSlot = currentSlot;
        _patternAt = patternAt;
    }

    public static IReadOnlyList<int> PatternNumbers { get; } = Enumerable.Range(1, PatternLibrary.PatternsPerBank).ToList();

    /// <summary>Pattern number in the current bank that Make fill writes to.</summary>
    public int FillPattern
    {
        get => _options.FillPattern;
        set => Set(Math.Clamp(value, 1, PatternLibrary.PatternsPerBank), _options.FillPattern, v => _options.FillPattern = v);
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
        _undo = (target, Snapshot(target.Pattern, target.Refresh));
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

    /// <summary>
    /// Writes a fill for the current pattern into <see cref="FillPattern"/> of the same bank. On the
    /// T-8, assign that pattern as the fill (SHIFT + STEP 8) and trigger it with SHIFT + STEP 7.
    /// </summary>
    [RelayCommand]
    private void MakeFill()
    {
        if (Target is not { } target || _currentSlot is null || _patternAt is null)
            return;
        var (bank, pattern) = _currentSlot();
        if (pattern == FillPattern)
        {
            Summary = $"The fill can't go into the pattern you're editing. Pick another fill pattern than {pattern}.";
            _generated(Summary);
            return;
        }

        var fill = _patternAt(bank, FillPattern);
        _undo = (target, Snapshot(fill, () => { }));
        RhythmGenerator.GenerateFill(target.Pattern, fill, _options);
        UndoCommand.NotifyCanExecuteChanged();
        Summary = $"Fill written to bank {bank} pattern {FillPattern}. On the T-8: SHIFT + STEP 8 to pick it as the fill, " +
                  "SHIFT + STEP 7 to play it. In the editor, press Fill while playing.";
        _generated(Summary);
    }

    private bool CanUndo() => _undo is { } undo && undo.Target == Target;

    /// <summary>Puts back the pattern from before the last Generate, Randomize or Make fill.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undo is not { } undo || undo.Target != Target)
            return;
        undo.Restore();
        _undo = null;
        UndoCommand.NotifyCanExecuteChanged();
        Summary = "Undone.";
        _generated(Summary);
    }

    /// <summary>Copies a pattern's cells and settings; the returned action writes them back and calls <paramref name="refresh"/>.</summary>
    private static Action Snapshot(RhythmPattern pattern, Action refresh)
    {
        var saved = pattern.Clone();
        return () =>
        {
            pattern.Length = saved.Length;
            pattern.Shuffle = saved.Shuffle;
            pattern.Flam = saved.Flam;
            pattern.Scale = saved.Scale;
            for (var s = 0; s < RhythmPattern.StepCount; s++)
            {
                for (var t = 0; t < RhythmPattern.TrackCount; t++)
                {
                    var cell = pattern.Steps[s].Cells[t];
                    var from = saved.Steps[s].Cells[t];
                    cell.On = from.On;
                    cell.Velocity = from.Velocity;
                    cell.SubStep = from.SubStep;
                    cell.Probability = from.Probability;
                    cell.SubStepProbability = from.SubStepProbability;
                }
            }
            refresh();
        };
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
