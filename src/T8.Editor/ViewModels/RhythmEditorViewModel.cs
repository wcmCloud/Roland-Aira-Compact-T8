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

    /// <param name="clipboard">Shared between patterns, so a row can be pasted into another pattern.</param>
    public RhythmEditorViewModel(RhythmPattern pattern, Action changed, Action<RhythmTrack>? audition = null, RhythmTrackClipboard? clipboard = null)
    {
        _pattern = pattern;
        _changed = changed;
        Clipboard = clipboard ?? new RhythmTrackClipboard();
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

    // ---- Rows: select, copy, paste, clear --------------------------------------------------

    public RhythmTrackClipboard Clipboard { get; }

    /// <summary>The row whose name was clicked; Cmd/Ctrl+C and Cmd/Ctrl+V copy and paste it.</summary>
    [ObservableProperty]
    private RhythmTrackViewModel? _selectedTrack;

    partial void OnSelectedTrackChanged(RhythmTrackViewModel? oldValue, RhythmTrackViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsSelected = false;
        if (newValue is not null)
            newValue.IsSelected = true;
    }

    /// <summary>Clicking a row's name selects it (click again to deselect).</summary>
    [RelayCommand]
    private void SelectTrack(RhythmTrackViewModel track) => SelectedTrack = SelectedTrack == track ? null : track;

    /// <summary>Copies every step of a row (on/off, velocity, probability, sub step).</summary>
    [RelayCommand]
    private void CopyTrack(RhythmTrackViewModel track)
    {
        SelectedTrack = track;
        Clipboard.Copy(track.Name, track.Cells.Select(c => c.Cell));
    }

    /// <summary>Pastes the copied row over a row: every step is replaced. Does nothing until a row is copied.</summary>
    [RelayCommand]
    private void PasteTrack(RhythmTrackViewModel track)
    {
        if (Clipboard.Cells is not { } cells)
            return;
        SelectedTrack = track;
        for (var i = 0; i < track.Cells.Count && i < cells.Count; i++)
            track.Cells[i].SetFrom(cells[i]);
        _changed();
    }

    [RelayCommand]
    private void ClearTrack(RhythmTrackViewModel track)
    {
        foreach (var cell in track.Cells)
            cell.On = false;
    }

    /// <summary>Cmd/Ctrl+C: copies the selected row.</summary>
    [RelayCommand]
    private void CopySelectedTrack()
    {
        if (SelectedTrack is { } track)
            CopyTrack(track);
    }

    /// <summary>Cmd/Ctrl+V: pastes into the selected row.</summary>
    [RelayCommand]
    private void PasteToSelectedTrack()
    {
        if (SelectedTrack is { } track && Clipboard.HasContent)
            PasteTrack(track);
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

    // ---- Multi-selection: set velocity, probability and sub step for many active steps at once ----

    /// <summary>Active cells picked with Ctrl/Cmd/Shift-click, edited together with <see cref="ApplyToSelectionCommand"/>.</summary>
    public ObservableCollection<RhythmCellViewModel> MultiSelection { get; } = new();

    public bool HasMultiSelection => MultiSelection.Count > 0;

    public string MultiSelectionLabel => MultiSelection.Count == 1 ? "1 step selected" : $"{MultiSelection.Count} steps selected";

    [ObservableProperty]
    private int _batchVelocity = RhythmCell.DefaultNoteVelocity;

    [ObservableProperty]
    private int _batchProbabilityPercent = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchHasSubStep))]
    private SubStep _batchSubStep;

    [ObservableProperty]
    private int _batchSubStepProbabilityPercent = 100;

    public bool BatchHasSubStep => BatchSubStep != SubStep.Off;

    partial void OnBatchVelocityChanged(int value) => BatchVelocity = Math.Clamp(value, 0, 10);
    partial void OnBatchProbabilityPercentChanged(int value) => BatchProbabilityPercent = Math.Clamp(value, 0, 100);
    partial void OnBatchSubStepProbabilityPercentChanged(int value) => BatchSubStepProbabilityPercent = Math.Clamp(value, 0, 100);

    /// <summary>
    /// Adds an active cell to the multi-selection, or takes it out if it's already in. Inactive cells
    /// can't be selected. The first cell picked sets the starting values of the batch controls.
    /// </summary>
    public void ToggleMultiSelect(RhythmCellViewModel cell)
    {
        if (cell.IsMultiSelected)
        {
            cell.IsMultiSelected = false;
            MultiSelection.Remove(cell);
        }
        else if (cell.On)
        {
            if (MultiSelection.Count == 0)
            {
                BatchVelocity = cell.Velocity;
                BatchProbabilityPercent = cell.ProbabilityPercent;
                BatchSubStep = cell.SubStep;
                BatchSubStepProbabilityPercent = cell.SubStepProbabilityPercent;
            }
            cell.IsMultiSelected = true;
            MultiSelection.Add(cell);
        }
        OnMultiSelectionChanged();
    }

    /// <summary>Selects every active step of every track that has a sound.</summary>
    [RelayCommand]
    private void SelectAllActive()
    {
        foreach (var cell in Tracks.SelectMany(t => t.Cells).Where(c => c.On && c.IsInLength && !c.IsMultiSelected))
            ToggleMultiSelect(cell);
    }

    /// <summary>Sets velocity, probability and sub step on every selected step, then clears the selection.</summary>
    [RelayCommand]
    private void ApplyToSelection()
    {
        foreach (var cell in MultiSelection)
        {
            cell.Velocity = BatchVelocity;
            cell.ProbabilityPercent = BatchProbabilityPercent;
            cell.SubStep = BatchSubStep;
            cell.SubStepProbabilityPercent = BatchSubStepProbabilityPercent;
        }
        ClearMultiSelection();
    }

    [RelayCommand]
    private void ClearMultiSelection()
    {
        foreach (var cell in MultiSelection)
            cell.IsMultiSelected = false;
        MultiSelection.Clear();
        OnMultiSelectionChanged();
    }

    /// <summary>A cell was switched off: it can't stay in the multi-selection.</summary>
    internal void OnCellTurnedOff(RhythmCellViewModel cell)
    {
        if (!cell.IsMultiSelected)
            return;
        cell.IsMultiSelected = false;
        MultiSelection.Remove(cell);
        OnMultiSelectionChanged();
    }

    private void OnMultiSelectionChanged()
    {
        OnPropertyChanged(nameof(HasMultiSelection));
        OnPropertyChanged(nameof(MultiSelectionLabel));
    }

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

