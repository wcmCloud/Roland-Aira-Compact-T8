using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;

namespace T8.Editor.ViewModels;

/// <summary>Edits one rhythm pattern in place: an 8-track × 32-step grid plus a cell inspector.</summary>
public sealed partial class RhythmEditorViewModel : ObservableObject
{
    private readonly RhythmPattern _pattern;
    private readonly Action _changed;

    [ObservableProperty]
    private RhythmCellViewModel? _selectedCell;

    public RhythmEditorViewModel(RhythmPattern pattern, Action changed, Action<RhythmTrack>? audition = null)
    {
        _pattern = pattern;
        _changed = changed;
        _allTracks = Enum.GetValues<RhythmTrack>()
            .Select(track => new RhythmTrackViewModel(track, pattern, this, changed, audition))
            .ToList();
        Tracks = new ObservableCollection<RhythmTrackViewModel>(_allTracks.Where(t => t.Track != RhythmTrack.CY));
        UpdateLength();
    }

    /// <summary>All eight tracks in file order, including CY.</summary>
    private readonly List<RhythmTrackViewModel> _allTracks;

    /// <summary>
    /// The tracks shown in the grid: all but CY, a cymbal slot in the T-8's files with no sound on the
    /// T-8. CY's steps are still kept in the pattern, so files load and save unchanged.
    /// </summary>
    public ObservableCollection<RhythmTrackViewModel> Tracks { get; }

    public static IReadOnlyList<SubStep> SubStepOptions { get; } = Enum.GetValues<SubStep>();

    internal RhythmPattern Pattern => _pattern;

    /// <summary>Re-reads every cell after the pattern was changed outside the editor (generate, undo).</summary>
    public void Refresh()
    {
        foreach (var track in _allTracks)
        {
            foreach (var cell in track.Cells)
                cell.Refresh();
        }
        UpdateLength();
        OnPropertyChanged(nameof(Length));
        OnPropertyChanged(nameof(Shuffle));
        OnPropertyChanged(nameof(Flam));
    }

    public int Length
    {
        get => _pattern.Length;
        set
        {
            value = Math.Clamp(value, 1, RhythmPattern.StepCount);
            if (_pattern.Length == value)
                return;
            _pattern.Length = value;
            OnPropertyChanged();
            UpdateLength();
            _changed();
        }
    }

    public int Shuffle
    {
        get => _pattern.Shuffle;
        set => SetPatternValue(_pattern.Shuffle, Math.Clamp(value, 0, 200), v => _pattern.Shuffle = v);
    }

    public int Flam
    {
        get => _pattern.Flam;
        set => SetPatternValue(_pattern.Flam, Math.Clamp(value, 0, 100), v => _pattern.Flam = v);
    }

    public RhythmTrackViewModel this[RhythmTrack track] => _allTracks[(int)track];

    [RelayCommand]
    private void ClearTrack(RhythmTrackViewModel track)
    {
        foreach (var cell in track.Cells)
            cell.On = false;
    }

    private int? _playingStep;

    /// <summary>Highlights the step column at a position counted from the start of playback; null clears it.</summary>
    public void ShowPlayingStep(long? position)
    {
        int? step = position is { } p ? (int)(p % Length) : null;
        if (step == _playingStep)
            return;
        foreach (var track in Tracks)
        {
            if (_playingStep is { } old)
                track.Cells[old].IsPlaying = false;
            if (step is { } now)
                track.Cells[now].IsPlaying = true;
        }
        _playingStep = step;
        OnPropertyChanged(nameof(PlayingStepNumber));
    }

    /// <summary>1-based number of the step being played, or null when stopped.</summary>
    public int? PlayingStepNumber => _playingStep + 1;

    internal void Select(RhythmCellViewModel cell)
    {
        if (SelectedCell is not null)
            SelectedCell.IsSelected = false;
        SelectedCell = cell;
        cell.IsSelected = true;
    }

    private void SetPatternValue(int current, int value, Action<int> set, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (current == value)
            return;
        set(value);
        OnPropertyChanged(name);
        _changed();
    }

    private void UpdateLength()
    {
        foreach (var track in Tracks)
        {
            foreach (var cell in track.Cells)
                cell.IsInLength = cell.StepNumber <= _pattern.Length;
        }
    }
}

