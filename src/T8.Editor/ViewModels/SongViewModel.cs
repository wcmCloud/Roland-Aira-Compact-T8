using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;
using T8.Core.Songs;
using T8.Editor.Services;
using T8.Midi;

namespace T8.Editor.ViewModels;

/// <summary>What the Song tab needs from the main window.</summary>
public interface ISongHost
{
    PatternLibrary Library { get; }
    int SelectedBank { get; }
    int SelectedPattern { get; }
    double Bpm { get; set; }
    EditorSettings Settings { get; }

    /// <summary>The open T-8 output, or null (with the reason in the status bar) if there is none.</summary>
    T8Controller? Controller();

    /// <summary>Stops the pattern playback of the Rhythm and Bass tabs, so the song can play.</summary>
    void StopPatternPlayback();

    void ShowStatus(string text);
}

/// <summary>
/// The Song tab: a list of patterns from the banks, each repeated and optionally followed by a fill
/// as a bridge, played in order through the T-8's sounds or on the T-8's own sequencer.
/// </summary>
public sealed partial class SongViewModel : ObservableObject
{
    public static readonly IReadOnlyList<string> Palette =
        ["#FF7A1A", "#5B8DEF", "#4CD38A", "#FF5C8A", "#B57BFF", "#FFD23F", "#3EC8C8", "#E8E8E8"];

