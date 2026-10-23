using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;
using T8.Editor.Services;
using T8.Midi;
using T8.Midi.Ports;

namespace T8.Editor.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable, ISongHost
{
    private readonly IMidiPortProvider _ports;
    private readonly IDialogService _dialogs;
    private readonly ISettingsStore? _settingsStore;
    private readonly Func<IEnumerable<string>> _driveCandidates;
    private T8Controller? _controller;
    private T8InputListener? _listener;
    private readonly RhythmTrackClipboard _rowClipboard = new();
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private bool _playheadShown;
    private (BassPattern Bass, RhythmPattern Rhythm)? _clipboard;
    private readonly PatternHistory _history;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LibraryLabel))]
    private string? _folderPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LibraryLabel))]
    private bool _isDirty;

    [ObservableProperty]
    private int _selectedBank = 1;

    [ObservableProperty]
    private int _selectedPattern = 1;

    [ObservableProperty]
    private BassEditorViewModel _bass = null!;

    [ObservableProperty]
    private RhythmEditorViewModel _rhythm = null!;

    /// <summary>The folder of factory patterns shipped next to the app. The T-8's own folder is still named BACKUP.</summary>
    public const string OriginalPresetsFolderName = "original presets";

    /// <summary>Shown in orange when the editor opened the patterns that ship in the original presets folder.</summary>
    public const string DefaultPresetsStatus =
        "Default presets were loaded. You can find them in the original presets folder.";

    [ObservableProperty]
    private string _status = "New library. Open a T-8 backup folder to load your patterns.";

    /// <summary>True while <see cref="Status"/> is the default-presets notice, so the status bar draws it in orange.</summary>
    [ObservableProperty]
    private bool _statusHighlighted;

    partial void OnStatusChanged(string value) => StatusHighlighted = value == DefaultPresetsStatus;

    [ObservableProperty]
    private MidiPortInfo? _selectedPort;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    private double _bpm = 120;

    [ObservableProperty]
    private bool _auditionEnabled = true;

    [ObservableProperty]
    private bool _showPlayhead = true;

    /// <summary>Paused with the Pause button: Play carries on from the same place.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(PauseCommand))]
    private bool _isPaused;

    /// <summary>What the T-8's own sequencer is doing, e.g. "T-8 playing · 124 BPM"; empty when it's stopped.</summary>
    [ObservableProperty]
    private string _t8Status = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private bool _isTestingConnection;

    /// <param name="settingsStore">Where settings are kept; null keeps them in memory only.</param>
    /// <param name="driveCandidates">Where to look for a mounted T-8; defaults to this OS's mount points.</param>
    /// <param name="updateChecker">Checks GitHub for a newer release; null turns update checks off (tests).</param>
    /// <param name="urlOpener">Opens web pages; defaults to the browser.</param>
    /// <param name="presetFolder">Folder to search for the shipped original presets; defaults to the app directory.</param>
    public MainWindowViewModel(
        IMidiPortProvider ports,
        IDialogService dialogs,
        ISettingsStore? settingsStore = null,
        Func<IEnumerable<string>>? driveCandidates = null,
        UpdateChecker? updateChecker = null,
        IUrlOpener? urlOpener = null,
        string? presetFolder = null)
    {
        _updateChecker = updateChecker;
        _urlOpener = urlOpener ?? new BrowserUrlOpener();
        _ports = ports;
        _dialogs = dialogs;
        _settingsStore = settingsStore;
        _driveCandidates = driveCandidates ?? T8Drive.CandidateRoots;
        Settings = settingsStore?.Load() ?? new EditorSettings();
        _bpm = Settings.Bpm;
        _auditionEnabled = Settings.AuditionEnabled;
        _showPlayhead = Settings.ShowPlayhead;
        Library = LoadDefaultPresets(presetFolder);
        _history = new PatternHistory(() => Library);
        _history.Changed += OnHistoryChanged;
        BassGenerator = new BassGeneratorViewModel(Settings.BassGenerator.Clone(), options =>
        {
            Settings.BassGenerator = options;
            SaveSettings();
        }, OnPatternGenerated,
            (bank, pattern) => Library.Bass[bank - 1, pattern - 1],
            (bank, pattern) => Library.Rhythm[bank - 1, pattern - 1],
            NextFreeSlots,
            () => Library.Rhythm[SelectedBank - 1, SelectedPattern - 1],
            _history);
        RhythmGenerator = new RhythmGeneratorViewModel(Settings.RhythmGenerator.Clone(), options =>
        {
            Settings.RhythmGenerator = options;
            SaveSettings();
        }, OnPatternGenerated,
            () => (SelectedBank, SelectedPattern),
            (bank, pattern) => Library.Rhythm[bank - 1, pattern - 1],
            (bank, pattern) => Library.Bass[bank - 1, pattern - 1],
            NextFreeSlots,
            () => Library.Bass[SelectedBank - 1, SelectedPattern - 1],
            _history);
        Song = new SongViewModel(this, dialogs, ports);
        PatternBanks = Enumerable.Range(1, PatternLibrary.BankCount)
            .Select(bank => new PatternBankRow(bank, Enumerable.Range(1, PatternLibrary.PatternsPerBank)
                .Select(pattern => new PatternSlotViewModel(bank, pattern, OnSlotClicked))
                .ToList()))
            .ToList();
        RhythmGenerator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RhythmGeneratorViewModel.FillPattern))
                RefreshSlots();
        };
        BassGenerator.ShowColours = Settings.ShowBassColours;
        BassGenerator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(BassGeneratorViewModel.ShowColours))
                return;
            Settings.ShowBassColours = BassGenerator.ShowColours;
            SaveSettings();
        };
        LoadSelectedPattern();
        if (_announceDefaultPresets)
            Status = DefaultPresetsStatus;
        RefreshPorts();
        _t8DrivePath = T8Drive.Find(_driveCandidates());
        StartupUpdateCheck = Settings.CheckForUpdatesOnStartup && _updateChecker is not null
            ? CheckForUpdatesInBackgroundAsync()
            : Task.CompletedTask;
    }

    // ---- New version on GitHub ----------------------------------------------------------

    private readonly UpdateChecker? _updateChecker;
    private readonly IUrlOpener _urlOpener;

    /// <summary>A newer release on GitHub, shown as a chip in the status bar; null when there is none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(UpdateLabel), nameof(UpdateTip))]
    private ReleaseInfo? _availableUpdate;

    public bool HasUpdate => AvailableUpdate is not null;

    public string UpdateLabel => AvailableUpdate is { } u ? $"v{u.Version} available" : "";

    public string UpdateTip => AvailableUpdate is { } u
        ? $"Version {u.Version} of the editor is out (you have {AppInfo.DisplayVersion}). Click to open its release page."
        : "";

    /// <summary>The check that runs when the editor starts (completed at once when it's off), for tests.</summary>
    public Task StartupUpdateCheck { get; }

    /// <summary>The startup check: cached for a day, and silent when it fails.</summary>
    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            var cache = Settings.UpdateCheck.Clone();
            var result = await Task.Run(() => _updateChecker!.CheckAsync(cache, useCache: true)).ConfigureAwait(false);
            // Back on the UI thread; the task finishes once the result is shown.
            var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            OnUi(() =>
            {
                ApplyUpdateResult(result, cache);
                applied.SetResult();
            });
            await applied.Task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The startup check never bothers the user.
        }
    }

    /// <summary>
    /// "Check for updates" (About window, macOS app menu): asks GitHub now, shows the chip when there is a newer
    /// version, and returns a message for the user, which also goes to the status bar.
    /// </summary>
    public async Task<string> CheckForUpdatesNowAsync()
    {
        if (_updateChecker is null)
            return Status = "Update checks aren't available in this build.";
        var cache = Settings.UpdateCheck.Clone();
        var result = await _updateChecker.CheckAsync(cache, useCache: false);
        ApplyUpdateResult(result, cache);
        return Status = result.Outcome switch
        {
            UpdateOutcome.UpdateAvailable => $"Version {result.Release!.Version} is available: click \"{UpdateLabel}\" in the status bar to download it.",
            UpdateOutcome.UpToDate => $"You're up to date ({AppInfo.DisplayVersion} is the latest version).",
            _ => $"Couldn't check for updates: {result.Error}.",
        };
    }

    [RelayCommand]
    private Task CheckForUpdatesAsync() => CheckForUpdatesNowAsync();

    private void ApplyUpdateResult(UpdateCheckResult result, UpdateCheckCache cache)
    {
        if (result.Outcome == UpdateOutcome.Failed)
            return;
        AvailableUpdate = result.Outcome == UpdateOutcome.UpdateAvailable ? result.Release : null;
        if (!result.FromCache)
        {
            Settings.UpdateCheck = cache;
            SaveSettings();
        }
    }

    /// <summary>The status bar chip: opens the new version's release page.</summary>
    [RelayCommand]
    private void OpenUpdatePage()
    {
        if (AvailableUpdate is { } update)
            _urlOpener.Open(update.Url);
    }

    /// <summary>The Song tab.</summary>
    public SongViewModel Song { get; }

    T8Controller? ISongHost.Controller() => Controller();

    void ISongHost.StopPatternPlayback() => StopPlayback();

    void ISongHost.ShowStatus(string text) => Status = text;

    void ISongHost.PatternsReplaced()
    {
        LoadSelectedPattern();
        MarkDirty();
    }

    /// <summary>The 4 × 16 grid of patterns, one row per bank.</summary>
    public IReadOnlyList<PatternBankRow> PatternBanks { get; }

    public const int BassTab = 1;
    public const int SongTab = 2;

    /// <summary>The tab shown: 0 Rhythm, 1 Bass, 2 Song.</summary>
    [ObservableProperty]
    private int _selectedTab;

    partial void OnSelectedTabChanged(int value)
    {
        if (value != SongTab && Song.FollowDawClock) Song.Stop();
        if (value == SongTab) Song.RefreshClockInputsCommand.Execute(null);
    }

    /// <summary>A click in the pattern grid: opens the pattern, and on the Song tab also adds it to the song.</summary>
    private void OnSlotClicked(PatternSlotViewModel slot)
    {
        SelectSlot(slot.Bank, slot.Pattern);
        if (SelectedTab == SongTab)
        {
            var entry = Song.AddEntry(slot.Bank, slot.Pattern);
            Status = $"Added {entry.Name} to the song.";
        }
    }

    /// <summary>Switches to a bank and pattern in one go (one load, one program change).</summary>
    public void SelectSlot(int bank, int pattern)
    {
        if (bank == SelectedBank && pattern == SelectedPattern)
            return;
#pragma warning disable MVVMTK0034 // set both fields before reacting once
        _selectedBank = bank;
        _selectedPattern = pattern;
#pragma warning restore MVVMTK0034
        OnPropertyChanged(nameof(SelectedBank));
        OnPropertyChanged(nameof(SelectedPattern));
        OnSelectionChanged();
    }

    /// <summary>Updates which grid cells are selected, empty and the fill pattern.</summary>
    private void RefreshSlots()
    {
        if (PatternBanks is null)
            return;
        foreach (var row in PatternBanks)
        {
            foreach (var slot in row.Slots)
            {
                slot.IsSelected = slot.Bank == SelectedBank && slot.Pattern == SelectedPattern;
                slot.IsFill = slot.Bank == SelectedBank && slot.Pattern == RhythmGenerator.FillPattern;
                slot.IsEmpty = IsEmpty(slot.Bank, slot.Pattern);
            }
        }
    }

    private bool IsEmpty(int bank, int pattern)
    {
        var bass = Library.Bass[bank - 1, pattern - 1];
        var rhythm = Library.Rhythm[bank - 1, pattern - 1];
        return bass.Steps.All(s => s.State == BassStepState.Off) && rhythm.Steps.All(s => s.Cells.All(c => !c.On));
    }

    /// <summary>The next empty slots after the one being edited, wrapping through the library.</summary>
    private IReadOnlyList<(int Bank, int Pattern)> NextFreeSlots(int count)
    {
        var found = new List<(int Bank, int Pattern)>();
        var total = PatternLibrary.BankCount * PatternLibrary.PatternsPerBank;
        var start = (SelectedBank - 1) * PatternLibrary.PatternsPerBank + (SelectedPattern - 1);
        for (var step = 1; step < total && found.Count < count; step++)
        {
            var index = (start + step) % total;
            var bank = index / PatternLibrary.PatternsPerBank + 1;
            var pattern = index % PatternLibrary.PatternsPerBank + 1;
            if (IsEmpty(bank, pattern))
                found.Add((bank, pattern));
        }
        return found;
    }

    public BassGeneratorViewModel BassGenerator { get; }
    public RhythmGeneratorViewModel RhythmGenerator { get; }

    private void OnPatternGenerated(string summary)
    {
        if (_history.IsDirty)
            MarkDirty();
        Status = summary;
        // The preview plays a snapshot of the pattern, so restart it to hear the new line.
        if (IsPlaying && Settings.PlayMode == PlayMode.PreviewNotes)
            StartPlayback();
    }

    public EditorSettings Settings { get; private set; }

    public string MidiSummary =>
        $"Bass {PartChannel(Settings.Midi.BassChannel)} · Rhythm {PartChannel(Settings.Midi.RhythmChannel)} · Pattern select ch {Settings.Midi.ProgramChangeChannel}" +
        (PlaysOnT8 ? " · Play runs the T-8's sequencer" : "");

    /// <summary>Channel number, or OFF, for the status-bar pills.</summary>
    public string BassChannelText => T8MidiMap.ChannelName(Settings.Midi.BassChannel);

    public string RhythmChannelText => T8MidiMap.ChannelName(Settings.Midi.RhythmChannel);

    public string PatternChannelText => T8MidiMap.ChannelName(Settings.Midi.ProgramChangeChannel);

    public bool BassChannelOn => Settings.Midi.BassOn;

    public bool RhythmChannelOn => Settings.Midi.RhythmOn;

    /// <summary>Play is running the T-8's own sequencer, shown as its own pill.</summary>
    public bool PlaysOnT8 => Settings.PlayMode == PlayMode.SyncT8Sequencer;

    private static string PartChannel(int channel) => channel == T8MidiMap.Off ? "OFF" : $"ch {channel}";

    private void NotifyMidiSummary()
    {
        OnPropertyChanged(nameof(MidiSummary));
        OnPropertyChanged(nameof(BassChannelText));
        OnPropertyChanged(nameof(RhythmChannelText));
        OnPropertyChanged(nameof(PatternChannelText));
        OnPropertyChanged(nameof(BassChannelOn));
        OnPropertyChanged(nameof(RhythmChannelOn));
        OnPropertyChanged(nameof(PlaysOnT8));
    }

    public PatternLibrary Library { get; private set; }

    public IReadOnlyList<int> BankNumbers { get; } = Enumerable.Range(1, PatternLibrary.BankCount).ToList();
    public IReadOnlyList<int> PatternNumbers { get; } = Enumerable.Range(1, PatternLibrary.PatternsPerBank).ToList();

    public ObservableCollection<MidiPortInfo> Ports { get; } = new();

    public const string AppName = AppInfo.Name;

    /// <summary>The window title: the app name and version, nothing after it.</summary>
    public string Title => $"{AppName} - {AppInfo.DisplayVersion}";

    /// <summary>The open set of patterns and whether it has unsaved changes, shown on the Patterns card.</summary>
    public string LibraryLabel
    {
        get
        {
            var name = FolderPath is null ? "Untitled" : Path.GetFileName(Path.TrimEndingDirectorySeparator(FolderPath));
            if (string.IsNullOrEmpty(name))
                name = FolderPath;
            return IsDirty ? $"{name} · unsaved changes" : name!;
        }
    }

    public string PatternLabel => $"Bank {SelectedBank} · Pattern {SelectedPattern}";

    public bool CanPaste => _clipboard is not null;

    public BassPattern CurrentBass => Library.Bass[SelectedBank - 1, SelectedPattern - 1];
    public RhythmPattern CurrentRhythm => Library.Rhythm[SelectedBank - 1, SelectedPattern - 1];

    partial void OnSelectedBankChanged(int value) => OnSelectionChanged();
    partial void OnSelectedPatternChanged(int value) => OnSelectionChanged();

    partial void OnSelectedPortChanged(MidiPortInfo? value)
    {
        StopPlayback();
        CloseController();
        OpenListener();
        if (value is not null && Settings.OutputPortName != value.Name)
        {
            Settings.OutputPortName = value.Name;
            SaveSettings();
        }
    }

    partial void OnBpmChanged(double value)
    {
        Settings.Bpm = value;
        SaveSettings();
        if (IsPlaying && Settings.PlayMode == PlayMode.SyncT8Sequencer)
            _controller!.Clock.Bpm = value;
        else if (IsPlaying)
            StartPlayback();
    }

    partial void OnShowPlayheadChanged(bool value)
    {
        Settings.ShowPlayhead = value;
        SaveSettings();
        UpdatePlayhead();
    }

    /// <summary>
    /// Moves the bass and rhythm highlights to the step being played. The window calls this on a
    /// timer; it clears them when stopped or when <see cref="ShowPlayhead"/> is off.
    /// </summary>
    public void UpdatePlayhead()
    {
        Song?.UpdatePosition();
        long? position = null;
        if (ShowPlayhead && IsPlaying)
            position = _controller?.CurrentStep;
        else if (ShowPlayhead && !IsPaused && Settings.FollowT8 && _listener is { IsRunning: true } t8)
            position = t8.CurrentStep; // the T-8 playing its own sequencer
        UpdateT8Status();
        if (position is null && !_playheadShown)
            return;
        Bass.ShowPlayingStep(position);
        Rhythm.ShowPlayingStep(position);
        _playheadShown = position is not null;
    }

    private void UpdateT8Status()
    {
        var status = Settings.FollowT8 && _listener is { IsRunning: true } t8 && !IsPlaying
            ? t8.Bpm is { } bpm ? $"T-8 playing · {bpm:0} BPM" : "T-8 playing"
            : "";
        if (T8Status != status)
            T8Status = status;
    }

    /// <summary>
    /// Listens to the T-8's MIDI output (its pattern changes and its own sequencer's clock). Closed
    /// while a test or the settings window uses the input, because Windows only lets one program
    /// open a MIDI input at a time.
    /// </summary>
    private void OpenListener()
    {
        CloseListener();
        if (SelectedPort is null || !Settings.FollowT8)
            return;
        try
        {
            _listener = T8InputListener.Open(_ports, SelectedPort, Settings.Midi.Clone());
        }
        catch (Exception)
        {
            _listener = null; // No input to follow; everything else still works.
        }
        if (_listener is not null)
            _listener.PatternChanged += (_, slot) => OnUi(() => OnT8PatternChanged(slot.Bank, slot.Pattern));
    }

    private void CloseListener()
    {
        _listener?.Dispose();
        _listener = null;
        T8Status = "";
    }

    /// <summary>The T-8 changed pattern (it sends a program change when tXPc is on): follow it.</summary>
    internal void OnT8PatternChanged(int bank, int pattern)
    {
        if (!Settings.FollowT8 || (bank == SelectedBank && pattern == SelectedPattern))
            return;
        SelectSlot(bank, pattern);
        Status = $"The T-8 switched to {PatternLabel}.";
    }

    private void OnUi(Action action)
    {
        if (_ui is null)
            action();
        else
            _ui.Post(_ => action(), null);
    }

    partial void OnAuditionEnabledChanged(bool value)
    {
        Settings.AuditionEnabled = value;
        SaveSettings();
    }

    private bool _announceDefaultPresets;

    /// <summary>The original presets folder, when it was found. Open starts there.</summary>
    private string? _originalPresetsPath;

    /// <summary>
    /// The patterns in the original presets folder next to the app, or a blank library when that folder isn't there.
    /// The folder is not opened as the user's file, so Save asks where to keep a copy.
    /// </summary>
    private PatternLibrary LoadDefaultPresets(string? presetFolder)
    {
        var root = presetFolder is null
            ? PatternLibrary.ResolveRoot(Path.Combine(AppContext.BaseDirectory, OriginalPresetsFolderName))
            : PatternLibrary.ResolveRoot(presetFolder);
        if (root is null)
            return PatternLibrary.CreateBlank();
        _originalPresetsPath = root;
        try
        {
            var library = PatternLibrary.Load(root);
            _announceDefaultPresets = true;
            return library;
        }
        catch (Exception ex) when (ex is IOException or FormatException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return PatternLibrary.CreateBlank();
        }
    }

    // ---- Library -------------------------------------------------------------------------

    [RelayCommand]
    private async Task NewLibraryAsync()
    {
        if (!await ConfirmDiscardAsync())
            return;
        StopPlayback();
        Library = PatternLibrary.CreateBlank();
        FolderPath = null;
        _history.Clear();
        LoadSelectedPattern();
        Status = "New library with 64 blank patterns.";
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (!await ConfirmDiscardAsync())
            return;
        var folder = await _dialogs.PickFolderAsync("Open a T-8 BACKUP folder (or the T-8 drive)", _originalPresetsPath);
        if (folder is not null)
            OpenFolder(folder);
    }

    /// <summary>Loads a folder with BASS and RHYTHM, or the T-8 drive root (uses its BACKUP folder).</summary>
    public bool OpenFolder(string folder)
    {
        var root = PatternLibrary.ResolveRoot(folder);
        if (root is null)
        {
            Status = $"No BASS and RHYTHM folders found in {folder}.";
            return false;
        }

        try
        {
            var library = PatternLibrary.Load(root);
            StopPlayback();
            Library = library;
            FolderPath = root;
            _history.Clear();
            LoadSelectedPattern();
            Settings.LastFolder = root;
            SaveSettings();
            Status = $"Loaded 64 patterns from {root}.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or FormatException or KeyNotFoundException or UnauthorizedAccessException)
        {
            Status = $"Couldn't load {root}: {ex.Message}";
            return false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (FolderPath is null)
            await SaveAsAsync();
        else
            SaveTo(FolderPath);
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Save patterns to a folder");
        if (folder is not null)
            SaveTo(folder);
    }

    public bool SaveTo(string folder)
    {
        try
        {
            Library.Save(folder);
            FolderPath = folder;
            _history.MarkSaved();
            Status = $"Saved 64 patterns to {folder}.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't save to {folder}: {ex.Message}";
            return false;
        }
    }

    // ---- T-8 drive -----------------------------------------------------------------------

    private const string DriveHelp =
        "Connect the T-8 by USB and switch it on while holding PLAY; it then shows up as a drive named T-8.";

    /// <summary>Where the T-8 drive is mounted, or null when it isn't (the T-8 isn't in drive mode).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsT8DriveConnected), nameof(T8DriveStatus))]
    [NotifyCanExecuteChangedFor(nameof(LoadFromT8Command), nameof(WriteToT8Command))]
    private string? _t8DrivePath;

    public bool IsT8DriveConnected => T8DrivePath is not null;

    public string T8DriveStatus => T8DrivePath is { } path
        ? $"T-8 drive connected at {path}"
        : "T-8 drive not connected. Put the T-8 in drive mode (hold PLAY while switching it on), then press Check.";

    /// <summary>Looks for the T-8 drive again; the Check button.</summary>
    [RelayCommand]
    private void CheckT8Drive()
    {
        RefreshT8Drive();
        Status = IsT8DriveConnected ? $"{T8DriveStatus}." : $"T-8 drive not found. {DriveHelp}";
    }

    /// <summary>Looks for the T-8 drive without a message, e.g. when the window becomes active again.</summary>
    public void RefreshT8Drive() => T8DrivePath = T8Drive.Find(_driveCandidates());

    /// <summary>Opens the patterns from a mounted T-8's BACKUP folder.</summary>
    [RelayCommand(CanExecute = nameof(IsT8DriveConnected))]
    private async Task LoadFromT8Async()
    {
        RefreshT8Drive();
        if (T8DrivePath is not { } drive)
        {
            Status = $"T-8 drive not found. {DriveHelp}";
            return;
        }
        if (!await ConfirmDiscardAsync())
            return;
        OpenFolder(T8Drive.BackupPath(drive));
    }

    /// <summary>Writes all 64 patterns to a mounted T-8's RESTORE folder.</summary>
    [RelayCommand(CanExecute = nameof(IsT8DriveConnected))]
    private async Task WriteToT8Async()
    {
        RefreshT8Drive();
        if (T8DrivePath is not { } drive)
        {
            Status = $"T-8 drive not found. {DriveHelp}";
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "Write to T-8",
            $"Write all 64 patterns to the T-8 at {drive}? After you press ACCENT on the T-8 they replace the patterns on it.",
            "Write");
        if (!confirmed)
            return;

        try
        {
            T8Drive.WriteRestore(Library, drive);
            Status = "Written to the T-8. Eject the T-8 drive, then press ACCENT on the T-8 to load the patterns. Wait for dOnE before switching it off.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't write to the T-8: {ex.Message}";
        }
    }

    // ---- Pattern -------------------------------------------------------------------------

    [RelayCommand]
    private void CopyPattern()
    {
        _clipboard = (CurrentBass.Clone(), CurrentRhythm.Clone());
        OnPropertyChanged(nameof(CanPaste));
        PastePatternCommand.NotifyCanExecuteChanged();
        Status = $"Copied {PatternLabel}.";
    }

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void PastePattern()
    {
        if (_clipboard is not { } clip)
            return;
        _history.Edit(() =>
        {
            Library.Bass[SelectedBank - 1, SelectedPattern - 1] = clip.Bass.Clone();
            Library.Rhythm[SelectedBank - 1, SelectedPattern - 1] = clip.Rhythm.Clone();
            LoadSelectedPattern();
        });
        if (_history.IsDirty)
            MarkDirty();
        Status = $"Pasted into {PatternLabel}.";
    }

    private bool CanUndo() => _history.CanUndo;

    private bool CanRedo() => _history.CanRedo;

    /// <summary>Puts back the last change to the open patterns (Cmd/Ctrl+Z).</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _history.Undo();

    /// <summary>Reapplies the change just undone (Shift+Cmd/Ctrl+Z).</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _history.Redo();

    [RelayCommand]
    private void ClearPattern()
    {
        _history.Edit(() =>
        {
            CurrentBass.Clear();
            CurrentRhythm.Clear();
            LoadSelectedPattern();
        });
        if (_history.IsDirty)
            MarkDirty();
        Status = $"Cleared {PatternLabel}.";
    }

    // ---- MIDI ----------------------------------------------------------------------------

    [RelayCommand]
    private Task ShowAboutAsync() => _dialogs.ShowAboutAsync(_updateChecker is null ? null : CheckForUpdatesNowAsync);

    /// <summary>
    /// Opens the help window (F1, the ? button in the top bar, Help in the macOS app menu): at the given topic, or
    /// at the topic for the tab that is open.
    /// </summary>
    [RelayCommand]
    private void ShowHelp(string? topic) => _dialogs.ShowHelp(topic ?? Help.HelpTopics.ForTab(SelectedTab));

    [RelayCommand]
    private void RefreshPorts()
    {
        var previous = SelectedPort?.Id;
        Ports.Clear();
        try
        {
            foreach (var port in _ports.ListOutputs())
                Ports.Add(port);
        }
        catch (Exception ex)
        {
            Status = $"Couldn't list MIDI ports: {ex.Message}";
        }

        SelectedPort = Ports.FirstOrDefault(p => p.Id == previous)
            ?? Ports.FirstOrDefault(p => p.Name == Settings.OutputPortName)
            ?? MidiPorts.FindT8(Ports)
            ?? Ports.FirstOrDefault();
    }

    [RelayCommand]
    private async Task OpenMidiSettingsAsync()
    {
        var settingsVm = new MidiSettingsViewModel(_ports, Settings);
        CloseListener(); // Detect and Test in the window open the T-8's input themselves
        if (!await _dialogs.ShowMidiSettingsAsync(settingsVm))
        {
            OpenListener();
            return;
        }

        StopPlayback();
        CloseController();
        Settings = settingsVm.ToSettings();
        SaveSettings();
        OnPropertyChanged(nameof(Settings));
        NotifyMidiSummary();
        RefreshPorts();
        OpenListener();
        Status = $"MIDI settings saved. {MidiSummary}.";
    }

    private bool CanTestConnection() => !IsTestingConnection;

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        if (SelectedPort is null)
        {
            Status = "No MIDI output selected. Connect the T-8 and press Refresh.";
            return;
        }

        StopPlayback();
        CloseController(); // the test opens the port itself
        CloseListener();
        IsTestingConnection = true;
        Status = $"Testing {SelectedPort.Name}: you should hear kick, snare, hats and a bass note…";
        try
        {
            var result = await new ConnectionTest(_ports, Settings.Midi).RunAsync(SelectedPort);
            Status = result.Summary;
        }
        catch (Exception ex)
        {
            Status = $"Connection test failed: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
            OpenListener();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play() => StartPlayback();

    private bool CanPlay() => !IsPlaying;

    [RelayCommand]
    private void Stop() => StopPlayback();

    [RelayCommand]
    private void SelectOnT8()
    {
        if (Controller() is not { } t8)
            return;
        t8.SelectPattern(SelectedBank, SelectedPattern);
        Status = $"Sent {PatternLabel} to the T-8 (needs RX Program Change on).";
    }

    /// <summary>
    /// Pauses playback where it is. Play carries on from there: the preview resumes, or the T-8's
    /// sequencer gets MIDI Continue. Pressing Pause again also carries on.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        if (IsPaused)
        {
            StartPlayback();
            return;
        }
        if (!IsPlaying || _controller is null)
            return;
        if (Settings.PlayMode == PlayMode.SyncT8Sequencer)
            _controller.Clock.Stop();
        else
            _controller.PausePreview();
        IsPlaying = false;
        IsPaused = true;
        UpdatePlayhead();
        Status = "Paused. Press Play or Pause to carry on, Stop to go back to the start.";
    }

    private bool CanPause() => IsPlaying || IsPaused;

    partial void OnIsPlayingChanged(bool value) => PauseCommand.NotifyCanExecuteChanged();

    private void StartPlayback()
    {
        Song?.Stop(); // a pattern and a song can't play at the same time
        if (Controller() is not { } t8)
            return;
        if (IsPaused)
        {
            IsPaused = false;
            if (Settings.PlayMode == PlayMode.SyncT8Sequencer)
                t8.Clock.Continue();
            else
                t8.ResumePreview();
            IsPlaying = true;
            Status = $"Carrying on with {PatternLabel}.";
            return;
        }
        try
        {
            if (Settings.PlayMode == PlayMode.SyncT8Sequencer)
            {
                // The T-8 plays its own stored pattern, following our clock.
                t8.SelectPattern(SelectedBank, SelectedPattern);
                t8.Clock.Bpm = Bpm;
                t8.Clock.Start();
                IsPlaying = true;
                Status = $"Running the T-8's {PatternLabel} at {Bpm:0} BPM (needs SYnC set to AUTO or USB).";
                return;
            }

            t8.PlayPreview(CurrentBass, CurrentRhythm, Bpm, fill: FillRhythm);
            IsPlaying = true;
            Status = $"Playing {PatternLabel} at {Bpm:0} BPM through {SelectedPort!.Name}.";
        }
        catch (Exception ex)
        {
            IsPlaying = false;
            Status = $"Couldn't play: {ex.Message}";
        }
    }

    /// <summary>The rhythm the Fill button plays: the generator's fill pattern in this bank, if it isn't this one.</summary>
    private RhythmPattern? FillRhythm =>
        RhythmGenerator.FillPattern == SelectedPattern ? null : Library.Rhythm[SelectedBank - 1, RhythmGenerator.FillPattern - 1];

    /// <summary>
    /// Plays the fill pattern once at the end of the current loop, then the main pattern again, like
    /// FILL TRG on the T-8. Starts playback first if needed.
    /// </summary>
    [RelayCommand]
    private void PlayFill()
    {
        if (Settings.PlayMode == PlayMode.SyncT8Sequencer)
        {
            Status = "The T-8 is running its own sequencer: press SHIFT + STEP 7 on the T-8 to play its fill.";
            return;
        }
        if (FillRhythm is null)
        {
            Status = $"Pattern {SelectedPattern} is the fill pattern itself. Pick another fill pattern on the Rhythm tab.";
            return;
        }
        if (!IsPlaying)
            StartPlayback();
        if (_controller?.QueueFill() == true)
            Status = $"Fill: bank {SelectedBank} pattern {RhythmGenerator.FillPattern} plays after this loop, then {PatternLabel} again.";
    }

    private void StopPlayback()
    {
        if (!IsPlaying && !IsPaused)
            return;
        _controller?.StopPreview();
        _controller?.Clock.Stop();
        IsPlaying = false;
        IsPaused = false;
        UpdatePlayhead();
        Status = "Stopped.";
    }

    private T8Controller? Controller()
    {
        if (_controller is not null)
            return _controller;
        if (SelectedPort is null)
        {
            Status = "No MIDI output selected. Connect the T-8 and press Refresh.";
            return null;
        }
        try
        {
            _controller = new T8Controller(_ports.OpenOutput(SelectedPort), Settings.Midi.Clone());
            return _controller;
        }
        catch (Exception ex)
        {
            Status = $"Couldn't open {SelectedPort.Name}: {ex.Message}";
            return null;
        }
    }

    private void AuditionDrum(RhythmTrack track)
    {
        if (AuditionEnabled && !IsPlaying && SelectedPort is not null)
            Controller()?.TriggerDrum(track);
    }

    private void AuditionBass(int note)
    {
        if (!AuditionEnabled || IsPlaying || SelectedPort is null || Controller() is not { } t8)
            return;
        t8.BassNoteOn(note);
        Task.Delay(150).ContinueWith(_ => t8.BassNoteOff(note));
    }

    // ---- Helpers -------------------------------------------------------------------------

    private void OnSelectionChanged()
    {
        LoadSelectedPattern();
        if (IsPaused)
            StopPlayback(); // the paused pattern is no longer the one shown
        if (IsPlaying && Settings.PlayMode == PlayMode.SyncT8Sequencer)
            _controller?.SelectPattern(SelectedBank, SelectedPattern);
        else if (IsPlaying)
            StartPlayback();
        else if (Settings.FollowSelection && SelectedPort is not null)
            Controller()?.SelectPattern(SelectedBank, SelectedPattern);
    }

    private void LoadSelectedPattern()
    {
        Bass?.Detach();
        BassGenerator.MatchPattern(CurrentBass);
        Bass = new BassEditorViewModel(CurrentBass, MarkDirty, AuditionBass, BassGenerator, _history);
        Rhythm = new RhythmEditorViewModel(CurrentRhythm, MarkDirty, AuditionDrum, _rowClipboard, _history);
        BassGenerator.Target = Bass;
        RhythmGenerator.Target = Rhythm;
        Song?.RefreshSteps();
        RefreshSlots();
        OnPropertyChanged(nameof(PatternLabel));
    }

    private void MarkDirty()
    {
        IsDirty = true;
        RefreshSlots();
        Song?.RefreshSteps();
    }

    private void OnHistoryChanged(PatternHistoryChange change)
    {
        IsDirty = _history.IsDirty;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        if (change is not (PatternHistoryChange.Undo or PatternHistoryChange.Redo))
            return;

        Bass?.Refresh();
        Rhythm?.Refresh();
        RefreshSlots();
        Song?.RefreshSteps();
        Status = change == PatternHistoryChange.Undo ? "Undone." : "Redone.";
        if (IsPlaying && Settings.PlayMode == PlayMode.PreviewNotes)
            StartPlayback();
    }

    private void CloseController()
    {
        _controller?.Dispose();
        _controller = null;
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore?.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't save settings: {ex.Message}";
        }
    }

    private async Task<bool> ConfirmDiscardAsync() =>
        !IsDirty || await _dialogs.ConfirmAsync("Unsaved changes", "Discard the changes you haven't saved?", "Discard");

    public void Dispose()
    {
        Bass?.Detach();
        Song?.Stop();
        CloseListener();
        _controller?.Dispose();
        _controller = null;
    }
}