public sealed class RhythmTrackViewModel
{
    public RhythmTrackViewModel(RhythmTrack track, RhythmPattern pattern, RhythmEditorViewModel editor, Action changed, Action<RhythmTrack>? audition)
    {
        Track = track;
        Cells = new ObservableCollection<RhythmCellViewModel>(
            pattern.Steps.Select((step, i) => new RhythmCellViewModel(step[track], track, i + 1, editor, changed, audition)));
    }

    public RhythmTrack Track { get; }

    public string Name => Track switch
    {
        RhythmTrack.AC => "Accent",
        RhythmTrack.BD => "Bass drum",
        RhythmTrack.SD => "Snare",
        RhythmTrack.LT => "Tom",
        RhythmTrack.HT => "Clap",
        RhythmTrack.CY => "CY (no sound)",
        RhythmTrack.CH => "Closed hat",
        RhythmTrack.OH => "Open hat",
        _ => Track.ToString(),
    };

    public ObservableCollection<RhythmCellViewModel> Cells { get; }
}

public sealed partial class RhythmCellViewModel : ObservableObject
{
    private readonly RhythmCell _cell;
    private readonly RhythmEditorViewModel _editor;
    private readonly Action _changed;
    private readonly Action<RhythmTrack>? _audition;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isInLength = true;

    [ObservableProperty]
    private bool _isPlaying;

    public RhythmCellViewModel(RhythmCell cell, RhythmTrack track, int stepNumber, RhythmEditorViewModel editor, Action changed, Action<RhythmTrack>? audition)
    {
        _cell = cell;
        Track = track;
        StepNumber = stepNumber;
        _editor = editor;
        _changed = changed;
        _audition = audition;
    }

    public RhythmTrack Track { get; }
    public int StepNumber { get; }
    public bool IsBeat => (StepNumber - 1) % 4 == 0;

    public bool On
    {
        get => _cell.On;
        set
        {
            if (_cell.On == value)
                return;
            _cell.On = value;
            // Blank cells store velocity 0; a newly entered note gets the T-8's default.
            if (value && _cell.Velocity == 0)
            {
                _cell.Velocity = RhythmCell.DefaultNoteVelocity;
                OnPropertyChanged(nameof(Velocity));
            }
            OnPropertyChanged();
            _changed();
        }
    }

    public int Velocity
    {
        get => _cell.Velocity;
        set => Set(_cell.Velocity, Math.Clamp(value, 0, 10), v => _cell.Velocity = v);
    }

    public SubStep SubStep
    {
        get => _cell.SubStep;
        set
        {
            if (_cell.SubStep == value)
                return;
            _cell.SubStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSubStep));
            _changed();
        }
    }

    public bool HasSubStep => SubStep != SubStep.Off;

    /// <summary>0–100 %, stored in tens.</summary>
    public int ProbabilityPercent
    {
        get => _cell.Probability * 10;
        set => Set(_cell.Probability, Math.Clamp((value + 5) / 10, 0, 10), v => _cell.Probability = v);
    }

    /// <summary>0–100 %, stored in tens.</summary>
    public int SubStepProbabilityPercent
    {
        get => _cell.SubStepProbability * 10;
        set => Set(_cell.SubStepProbability, Math.Clamp((value + 5) / 10, 0, 10), v => _cell.SubStepProbability = v);
    }

    public string Description => $"{Track} step {StepNumber}";

    internal void Refresh()
    {
        OnPropertyChanged(nameof(On));
        OnPropertyChanged(nameof(Velocity));
        OnPropertyChanged(nameof(SubStep));
        OnPropertyChanged(nameof(HasSubStep));
        OnPropertyChanged(nameof(ProbabilityPercent));
        OnPropertyChanged(nameof(SubStepProbabilityPercent));
    }

    /// <summary>Called when the cell is clicked: the click itself toggles On through the binding.</summary>
    [RelayCommand]
    private void Activate()
    {
        _editor.Select(this);
        if (On && Track is not (RhythmTrack.AC or RhythmTrack.CY))
            _audition?.Invoke(Track);
    }

    private void Set(int current, int value, Action<int> set, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (current == value)
            return;
        set(value);
        OnPropertyChanged(name);
        _changed();
    }
}
