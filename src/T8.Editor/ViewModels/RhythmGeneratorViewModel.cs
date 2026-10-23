using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;

namespace T8.Editor.ViewModels;

/// <summary>
/// Generates drum patterns into the current rhythm pattern from a style and chances. Generate
/// replaces the whole pattern and keeps the seed. Vary keeps locked parts. Fill bank writes eight
/// related patterns into the next empty slots. Each of those is one step on the shared undo history.
/// </summary>
public sealed partial class RhythmGeneratorViewModel : ObservableObject
{
    private readonly RhythmGeneratorOptions _options;
    private readonly Action<RhythmGeneratorOptions> _save;
    private readonly Action<string> _generated;
    private readonly PatternHistory? _history;
    private readonly Func<(int Bank, int Pattern)>? _currentSlot;
    private readonly Func<int, int, RhythmPattern>? _patternAt;
    private readonly Func<int, int, BassPattern>? _bassAt;
    private readonly Func<int, IReadOnlyList<(int Bank, int Pattern)>>? _freeSlots;
    private readonly Func<BassPattern>? _currentBass;
    private int _variation;
    private RhythmEditorViewModel? _target;

    [ObservableProperty]
    private string _summary = "";

    /// <param name="currentSlot">Bank and pattern being edited, for Make fill and Fill bank.</param>
    /// <param name="patternAt">The library's rhythm pattern for a bank and pattern.</param>
    /// <param name="bassAt">The library's bass pattern for a slot Fill bank copies the bass into.</param>
    /// <param name="freeSlots">The next empty slots, up to the count asked for.</param>
    /// <param name="currentBass">The bass line edited with this rhythm pattern.</param>
    /// <param name="history">Undo stack for the open library. Null leaves Generate without an undo step.</param>
    public RhythmGeneratorViewModel(
        RhythmGeneratorOptions options,
        Action<RhythmGeneratorOptions> save,
        Action<string> generated,
        Func<(int Bank, int Pattern)>? currentSlot = null,
        Func<int, int, RhythmPattern>? patternAt = null,
        Func<int, int, BassPattern>? bassAt = null,
        Func<int, IReadOnlyList<(int Bank, int Pattern)>>? freeSlots = null,
        Func<BassPattern>? currentBass = null,
        PatternHistory? history = null)
    {
        _options = options;
        _save = save;
        _generated = generated;
        _currentSlot = currentSlot;
        _patternAt = patternAt;
        _bassAt = bassAt;
        _freeSlots = freeSlots;
        _currentBass = currentBass;
        _history = history;
        if (history is not null)
            history.Changed += _ => UndoCommand.NotifyCanExecuteChanged();
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
        set => SetProperty(ref _target, value);
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

    public bool LockBassDrum
    {
        get => _options.LockBassDrum;
        set => Set(value, _options.LockBassDrum, v => _options.LockBassDrum = v);
    }

    public bool LockSnare
    {
        get => _options.LockSnare;
        set => Set(value, _options.LockSnare, v => _options.LockSnare = v);
    }

    public bool LockTom
    {
        get => _options.LockTom;
        set => Set(value, _options.LockTom, v => _options.LockTom = v);
    }

    public bool LockClap
    {
        get => _options.LockClap;
        set => Set(value, _options.LockClap, v => _options.LockClap = v);
    }

    public bool LockClosedHat
    {
        get => _options.LockClosedHat;
        set => Set(value, _options.LockClosedHat, v => _options.LockClosedHat = v);
    }

    public bool LockOpenHat
    {
        get => _options.LockOpenHat;
        set => Set(value, _options.LockOpenHat, v => _options.LockOpenHat = v);
    }

    public int Seed
    {
        get => _options.Seed;
        set => Set(value, _options.Seed, v => _options.Seed = v);
    }

    /// <summary>Replaces the whole pattern with the current settings and seed.</summary>
    [RelayCommand]
    private void Generate()
    {
        if (Target is not { } target)
            return;
        PatternHistory.Apply(_history, () =>
        {
            RhythmGenerator.Generate(target.Pattern, _options);
            target.Refresh();
        });
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
    /// Keeps locked tracks and rewrites hats, ghost notes and step probability. The seed stays,
    /// so Generate still brings back the same pattern; press Vary again for another take.
    /// </summary>
    [RelayCommand]
    private void Vary()
    {
        if (Target is not { } target)
            return;
        _variation++;
        var options = _options.Clone();
        options.Seed = PatternVariation.Seed(_options.Seed, PatternVariation.BankSize + _variation);
        PatternHistory.Apply(_history, () =>
        {
            RhythmGenerator.Vary(target.Pattern, options);
            target.Refresh();
        });
        var movement = LiveVariation > 0
            ? "Rewrote hats, ghost notes and probability."
            : "Rewrote hats and ghost notes.";
        Summary = $"Variation. {KeptPhrase()} {movement}";
        _generated(Summary);
    }

    /// <summary>
    /// Writes eight related patterns into the next empty slots, closest first. Each keeps the
    /// locks and takes a copy of the current bass line, so a song has somewhere to go.
    /// </summary>
    [RelayCommand]
    private void FillBank()
    {
        if (Target is not { } target || _patternAt is null || _bassAt is null || _freeSlots is null || _currentBass is null)
        {
            Summary = "Fill bank writes eight variations into the next empty patterns.";
            _generated(Summary);
            return;
        }

        var slots = _freeSlots(PatternVariation.BankSize);
        if (slots.Count == 0)
        {
            Summary = "No empty patterns left. Fill bank writes into empty slots, and these all have notes.";
            _generated(Summary);
            return;
        }

        var bass = _currentBass();
        PatternHistory.Apply(_history, () =>
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var (bank, pattern) = slots[i];
                var rhythmSlot = _patternAt(bank, pattern);
                var bassSlot = _bassAt(bank, pattern);
                var varied = target.Pattern.Clone();
                var options = _options.Clone();
                options.Seed = PatternVariation.Seed(_options.Seed, i);
                RhythmGenerator.Vary(varied, options, PatternVariation.Amount(i, slots.Count));
                rhythmSlot.CopyFrom(varied);
                bassSlot.CopyFrom(bass);
            }
        });
        var movement = LiveVariation > 0
            ? "Hats, ghost notes and probability change"
            : "Hats and ghost notes change";
        Summary = $"{slots.Count} variations written to {PatternSlots.Describe(slots)}. " +
                  $"{KeptPhrase()} {movement}; the bass line is copied with them.";
        if (slots.Count < PatternVariation.BankSize)
            Summary += $" {PatternVariation.BankSize - slots.Count} more would need an empty pattern.";
        _generated(Summary);
    }