    private readonly ISongHost _host;
    private readonly IDialogService _dialogs;
    private readonly Stopwatch _time = new();
    private Song _song = new();
    private SongPlan? _plan;
    private SongSyncDriver? _sync;
    private T8Controller? _player;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileLabel))]
    private string? _filePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileLabel))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(PreviousCommand), nameof(NextCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    private string _currentText = "—";

    [ObservableProperty]
    private string _nextText = "—";

    [ObservableProperty]
    private string _timeText = "00:00";

    [ObservableProperty]
    private string _positionText = "";

    /// <summary>How far through the current entry (repeats and fill), 0–1.</summary>
    [ObservableProperty]
    private double _entryProgress;

    /// <summary>How far through the whole song, 0–1.</summary>
    [ObservableProperty]
    private double _songProgress;

    [ObservableProperty]
    private Choice<int>? _loopChoice;

    public SongViewModel(ISongHost host, IDialogService dialogs)
    {
        _host = host;
        _dialogs = dialogs;
        LoadEntries();
    }

    public ObservableCollection<SongEntryViewModel> Entries { get; } = new();

    /// <summary>The coloured blocks of the timeline, one per entry plus one per fill.</summary>
    public ObservableCollection<TimelineBlock> Timeline { get; } = new();

    /// <summary>"Whole song", or loop a single entry.</summary>
    public ObservableCollection<Choice<int>> LoopChoices { get; } = new();

    public static IReadOnlyList<int> Banks { get; } = Enumerable.Range(1, PatternLibrary.BankCount).ToList();
    public static IReadOnlyList<int> Patterns { get; } = Enumerable.Range(1, PatternLibrary.PatternsPerBank).ToList();

    public string Title
    {
        get => _song.Title;
        set
        {
            if (_song.Title == value)
                return;
            _song.Title = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public bool Loop
    {
        get => _song.Loop;
        set
        {
            if (_song.Loop == value)
                return;
            _song.Loop = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public string FileLabel => (FilePath is null ? "Not saved" : Path.GetFileName(FilePath)) + (IsDirty ? " *" : "");

    public Song Song => _song;

    public bool IsEmpty => Entries.Count == 0;

    // ---- Editing -------------------------------------------------------------------------

    /// <summary>Adds the pattern selected in the pattern grid.</summary>
    [RelayCommand]
    private void AddCurrentPattern() => AddEntry(_host.SelectedBank, _host.SelectedPattern);

    public SongEntryViewModel AddEntry(int bank, int pattern)
    {
        var entry = new SongEntry
        {
            Bank = bank,
            Pattern = pattern,
            Name = $"Bank {bank} - Pattern {pattern}",
            Color = Palette[Entries.Count % Palette.Count],
        };
        _song.Entries.Add(entry);
        var vm = CreateEntry(entry);
        Entries.Add(vm);
        Changed();
        return vm;
    }

    internal void Remove(SongEntryViewModel entry)
    {
        var index = Entries.IndexOf(entry);
        if (index < 0)
            return;
        Stop();
        Entries.RemoveAt(index);
        _song.Entries.RemoveAt(index);
        Changed();
    }

    internal void Move(SongEntryViewModel entry, int by)
    {
        var index = Entries.IndexOf(entry);
        var target = index + by;
        if (index < 0 || target < 0 || target >= Entries.Count)
            return;
        Stop();
        Entries.Move(index, target);
        _song.Entries.RemoveAt(index);
        _song.Entries.Insert(target, entry.Model);
        Changed();
    }

    internal void Duplicate(SongEntryViewModel entry)
    {
        var index = Entries.IndexOf(entry);
        if (index < 0)
            return;
        var copy = entry.Model.Clone();
        _song.Entries.Insert(index + 1, copy);
        Entries.Insert(index + 1, CreateEntry(copy));
        Changed();
    }

    private SongEntryViewModel CreateEntry(SongEntry entry) => new(entry, this, _host);

    /// <summary>Called by an entry when it changes.</summary>
    internal void Changed()
    {
        for (var i = 0; i < Entries.Count; i++)
            Entries[i].Number = i + 1;
        OnPropertyChanged(nameof(IsEmpty));
        RebuildLoopChoices();
        RebuildTimeline();
        PlayCommand.NotifyCanExecuteChanged();
        MarkDirty();
    }

    private void MarkDirty() => IsDirty = true;

    /// <summary>Pattern lengths may have changed (a new library, an edited last step).</summary>
    public void RefreshSteps()
    {
        foreach (var entry in Entries)
            entry.RefreshSteps();
        RebuildTimeline();
    }

    private void RebuildLoopChoices()
    {
        var selected = LoopChoice?.Value ?? -1;
        LoopChoices.Clear();
        LoopChoices.Add(new Choice<int>(-1, "Whole song"));
        for (var i = 0; i < Entries.Count; i++)
            LoopChoices.Add(new Choice<int>(i, $"#{i + 1}: {Entries[i].SlotLabel} {Entries[i].Name}".TrimEnd()));
        LoopChoice = LoopChoices.FirstOrDefault(c => c.Value == selected) ?? LoopChoices[0];
    }

    private void RebuildTimeline()
    {
        Timeline.Clear();
        var plan = SongPlan.Create(_song, _host.Library);
        if (plan.TotalSteps == 0)
            return;
        foreach (var group in plan.Passes.GroupBy(p => (p.Entry, p.IsFill)))
        {
            var entry = Entries[group.Key.Entry];
            var steps = group.Sum(p => p.Steps);
            Timeline.Add(new TimelineBlock(
                group.Key.Entry,
                group.Key.IsFill,
                (double)steps / plan.TotalSteps,
                entry.Color,
                group.Key.IsFill ? "FILL" : entry.Model.Repeats > 1 ? $"{entry.Name} ×{entry.Model.Repeats}" : entry.Name));
        }
    }

    // ---- Files ---------------------------------------------------------------------------

    [RelayCommand]
    private async Task NewSongAsync()
    {
        if (IsDirty && !IsEmpty && !await _dialogs.ConfirmAsync("New song", "Discard the changes to this song?", "Discard"))
            return;
        Stop();
        _song = new Song();
        FilePath = null;
        LoadEntries();
        IsDirty = false;
        _host.ShowStatus("New song.");
    }

    [RelayCommand]
    private async Task OpenSongAsync()
    {
        if (IsDirty && !IsEmpty && !await _dialogs.ConfirmAsync("Open song", "Discard the changes to this song?", "Discard"))
            return;
        if (await _dialogs.PickSongToOpenAsync() is { } path)
            OpenSong(path);
    }

    public bool OpenSong(string path)
    {
        try
        {
            var song = Song.Load(path);
            Stop();
            _song = song;
            FilePath = path;
            LoadEntries();
            if (song.Bpm is { } bpm)
                _host.Bpm = bpm;
            IsDirty = false;
            _host.ShowStatus($"Opened song \"{song.Title}\" ({song.Entries.Count} entries).");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            _host.ShowStatus($"Couldn't open the song: {ex.Message}");
            return false;
        }
    }

    [RelayCommand]
    private async Task SaveSongAsync()
    {
        if (FilePath is null)
            await SaveSongAsAsync();
        else
            SaveSong(FilePath);
    }

    [RelayCommand]
    private async Task SaveSongAsAsync()
    {
        var name = string.Concat(_song.Title.Split(Path.GetInvalidFileNameChars())).Trim();
        if (await _dialogs.PickSongToSaveAsync(name.Length > 0 ? name : "Song") is { } path)
            SaveSong(path);
    }

    public bool SaveSong(string path)
    {
        try
        {
            _song.Bpm = _host.Bpm;
            _song.Save(path);
            FilePath = path;
            IsDirty = false;
            _host.ShowStatus($"Saved song to {path}.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _host.ShowStatus($"Couldn't save the song: {ex.Message}");
            return false;
        }
    }

    private void LoadEntries()
    {
        Entries.Clear();
        foreach (var entry in _song.Entries)
            Entries.Add(CreateEntry(entry));
        for (var i = 0; i < Entries.Count; i++)
            Entries[i].Number = i + 1;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Loop));
        OnPropertyChanged(nameof(IsEmpty));
        RebuildLoopChoices();
        RebuildTimeline();
        PlayCommand.NotifyCanExecuteChanged();
    }

    // ---- Playing -------------------------------------------------------------------------

    private bool CanPlay() => !IsPlaying && Entries.Count > 0;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play() => PlayFrom(0);

    /// <summary>Plays from the start of a song entry (in the whole song, or the looped entry).</summary>
    public void PlayFrom(int entry)
    {
        var errors = _song.Validate();
        if (errors.Count > 0)
        {
            _host.ShowStatus(errors[0]);
            return;
        }
        _host.StopPatternPlayback();
        StopPlayers();
        if (_host.Controller() is not { } t8)
            return;

        var only = LoopChoice is { Value: >= 0 } choice ? choice.Value : (int?)null;
        _plan = SongPlan.Create(_song, _host.Library, only);
        if (_plan.TotalSteps == 0)
            return;
        var loop = only is not null || _song.Loop;
        var fromPass = Math.Max(0, _plan.FirstPassOf(only ?? entry));
        _player = t8;
        try
        {
            if (_host.Settings.PlayMode == PlayMode.SyncT8Sequencer)
            {
                _sync = new SongSyncDriver(t8, _plan, loop, fromPass);
                _sync.Start(_host.Bpm);
            }
            else
            {
                t8.PlaySong(_plan, _host.Library, _host.Bpm, loop, fromPass);
            }
        }
        catch (Exception ex)
        {
            _host.ShowStatus($"Couldn't play the song: {ex.Message}");
            StopPlayers();
            return;
        }

        _time.Restart();
        IsPlaying = true;
        _host.ShowStatus(_host.Settings.PlayMode == PlayMode.SyncT8Sequencer
            ? $"Playing \"{Title}\" on the T-8's sequencer (needs rXPc on and SYnC set to AUTO or USB)."
            : $"Playing \"{Title}\" through the T-8's sounds.");
        UpdatePosition();
    }

    [RelayCommand]
    public void Stop()
    {
        if (!IsPlaying && _sync is null && _player is null)
            return;
        StopPlayers();
        IsPlaying = false;
        _time.Reset();
        foreach (var entry in Entries)
            entry.IsCurrent = false;
        foreach (var block in Timeline)
            block.Progress = 0;
        EntryProgress = 0;
        SongProgress = 0;
        CurrentText = "—";
        NextText = "—";
        PositionText = "";
        TimeText = "00:00";
    }

    private void StopPlayers()
    {
        _sync?.Dispose();
        _sync = null;
        _player?.StopPreview();
        _player?.Clock.Stop();
        _player = null;
    }

    private bool CanStep() => IsPlaying;

    [RelayCommand(CanExecute = nameof(CanStep))]
    private void Previous() => Jump(-1);

    [RelayCommand(CanExecute = nameof(CanStep))]
    private void Next() => Jump(1);

    private void Jump(int by)
    {
        if (_plan is null || CurrentPass() is not { } pass)
            return;
        var target = Math.Clamp(pass.Entry + by, 0, Entries.Count - 1);
        if (LoopChoice is { Value: >= 0 })
            return; // looping one entry: nothing to jump to
        PlayFrom(target);
    }

    private SongPass? CurrentPass()
    {
        if (_plan is null || CurrentStep() is not { } step)
            return null;
        var index = _plan.PassAt(step);
        return index < 0 ? null : _plan.Passes[index];
    }

    private long? CurrentStep() => _sync is not null ? _sync.CurrentStep : _player?.CurrentStep;

    /// <summary>Moves the highlights, progress and texts along; the main window calls this on a timer.</summary>
    public void UpdatePosition()
    {
        if (!IsPlaying || _plan is null)
            return;

        var finished = _sync?.Finished ?? _player?.PreviewFinished ?? true;
        if (finished)
        {
            Stop();
            _host.ShowStatus($"Song \"{Title}\" finished.");
            return;
        }

        TimeText = _time.Elapsed.ToString(_time.Elapsed.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");
        if (CurrentStep() is not { } step)
            return;
        var passIndex = _plan.PassAt(step);
        var pass = _plan.Passes[passIndex];
        var inSong = step % _plan.TotalSteps;

        for (var i = 0; i < Entries.Count; i++)
            Entries[i].IsCurrent = i == pass.Entry;

        var entry = Entries[pass.Entry];
        var entryPasses = _plan.Passes.Where(p => p.Entry == pass.Entry).ToList();
        var entryStart = entryPasses[0].StartStep;
        var entrySteps = entryPasses.Sum(p => p.Steps);
        EntryProgress = Math.Clamp((double)(inSong - entryStart) / entrySteps, 0, 1);
        SongProgress = Math.Clamp((double)inSong / _plan.TotalSteps, 0, 1);

        foreach (var block in Timeline)
        {
            var blockPasses = _plan.Passes.Where(p => p.Entry == block.Entry && p.IsFill == block.IsFill).ToList();
            if (blockPasses.Count == 0)
            {
                block.Progress = 0;
                continue;
            }
            var start = blockPasses[0].StartStep;
            var steps = blockPasses.Sum(p => p.Steps);
            block.Progress = Math.Clamp((double)(inSong - start) / steps, 0, 1);
        }

        CurrentText = pass.IsFill
            ? $"Fill: bank {pass.Bank} pattern {pass.Pattern} → next"
            : $"{entry.SlotLabel} {entry.Name}  ({pass.Repeat}/{entry.Model.Repeats})";
        var nextIndex = passIndex + 1 < _plan.Passes.Count ? passIndex + 1 : (_song.Loop || LoopChoice is { Value: >= 0 }) ? 0 : -1;
        NextText = nextIndex < 0
            ? "End of song"
            : _plan.Passes[nextIndex] is var next && next.IsFill
                ? $"Fill: bank {next.Bank} pattern {next.Pattern}"
                : $"{Entries[_plan.Passes[nextIndex].Entry].SlotLabel} {Entries[_plan.Passes[nextIndex].Entry].Name}".Trim();
        PositionText = $"Step {inSong - pass.StartStep + 1} / {pass.Steps} · {_host.Bpm:0} BPM";
    }
}

/// <summary>One line of the song in the Song tab.</summary>
public sealed partial class SongEntryViewModel : ObservableObject
{
    private readonly SongViewModel _song;
    private readonly ISongHost _host;

    [ObservableProperty]
    private int _number;

    /// <summary>The entry playing now.</summary>
    [ObservableProperty]
    private bool _isCurrent;

    public SongEntryViewModel(SongEntry model, SongViewModel song, ISongHost host)
    {
        Model = model;
        _song = song;
        _host = host;
    }

    public SongEntry Model { get; }

    public int Bank
    {
        get => Model.Bank;
        set => Set(Model.Bank, Math.Clamp(value, 1, PatternLibrary.BankCount), v => Model.Bank = v, steps: true);
    }

    public int Pattern
    {
        get => Model.Pattern;
        set => Set(Model.Pattern, Math.Clamp(value, 1, PatternLibrary.PatternsPerBank), v => Model.Pattern = v, steps: true);
    }

    public int Repeats
    {
        get => Model.Repeats;
        set => Set(Model.Repeats, Math.Clamp(value, 1, SongEntry.MaxRepeats), v => Model.Repeats = v);
    }

    public string Name
    {
        get => Model.Name;
        set => Set(Model.Name, value ?? "", v => Model.Name = v);
    }

    public string Color
    {
        get => Model.Color;
        set => Set(Model.Color, value, v => Model.Color = v);
    }

    /// <summary>Play a fill pattern after the repeats, as a bridge into the next entry.</summary>
    public bool HasFill
    {
        get => Model.Fill is not null;
        set
        {
            if (HasFill == value)
                return;
            Model.Fill = value ? new SongFill(Model.Bank, _host.Settings.RhythmGenerator.FillPattern) : null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FillPattern));
            OnPropertyChanged(nameof(FillLabel));
            _song.Changed();
        }
    }

    /// <summary>The fill pattern, in the entry's bank.</summary>
    public int FillPattern
    {
        get => Model.Fill?.Pattern ?? _host.Settings.RhythmGenerator.FillPattern;
        set
        {
            value = Math.Clamp(value, 1, PatternLibrary.PatternsPerBank);
            if (Model.Fill is null || Model.Fill.Pattern == value)
                return;
            Model.Fill = Model.Fill with { Pattern = value };
            OnPropertyChanged();
            OnPropertyChanged(nameof(FillLabel));
            _song.Changed();
        }
    }

    public string FillLabel => Model.Fill is { } fill ? $"Fill {fill.Bank}-{fill.Pattern}" : "";

    public string SlotLabel => $"{Model.Bank}-{Model.Pattern}";

    /// <summary>Steps in one pass of the pattern (its rhythm length).</summary>
    public int Steps => SongPlan.Length(_host.Library, Model.Bank, Model.Pattern);

    internal void RefreshSteps() => OnPropertyChanged(nameof(Steps));

    /// <summary>Next colour from the palette.</summary>
    [RelayCommand]
    private void NextColor()
    {
        var index = SongViewModel.Palette.ToList().IndexOf(Color);
        Color = SongViewModel.Palette[(index + 1) % SongViewModel.Palette.Count];
    }

    [RelayCommand]
    private void Remove() => _song.Remove(this);

    [RelayCommand]
    private void MoveUp() => _song.Move(this, -1);

    [RelayCommand]
    private void MoveDown() => _song.Move(this, 1);

    [RelayCommand]
    private void Duplicate() => _song.Duplicate(this);

    /// <summary>Plays the song from this entry.</summary>
    [RelayCommand]
    private void PlayFromHere() => _song.PlayFrom(Number - 1);

    private void Set<T>(T current, T value, Action<T> apply, bool steps = false, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
            return;
        apply(value);
        OnPropertyChanged(name);
        if (steps)
        {
            if (Model.Fill is { } fill)
                Model.Fill = fill with { Bank = Model.Bank };
            OnPropertyChanged(nameof(Steps));
            OnPropertyChanged(nameof(SlotLabel));
            OnPropertyChanged(nameof(FillLabel));
        }
        _song.Changed();
    }
}

/// <summary>A block of the song timeline.</summary>
public sealed partial class TimelineBlock(int entry, bool isFill, double share, string color, string label) : ObservableObject
{
    [ObservableProperty]
    private double _progress;

    public int Entry { get; } = entry;
    public bool IsFill { get; } = isFill;

    /// <summary>Share of the song's length, 0–1, used as a star width.</summary>
    public double Share { get; } = share;

    public string Color { get; } = color;
    public string Label { get; } = label;
}
