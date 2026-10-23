using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;

namespace T8.Editor.ViewModels;

/// <summary>Edits one bass pattern in place.</summary>
public sealed partial class BassEditorViewModel : ObservableObject
{
    private readonly BassPattern _pattern;
    private readonly Action _changed;
    private readonly Action<int>? _audition;
    private readonly PatternHistory? _history;
    private BassGeneratorViewModel? _harmony;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedNote), nameof(SelectionLabel))]
    private BassStepViewModel? _selectedStep;

    /// <summary>The step being played, highlighted while Play runs; null when stopped.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayingNote))]
    private BassStepViewModel? _playingStep;

    /// <summary>After a key is played on the keyboard, move the selection to the next step.</summary>
    [ObservableProperty]
    private bool _advanceAfterNote = true;

    /// <summary>Colour the scale and the chord on the keyboard. Off leaves the keys plain. Generate still uses the key.</summary>
    [ObservableProperty]
    private bool _showColours;

    public BassEditorViewModel(
        BassPattern pattern, Action changed, Action<int>? audition = null, BassGeneratorViewModel? harmony = null,
        PatternHistory? history = null)
    {
        _pattern = pattern;
        _changed = changed;
        _audition = audition;
        _history = history;
        Steps = new ObservableCollection<BassStepViewModel>(
            pattern.Steps.Select((step, i) => new BassStepViewModel(step, i + 1, OnStepChanged, audition, Select, history)));
        UpdateLength();
        SelectedStep = Steps[0];
        if (harmony is not null)
            Attach(harmony);
    }

    /// <summary>
    /// The bass generator whose key, scale and chords light the keyboard. Generate and Vary use this
    /// same key, so a line played by hand stays in the scale the keys show.
    /// </summary>
    public BassGeneratorViewModel? Harmony => _harmony;

    /// <summary>True when <see cref="Harmony"/> is lighting the keyboard.</summary>
    public bool ShowScale { get; private set; }

    /// <summary>Pitch classes in the scale, as bits 0–11.</summary>
    public int ScaleMask { get; private set; }

    /// <summary>Root, third and fifth of the chord at the selected step, as bits 0–11.</summary>
    public int ChordMask { get; private set; }

    /// <summary>Root, third and fifth of the chord at the selected step, in that order. Empty when no key is set.</summary>
    public string ChordLabel { get; private set; } = "";

    /// <summary>Follows the generator's key. Drop the subscription when this editor is replaced.</summary>
    public void Attach(BassGeneratorViewModel harmony)
    {
        Detach();
        _harmony = harmony;
        harmony.PropertyChanged += OnHarmonyChanged;
        OnPropertyChanged(nameof(Harmony));
        ShowColours = harmony.ShowColours;
        RefreshScale();
    }

    public void Detach()
    {
        if (_harmony is null)
            return;
        _harmony.PropertyChanged -= OnHarmonyChanged;
        _harmony = null;
        OnPropertyChanged(nameof(Harmony));
        RefreshScale();
    }

    /// <summary>Lowest and highest keys on the keyboard: the T-8's bass range.</summary>
    public static int LowestNote => BassStepViewModel.MinNote;
    public static int HighestNote => BassStepViewModel.MaxNote;

    /// <summary>The selected step's note, shown on the keyboard; null when the step is off.</summary>
    public int? SelectedNote => SelectedStep is { HasNote: true } step ? step.Note : null;

    public string SelectionLabel => SelectedStep is { } step
        ? $"Step {step.Number}: {(step.HasNote ? step.NoteName : "off")}{(step.State == BassStepState.Tie ? " (tie)" : "")}"
        : "No step selected";

    /// <summary>
    /// The note sounding at <see cref="PlayingStep"/>: its own note, the tied note before it for a
    /// tie, or null for an off step.
    /// </summary>
    public int? PlayingNote
    {
        get
        {
            if (PlayingStep is not { } step)
                return null;
            var index = step.Number - 1;
            for (var i = 0; i < Length; i++)
            {
                var candidate = Steps[((index - i) % Length + Length) % Length];
                if (candidate.State == BassStepState.Note)
                    return candidate.Note;
                if (candidate.State == BassStepState.Off)
                    return null;
            }
            return null;
        }
    }

    /// <summary>Highlights the step at a position counted from the start of playback; null clears it.</summary>
    public void ShowPlayingStep(long? position) =>
        PlayingStep = position is { } p ? Steps[(int)(p % Length)] : null;

    partial void OnPlayingStepChanged(BassStepViewModel? oldValue, BassStepViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsPlaying = false;
        if (newValue is not null)
            newValue.IsPlaying = true;
    }

    partial void OnSelectedStepChanged(BassStepViewModel? oldValue, BassStepViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsSelected = false;
        if (newValue is not null)
            newValue.IsSelected = true;
        RefreshScale();
    }

    partial void OnShowColoursChanged(bool value)
    {
        if (_harmony is not null && _harmony.ShowColours != value)
            _harmony.ShowColours = value;
        RefreshScale();
    }

    private void OnHarmonyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BassGeneratorViewModel.ShowColours))
        {
            if (ShowColours != _harmony!.ShowColours)
                ShowColours = _harmony.ShowColours;
            return;
        }
        if (e.PropertyName is nameof(BassGeneratorViewModel.Key)
            or nameof(BassGeneratorViewModel.Scale)
            or nameof(BassGeneratorViewModel.Progression)
            or nameof(BassGeneratorViewModel.StepsPerChord))
            RefreshScale();
    }

    /// <summary>Lights the scale, and the chord that covers the selected step, while colours are on.</summary>
    private void RefreshScale()
    {
        var show = _harmony is not null && ShowColours;
        var scaleMask = 0;
        var chordMask = 0;
        var label = "";
        if (_harmony is { } harmony)
        {
            var options = harmony.Options;
            var step = Math.Max(0, (SelectedStep?.Number ?? 1) - 1);
            var chord = BassGenerator.ChordAt(options.Root, options.Scale, options.Progression, options.StepsPerChord, step);
            scaleMask = BassGenerator.PitchClassMask(BassGenerator.ScaleClasses(options.Root, options.Scale));
            chordMask = BassGenerator.PitchClassMask(chord);
            label = string.Join(" · ", chord.Select(pitch => BassGenerator.KeyNames[pitch]));
        }

        if (ShowScale != show)
        {
            ShowScale = show;
            OnPropertyChanged(nameof(ShowScale));
        }
        if (ScaleMask != scaleMask)
        {
            ScaleMask = scaleMask;
            OnPropertyChanged(nameof(ScaleMask));
        }
        if (ChordMask != chordMask)
        {
            ChordMask = chordMask;
            OnPropertyChanged(nameof(ChordMask));
        }
        if (ChordLabel != label)
        {
            ChordLabel = label;
            OnPropertyChanged(nameof(ChordLabel));
        }
    }

    /// <summary>
    /// Plays a key on the keyboard: sets the selected step's note (turning it on if it was off or a
    /// tie), auditions it and, if <see cref="AdvanceAfterNote"/> is on, selects the next step.
    /// Without a selection the key is only auditioned.
    /// </summary>
    [RelayCommand]
    private void EnterNote(int note)
    {
        if (SelectedStep is not { } step)
        {
            _audition?.Invoke(note);
            return;
        }

        step.SetNote(note);
        if (AdvanceAfterNote)
            SelectNext();
    }

    [RelayCommand]
    private void SelectNext() => MoveSelection(1);

    [RelayCommand]
    private void SelectPrevious() => MoveSelection(-1);

    private void MoveSelection(int by)
    {
        var index = SelectedStep is null ? 0 : SelectedStep.Number - 1 + by;
        // Wrap within the pattern's length, like the T-8's step entry.
        SelectedStep = Steps[((index % Length) + Length) % Length];
    }

    private void Select(BassStepViewModel step) => SelectedStep = step;

    public ObservableCollection<BassStepViewModel> Steps { get; }

    internal BassPattern Pattern => _pattern;

    /// <summary>Re-reads every step after the pattern was changed outside the editor (generate, undo).</summary>
    public void Refresh()
    {
        foreach (var step in Steps)
            step.Refresh();
        UpdateLength();
        OnPropertyChanged(nameof(Length));
        OnPropertyChanged(nameof(Triplet));
        OnPropertyChanged(nameof(SelectedNote));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(PlayingNote));
    }

    public int Length
    {
        get => _pattern.Length;
        set
        {
            value = Math.Clamp(value, 1, BassPattern.StepCount);
            if (_pattern.Length == value)
                return;
            PatternHistory.Apply(_history, () =>
            {
                _pattern.Length = value;
                OnPropertyChanged(nameof(Length));
                UpdateLength();
                _changed();
            });
        }
    }

    public bool Triplet
    {
        get => _pattern.Triplet != 0;
        set
        {
            if (Triplet == value)
                return;
            PatternHistory.Apply(_history, () =>
            {
                _pattern.Triplet = value ? 1 : 0;
                OnPropertyChanged(nameof(Triplet));
                _changed();
            });
        }
    }

    /// <summary>Moves every note up or down by semitones, keeping them in the T-8 range.</summary>
    [RelayCommand]
    private void Transpose(int semitones) =>
        PatternHistory.Apply(_history, () =>
        {
            foreach (var step in Steps)
                step.Note += semitones;
        });

    private void UpdateLength()
    {
        foreach (var step in Steps)
            step.IsInLength = step.Number <= _pattern.Length;
    }

    private void OnStepChanged()
    {
        OnPropertyChanged(nameof(SelectedNote));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(PlayingNote));
        _changed();
    }
}

