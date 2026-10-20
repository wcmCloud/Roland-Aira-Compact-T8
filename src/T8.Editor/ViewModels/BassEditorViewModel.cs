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

    public BassEditorViewModel(BassPattern pattern, Action changed, Action<int>? audition = null)
    {
        _pattern = pattern;
        _changed = changed;
        Steps = new ObservableCollection<BassStepViewModel>(
            pattern.Steps.Select((step, i) => new BassStepViewModel(step, i + 1, OnStepChanged, audition)));
        UpdateLength();
    }

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

    private void OnStepChanged() => _changed();
}

public sealed partial class BassStepViewModel : ObservableObject
{
    public const int MinNote = 12; // C0
    public const int MaxNote = 96; // C7

    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private readonly BassStep _step;
    private readonly Action _changed;
    private readonly Action<int>? _audition;

    [ObservableProperty]
    private bool _isInLength = true;

    public BassStepViewModel(BassStep step, int number, Action changed, Action<int>? audition)
    {
        _step = step;
        Number = number;
        _changed = changed;
        _audition = audition;
    }

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
