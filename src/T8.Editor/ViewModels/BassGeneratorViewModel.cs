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
/// chances. Generate uses the current seed, so tweaking a setting and pressing it again keeps the
/// same feel; Randomize picks a new seed. Undo puts back the line from before the last generate.
/// </summary>
public sealed partial class BassGeneratorViewModel : ObservableObject
{
    private readonly BassGeneratorOptions _options;
    private readonly Action<BassGeneratorOptions> _save;
    private readonly Action _generated;
    private (BassEditorViewModel Target, BassStep[] Steps)? _undo;
    private BassEditorViewModel? _target;

    [ObservableProperty]
    private string _summary = "";

    public BassGeneratorViewModel(BassGeneratorOptions options, Action<BassGeneratorOptions> save, Action generated)
    {
        _options = options;
        _save = save;
        _generated = generated;
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
        set
        {
            if (SetProperty(ref _target, value))
                UndoCommand.NotifyCanExecuteChanged();
        }
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

    public BassGeneratorOptions Options => _options.Clone();

    /// <summary>Writes a new line into the pattern with the current settings and seed.</summary>
    [RelayCommand]
    private void Generate()
    {
        if (Target is not { } target)
            return;
        _undo = (target, target.Pattern.Steps.Select(s => s.Clone()).ToArray());
        BassGenerator.Generate(target.Pattern, _options);
        target.Refresh();
        UndoCommand.NotifyCanExecuteChanged();
        Summary = $"{Key.Name} {Scale.Name.ToLowerInvariant()}" +
                  (_options.Progression == Core.Patterns.Progression.None ? "" : $", {Progression.Name}") +
                  $" · seed {Seed} · {target.Length} steps";
        _generated();
    }

    /// <summary>New seed, then generate: a different line that follows the same rules.</summary>
    [RelayCommand]
    private void Randomize()
    {
        Seed = Random.Shared.Next(1, 100_000);
        Generate();
    }

    private bool CanUndo() => _undo is { } undo && undo.Target == Target;

    /// <summary>Puts back the line from before the last Generate or Randomize.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undo is not { } undo || undo.Target != Target)
            return;
        for (var i = 0; i < undo.Steps.Length; i++)
        {
            var step = undo.Target.Pattern.Steps[i];
            var saved = undo.Steps[i];
            step.State = saved.State;
            step.Note = saved.Note;
            step.Accent = saved.Accent;
            step.Slide = saved.Slide;
        }
        undo.Target.Refresh();
        _undo = null;
        UndoCommand.NotifyCanExecuteChanged();
        Summary = "Undone.";
        _generated();
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
