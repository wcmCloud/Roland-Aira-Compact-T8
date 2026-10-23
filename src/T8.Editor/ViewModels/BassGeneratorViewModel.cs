using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;

namespace T8.Editor.ViewModels;

/// <summary>A value and the name shown for it in a picker.</summary>
public sealed record Choice<T>(T Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Generates bass lines into the current pattern from a key, a scale, a chord progression and
/// chances. Generate replaces the whole line and keeps the seed. Vary keeps the rhythm, the pitches
/// and (by default) the root on the beat. Fill bank writes eight related lines into the next empty
/// slots. Each of those is one step on the shared undo history.
/// </summary>
public sealed partial class BassGeneratorViewModel : ObservableObject
{
    private readonly BassGeneratorOptions _options;
    private readonly Action<BassGeneratorOptions> _save;
    private readonly Action<string> _generated;
    private readonly PatternHistory? _history;
    private readonly Func<int, int, BassPattern>? _patternAt;
    private readonly Func<int, int, RhythmPattern>? _rhythmAt;
    private readonly Func<int, IReadOnlyList<(int Bank, int Pattern)>>? _freeSlots;
    private readonly Func<RhythmPattern>? _currentRhythm;
    private int _variation;
    private BassEditorViewModel? _target;

    [ObservableProperty]
    private string _summary = "";

    /// <summary>Colour the scale and the current chord on the bass keyboard. Off leaves every key plain.</summary>
    [ObservableProperty]
    private bool _showColours;

    /// <param name="patternAt">The library's bass pattern for a bank and pattern.</param>
    /// <param name="rhythmAt">The library's rhythm pattern for a slot Fill bank copies the drums into.</param>
    /// <param name="freeSlots">The next empty slots after the one being edited.</param>
    /// <param name="currentRhythm">The drums edited with this bass line.</param>
    /// <param name="history">Undo stack for the open library. Null leaves Generate without an undo step.</param>
    public BassGeneratorViewModel(
        BassGeneratorOptions options,
        Action<BassGeneratorOptions> save,
        Action<string> generated,
        Func<int, int, BassPattern>? patternAt = null,
        Func<int, int, RhythmPattern>? rhythmAt = null,
        Func<int, IReadOnlyList<(int Bank, int Pattern)>>? freeSlots = null,
        Func<RhythmPattern>? currentRhythm = null,
        PatternHistory? history = null)
    {
        _options = options;
        _save = save;
        _generated = generated;
        _patternAt = patternAt;
        _rhythmAt = rhythmAt;
        _freeSlots = freeSlots;
        _currentRhythm = currentRhythm;
        _history = history;
        if (history is not null)
            history.Changed += _ => UndoCommand.NotifyCanExecuteChanged();
    }

    public static IReadOnlyList<Choice<int>> Keys { get; } =
        BassGenerator.KeyNames.Select((name, i) => new Choice<int>(i, name)).ToList();

    public static IReadOnlyList<Choice<Scale>> Scales { get; } =
        Enum.GetValues<Scale>().Select(s => new Choice<Scale>(s, BassGenerator.DisplayName(s))).ToList();

    public static IReadOnlyList<Choice<Progression>> Progressions { get; } =
        Enum.GetValues<Progression>().Select(p => new Choice<Progression>(p, BassGenerator.DisplayName(p))).ToList();

    public static IReadOnlyList<int> ChordLengths { get; } = [4, 8, 16, 32];
    public static IReadOnlyList<int> OctaveChoices { get; } = [0, 1, 2, 3, 4, 5];
    public static IReadOnlyList<int> OctaveSpans { get; } = [1, 2, 3, 4];

    /// <summary>The bass pattern being edited; set by the main window when the pattern changes.</summary>
    public BassEditorViewModel? Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    public Choice<int> Key
    {
        get => Keys[_options.Root];
        set => Set(value is null ? _options.Root : value.Value, _options.Root, v => _options.Root = v);
    }

    public Choice<Scale> Scale
    {
        get => Scales.First(s => s.Value == _options.Scale);
        set => Set(value is null ? _options.Scale : value.Value, _options.Scale, v => _options.Scale = v);
    }

    public Choice<Progression> Progression
    {
        get => Progressions.First(p => p.Value == _options.Progression);
        set => Set(value is null ? _options.Progression : value.Value, _options.Progression, v => _options.Progression = v);
    }

    public int StepsPerChord
    {
        get => _options.StepsPerChord;
        set => Set(value, _options.StepsPerChord, v => _options.StepsPerChord = v);
    }

    public int LowestOctave
    {
        get => _options.LowestOctave;
        set => Set(value, _options.LowestOctave, v => _options.LowestOctave = v);
    }

    public int Octaves
    {
        get => _options.Octaves;
        set => Set(value, _options.Octaves, v => _options.Octaves = v);
    }

    public int NoteChance
    {
        get => _options.NoteChance;
        set => Set(Percent(value), _options.NoteChance, v => _options.NoteChance = v);
    }

    public int ChordToneChance
    {
        get => _options.ChordToneChance;
        set => Set(Percent(value), _options.ChordToneChance, v => _options.ChordToneChance = v);
    }

    public int TieChance
    {
        get => _options.TieChance;
        set => Set(Percent(value), _options.TieChance, v => _options.TieChance = v);
    }

    public int SlideChance
    {
        get => _options.SlideChance;
        set => Set(Percent(value), _options.SlideChance, v => _options.SlideChance = v);
    }

    public int AccentChance
    {
        get => _options.AccentChance;
        set => Set(Percent(value), _options.AccentChance, v => _options.AccentChance = v);
    }

    public int OctaveJumpChance
    {
        get => _options.OctaveJumpChance;
        set => Set(Percent(value), _options.OctaveJumpChance, v => _options.OctaveJumpChance = v);
    }

    public int Seed
    {
        get => _options.Seed;
        set => Set(value, _options.Seed, v => _options.Seed = v);
    }

    public bool LockRoots
    {
        get => _options.LockRoots;
        set => Set(value, _options.LockRoots, v => _options.LockRoots = v);
    }

    /// <summary>Sets the key and scale from the notes in the line. An empty line is left as it is.</summary>
    public void MatchPattern(BassPattern pattern)
    {
        if (BassGenerator.DetectKey(pattern) is not { } detected)
            return;
        Key = Keys[detected.Root];
        Scale = Scales.First(s => s.Value == detected.Scale);
    }

    public BassGeneratorOptions Options => _options.Clone();

    /// <summary>Replaces the whole line with the current settings and seed.</summary>
    [RelayCommand]
    private void Generate()
    {
        if (Target is not { } target)
            return;
        PatternHistory.Apply(_history, () =>
        {
            BassGenerator.Generate(target.Pattern, _options);
            target.Refresh();
        });
        Summary = LineSummary(target);
        _generated(Summary);
    }

    /// <summary>New seed, then generate: a different line that follows the same rules.</summary>
    [RelayCommand]
    private void Randomize()
    {
        Seed = Random.Shared.Next(1, 100_000);
        Generate();
    }

    /// <summary>
    /// Keeps the rhythm and the pitches. Roots on the beat keep their octave when locked; other
    /// notes can jump an octave. The seed stays, so Generate still brings the line back.
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
            BassGenerator.Vary(target.Pattern, options);
            target.Refresh();
        });
        Summary = LockRoots
            ? "Variation. Kept roots on the beat. Rewrote octave jumps."
            : "Variation. Roots can jump octaves too. Pitch and rhythm stay.";
        _generated(Summary);
    }

    /// <summary>
    /// Writes eight related lines into the next empty slots, closest first. Each keeps the locked
    /// roots and takes a copy of the current drums, so a song has somewhere to go.
    /// </summary>
    [RelayCommand]
    private void FillBank()
    {
        if (Target is not { } target || _patternAt is null || _rhythmAt is null || _freeSlots is null || _currentRhythm is null)
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

        var rhythm = _currentRhythm();
        PatternHistory.Apply(_history, () =>
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var (bank, pattern) = slots[i];
                var bassSlot = _patternAt(bank, pattern);
                var rhythmSlot = _rhythmAt(bank, pattern);
                var varied = target.Pattern.Clone();
                var options = _options.Clone();
                options.Seed = PatternVariation.Seed(_options.Seed, i);
                BassGenerator.Vary(varied, options, PatternVariation.Amount(i, slots.Count));
                bassSlot.CopyFrom(varied);
                rhythmSlot.CopyFrom(rhythm);
            }
        });
        var roots = LockRoots ? "Roots on the beat stay; other notes can jump an octave." : "Octave jumps move; the rhythm of the line stays.";
        Summary = $"{slots.Count} variations written to {PatternSlots.Describe(slots)}. {roots} The drums are copied with them.";
        if (slots.Count < PatternVariation.BankSize)
            Summary += $" {PatternVariation.BankSize - slots.Count} more would need an empty pattern.";
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

    private string LineSummary(BassEditorViewModel target) =>
        $"{Key.Name} {Scale.Name.ToLowerInvariant()}" +
        (_options.Progression == Core.Patterns.Progression.None ? "" : $", {Progression.Name}") +
        $" · seed {Seed} · {target.Length} steps";

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