public sealed partial class BassStepViewModel : ObservableObject
{
    public const int MinNote = 12; // C0
    public const int MaxNote = 96; // C7

    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private readonly BassStep _step;
    private readonly Action _changed;
    private readonly Action<int>? _audition;
    private readonly Action<BassStepViewModel>? _select;
    private readonly PatternHistory? _history;

    [ObservableProperty]
    private bool _isInLength = true;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isPlaying;

    public BassStepViewModel(BassStep step, int number, Action changed, Action<int>? audition, Action<BassStepViewModel>? select = null,
        PatternHistory? history = null)
    {
        _step = step;
        Number = number;
        _changed = changed;
        _audition = audition;
        _select = select;
        _history = history;
    }

    internal void Refresh()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(Note));
        OnPropertyChanged(nameof(NoteName));
        OnPropertyChanged(nameof(Accent));
        OnPropertyChanged(nameof(Slide));
    }

    /// <summary>Note names for the step's right-click menu, one group per octave of the T-8's range.</summary>
    public static IReadOnlyList<(string Octave, IReadOnlyList<int> Notes)> NoteGroups { get; } =
        Enumerable.Range(MinNote, MaxNote - MinNote + 1)
            .GroupBy(n => n / 12 - 1)
            .Select(g => ($"Octave {g.Key}", (IReadOnlyList<int>)g.ToList()))
            .ToList();

    public int Number { get; }

    /// <summary>True on steps 1, 5, 9… so the view can mark beats.</summary>
    public bool IsBeat => (Number - 1) % 4 == 0;

    public BassStepState State
    {
        get => _step.State;
        set
        {
            if (_step.State == value)
                return;
            PatternHistory.Apply(_history, () =>
            {
                _step.State = value;
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(StateLabel));
                OnPropertyChanged(nameof(HasNote));
                _changed();
            });
        }
    }

    public string StateLabel => State switch
    {
        BassStepState.Note => "Note",
        BassStepState.Tie => "Tie",
        _ => "Off",
    };

    public bool HasNote => State != BassStepState.Off;

    public int Note
    {
        get => _step.Note;
        set
        {
            value = Math.Clamp(value, MinNote, MaxNote);
            if (_step.Note == value)
                return;
            PatternHistory.Apply(_history, () =>
            {
                _step.Note = value;
                OnPropertyChanged(nameof(Note));
                OnPropertyChanged(nameof(NoteName));
                _changed();
            });
        }
    }

    public string NoteName => FormatNote(Note);

    public bool Accent
    {
        get => _step.Accent;
        set
        {
            if (_step.Accent == value)
                return;
            PatternHistory.Apply(_history, () =>
            {
                _step.Accent = value;
                OnPropertyChanged(nameof(Accent));
                _changed();
            });
        }
    }

    public bool Slide
    {
        get => _step.Slide;
        set
        {
            if (_step.Slide == value)
                return;
            PatternHistory.Apply(_history, () =>
            {
                _step.Slide = value;
                OnPropertyChanged(nameof(Slide));
                _changed();
            });
        }
    }

    /// <summary>MIDI note to name as on the T-8 display: 12 = C0, 96 = C7.</summary>
    public static string FormatNote(int note) => $"{NoteNames[note % 12]}{note / 12 - 1}";

    /// <summary>Off → Note → Tie → Off. A new note is auditioned.</summary>
    [RelayCommand]
    private void CycleState()
    {
        State = State switch
        {
            BassStepState.Off => BassStepState.Note,
            BassStepState.Note => BassStepState.Tie,
            _ => BassStepState.Off,
        };
        if (State == BassStepState.Note)
            _audition?.Invoke(Note);
    }

    /// <summary>Selects this step, so the keyboard below edits it.</summary>
    [RelayCommand]
    private void Select() => _select?.Invoke(this);

    /// <summary>Sets the note and turns the step on (from off or tie), auditioning it unless told not to.</summary>
    public void SetNote(int note, bool audition = true) =>
        PatternHistory.Apply(_history, () =>
        {
            Note = note;
            State = BassStepState.Note;
            if (audition)
                _audition?.Invoke(Note);
        });

    [RelayCommand]
    private void SetState(BassStepState state)
    {
        State = state;
        if (state == BassStepState.Note)
            _audition?.Invoke(Note);
    }

    [RelayCommand]
    private void NoteUp() => ChangeNote(1);

    [RelayCommand]
    private void NoteDown() => ChangeNote(-1);

    [RelayCommand]
    private void OctaveUp() => ChangeNote(12);

    [RelayCommand]
    private void OctaveDown() => ChangeNote(-12);

    private void ChangeNote(int semitones) =>
        PatternHistory.Apply(_history, () =>
        {
            Note += semitones;
            if (State == BassStepState.Off)
                State = BassStepState.Note;
            _audition?.Invoke(Note);
        });
}
