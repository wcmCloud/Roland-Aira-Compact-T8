using System.Collections.ObjectModel;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedNote), nameof(SelectionLabel))]
    private BassStepViewModel? _selectedStep;

    /// <summary>After a key is played on the keyboard, move the selection to the next step.</summary>
    [ObservableProperty]
    private bool _advanceAfterNote = true;

    public BassEditorViewModel(BassPattern pattern, Action changed, Action<int>? audition = null)
    {
        _pattern = pattern;
        _changed = changed;
        _audition = audition;
        Steps = new ObservableCollection<BassStepViewModel>(
            pattern.Steps.Select((step, i) => new BassStepViewModel(step, i + 1, OnStepChanged, audition, Select)));
        UpdateLength();
        SelectedStep = Steps[0];
    }

    /// <summary>Lowest and highest keys on the keyboard: the T-8's bass range.</summary>
    public static int LowestNote => BassStepViewModel.MinNote;
    public static int HighestNote => BassStepViewModel.MaxNote;

    /// <summary>The selected step's note, shown on the keyboard; null when the step is off.</summary>
    public int? SelectedNote => SelectedStep is { HasNote: true } step ? step.Note : null;

    public string SelectionLabel => SelectedStep is { } step
        ? $"Step {step.Number}: {(step.HasNote ? step.NoteName : "off")}{(step.State == BassStepState.Tie ? " (tie)" : "")}"
        : "No step selected";

    partial void OnSelectedStepChanged(BassStepViewModel? oldValue, BassStepViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsSelected = false;
        if (newValue is not null)
            newValue.IsSelected = true;
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

    public int Length
    {
        get => _pattern.Length;
        set
        {
            value = Math.Clamp(value, 1, BassPattern.StepCount);
            if (_pattern.Length == value)
                return;
            _pattern.Length = value;
            OnPropertyChanged();
            UpdateLength();
            _changed();
        }
    }

    public bool Triplet
    {
        get => _pattern.Triplet != 0;
        set
        {
            if (Triplet == value)
                return;
            _pattern.Triplet = value ? 1 : 0;
            OnPropertyChanged();
            _changed();
        }
    }

    /// <summary>Moves every note up or down by semitones, keeping them in the T-8 range.</summary>
    [RelayCommand]
    private void Transpose(int semitones)
    {
        foreach (var step in Steps)
            step.Note += semitones;
    }

    private void UpdateLength()
    {
        foreach (var step in Steps)
            step.IsInLength = step.Number <= _pattern.Length;
    }

    private void OnStepChanged()
    {
        OnPropertyChanged(nameof(SelectedNote));
        OnPropertyChanged(nameof(SelectionLabel));
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

    [ObservableProperty]
    private bool _isInLength = true;

    [ObservableProperty]
    private bool _isSelected;

    public BassStepViewModel(BassStep step, int number, Action changed, Action<int>? audition, Action<BassStepViewModel>? select = null)
    {
        _step = step;
        Number = number;
        _changed = changed;
        _audition = audition;
        _select = select;
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
            _step.State = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StateLabel));
            OnPropertyChanged(nameof(HasNote));
            _changed();
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
            _step.Note = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NoteName));
            _changed();
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
            _step.Accent = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public bool Slide
    {
        get => _step.Slide;
        set
        {
            if (_step.Slide == value)
                return;
            _step.Slide = value;
            OnPropertyChanged();
            _changed();
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

    /// <summary>Sets the note and turns the step on (from off or tie), auditioning it.</summary>
    public void SetNote(int note)
    {
        Note = note;
        State = BassStepState.Note;
        _audition?.Invoke(Note);
    }

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

    private void ChangeNote(int semitones)
    {
        Note += semitones;
        if (State == BassStepState.Off)
            State = BassStepState.Note;
        _audition?.Invoke(Note);
    }
}