public sealed partial class RhythmTrackViewModel : ObservableObject
{
    /// <summary>Selected by clicking the row's name.</summary>
    [ObservableProperty]
    private bool _isSelected;

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

    /// <summary>Part of the multi-selection (Ctrl/Cmd/Shift-click).</summary>
    [ObservableProperty]
    private bool _isMultiSelected;

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
            if (!value)
                _editor.OnCellTurnedOff(this);
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

    internal RhythmCell Cell => _cell;

    /// <summary>Takes all the values of a copied step.</summary>
    internal void SetFrom(RhythmCell from)
    {
        _cell.On = from.On;
        _cell.Velocity = from.Velocity;
        _cell.SubStep = from.SubStep;
        _cell.Probability = from.Probability;
        _cell.SubStepProbability = from.SubStepProbability;
        if (!from.On)
            _editor.OnCellTurnedOff(this);
        Refresh();
    }

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

/// <summary>A copied rhythm row: the 32 steps of one track, shared between patterns.</summary>
public sealed partial class RhythmTrackClipboard : ObservableObject
{
    public IReadOnlyList<RhythmCell>? Cells { get; private set; }

    /// <summary>Name of the row it was copied from.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasteTip))]
    private string? _source;

    public bool HasContent => Cells is not null;

    public string PasteTip => Source is null ? "Paste a copied row here (copy one first)" : $"Paste the copied {Source} row here";

    public void Copy(string source, IEnumerable<RhythmCell> cells)
    {
        Cells = cells.Select(c => c.Clone()).ToList();
        Source = source;
        OnPropertyChanged(nameof(HasContent));
    }
}