    /// <summary>
    /// Writes a fill for the current pattern into <see cref="FillPattern"/> of the same bank. On the
    /// T-8, assign that pattern as the fill (SHIFT + STEP 8) and trigger it with SHIFT + STEP 7.
    /// </summary>
    [RelayCommand]
    private void MakeFill()
    {
        if (Target is null || _currentSlot is null || _patternAt is null)
            return;
        var (bank, pattern) = _currentSlot();
        if (pattern == FillPattern)
        {
            Summary = $"The fill can't go into the pattern you're editing. Pick another fill pattern than {pattern}.";
            _generated(Summary);
            return;
        }

        var fill = _patternAt(bank, FillPattern);
        var source = Target.Pattern;
        PatternHistory.Apply(_history, () => RhythmGenerator.GenerateFill(source, fill, _options));
        Summary = $"Fill written to bank {bank} pattern {FillPattern}. On the T-8: SHIFT + STEP 8 to pick it as the fill, " +
                  "SHIFT + STEP 7 to play it. In the editor, press Fill while playing.";
        _generated(Summary);
    }

    private bool CanUndo() => _history?.CanUndo == true;

    /// <summary>Puts back the last change to the open patterns (the same stack as Cmd/Ctrl+Z).</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_history is null || !_history.CanUndo)
            return;
        _history.Undo();
        Target?.Refresh();
        Summary = "Undone.";
    }

    private string KeptPhrase()
    {
        var names = new List<string>();
        if (LockBassDrum) names.Add("the kick");
        if (LockSnare) names.Add("the snare");
        if (LockTom) names.Add("the tom");
        if (LockClap) names.Add("the clap");
        if (LockClosedHat) names.Add("the closed hat");
        if (LockOpenHat) names.Add("the open hat");
        return names.Count == 0 ? "Nothing locked." : "Kept " + JoinAnd(names) + ".";
    }

    private static string JoinAnd(IReadOnlyList<string> parts) => parts.Count switch
    {
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
    };

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
