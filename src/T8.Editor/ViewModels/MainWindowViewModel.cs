using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;
using T8.Editor.Services;
using T8.Midi;
using T8.Midi.Ports;

namespace T8.Editor.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IMidiPortProvider _ports;
    private readonly IDialogService _dialogs;
    private readonly ISettingsStore? _settingsStore;
    private readonly Func<IEnumerable<string>> _driveCandidates;
    private T8Controller? _controller;
    private bool _playheadShown;
    private (BassPattern Bass, RhythmPattern Rhythm)? _clipboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _folderPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool _isDirty;

    [ObservableProperty]
    private int _selectedBank = 1;

    [ObservableProperty]
    private int _selectedPattern = 1;

    [ObservableProperty]
    private BassEditorViewModel _bass = null!;

    [ObservableProperty]
    private RhythmEditorViewModel _rhythm = null!;

    [ObservableProperty]
    private string _status = "New library. Open a T-8 backup folder to load your patterns.";

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private bool _isTestingConnection;

    /// <param name="settingsStore">Where settings are kept; null keeps them in memory only.</param>
    /// <param name="driveCandidates">Where to look for a mounted T-8; defaults to this OS's mount points.</param>
    public MainWindowViewModel(
        IMidiPortProvider ports,
        IDialogService dialogs,
        ISettingsStore? settingsStore = null,
        Func<IEnumerable<string>>? driveCandidates = null)
    {
        _ports = ports;
        _dialogs = dialogs;
        _settingsStore = settingsStore;
        _driveCandidates = driveCandidates ?? T8Drive.CandidateRoots;
        Settings = settingsStore?.Load() ?? new EditorSettings();
        _bpm = Settings.Bpm;
        _auditionEnabled = Settings.AuditionEnabled;
        _showPlayhead = Settings.ShowPlayhead;
        Library = PatternLibrary.CreateBlank();
        BassGenerator = new BassGeneratorViewModel(Settings.BassGenerator.Clone(), options =>
        {
            Settings.BassGenerator = options;
            SaveSettings();
        }, OnBassGenerated);
        LoadSelectedPattern();
        RefreshPorts();
    }

    public BassGeneratorViewModel BassGenerator { get; }

    private void OnBassGenerated()
    {
        MarkDirty();
        Status = BassGenerator.Summary;
        // The preview plays a snapshot of the pattern, so restart it to hear the new line.
        if (IsPlaying && Settings.PlayMode == PlayMode.PreviewNotes)
            StartPlayback();
    }

    public EditorSettings Settings { get; private set; }

    public string MidiSummary =>
        $"Bass ch {Settings.Midi.BassChannel} · Rhythm ch {Settings.Midi.RhythmChannel} · Pattern select ch {Settings.Midi.ProgramChangeChannel}" +
        (Settings.PlayMode == PlayMode.SyncT8Sequencer ? " · Play runs the T-8's sequencer" : "");

    public PatternLibrary Library { get; private set; }

    public IReadOnlyList<int> BankNumbers { get; } = Enumerable.Range(1, PatternLibrary.BankCount).ToList();
    public IReadOnlyList<int> PatternNumbers { get; } = Enumerable.Range(1, PatternLibrary.PatternsPerBank).ToList();

    public ObservableCollection<MidiPortInfo> Ports { get; } = new();

    public string Title => $"T-8 Editor - {(FolderPath is null ? "Untitled" : Path.GetFileName(FolderPath))}{(IsDirty ? " *" : "")}";

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
        var position = IsPlaying && ShowPlayhead ? _controller?.CurrentStep : null;
        if (position is null && !_playheadShown)
            return;
        Bass.ShowPlayingStep(position);
        Rhythm.ShowPlayingStep(position);
        _playheadShown = position is not null;
    }

    partial void OnAuditionEnabledChanged(bool value)
    {
        Settings.AuditionEnabled = value;
        SaveSettings();
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
        IsDirty = false;
        LoadSelectedPattern();
        Status = "New library with 64 blank patterns.";
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (!await ConfirmDiscardAsync())
            return;
        var folder = await _dialogs.PickFolderAsync("Open a T-8 BACKUP folder (or the T-8 drive)");
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
            IsDirty = false;
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
            IsDirty = false;
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

    /// <summary>Opens the patterns from a mounted T-8's BACKUP folder.</summary>
    [RelayCommand]
    private async Task LoadFromT8Async()
    {
        var drive = T8Drive.Find(_driveCandidates());
        if (drive is null)
        {
            Status = $"T-8 drive not found. {DriveHelp}";
            return;
        }
        if (!await ConfirmDiscardAsync())
            return;
        OpenFolder(T8Drive.BackupPath(drive));
    }

    /// <summary>Writes all 64 patterns to a mounted T-8's RESTORE folder.</summary>
    [RelayCommand]
    private async Task WriteToT8Async()
    {
        var drive = T8Drive.Find(_driveCandidates());
        if (drive is null)
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
        Library.Bass[SelectedBank - 1, SelectedPattern - 1] = clip.Bass.Clone();
        Library.Rhythm[SelectedBank - 1, SelectedPattern - 1] = clip.Rhythm.Clone();
        LoadSelectedPattern();
        MarkDirty();
        Status = $"Pasted into {PatternLabel}.";
    }

    [RelayCommand]
    private void ClearPattern()
    {
        CurrentBass.Clear();
        CurrentRhythm.Clear();
        LoadSelectedPattern();
        MarkDirty();
        Status = $"Cleared {PatternLabel}.";
    }

    // ---- MIDI ----------------------------------------------------------------------------

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
        if (!await _dialogs.ShowMidiSettingsAsync(settingsVm))
            return;

        StopPlayback();
        CloseController();
        Settings = settingsVm.ToSettings();
        SaveSettings();
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(MidiSummary));
        RefreshPorts();
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

    private void StartPlayback()
    {
        if (Controller() is not { } t8)
            return;
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

            t8.PlayPreview(CurrentBass, CurrentRhythm, Bpm);
            IsPlaying = true;
            Status = $"Playing {PatternLabel} at {Bpm:0} BPM through {SelectedPort!.Name}.";
        }
        catch (Exception ex)
        {
            IsPlaying = false;
            Status = $"Couldn't play: {ex.Message}";
        }
    }

    private void StopPlayback()
    {
        if (!IsPlaying)
            return;
        _controller?.StopPreview();
        _controller?.Clock.Stop();
        IsPlaying = false;
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
        if (IsPlaying && Settings.PlayMode == PlayMode.SyncT8Sequencer)
            _controller?.SelectPattern(SelectedBank, SelectedPattern);
        else if (IsPlaying)
            StartPlayback();
        else if (Settings.FollowSelection && SelectedPort is not null)
            Controller()?.SelectPattern(SelectedBank, SelectedPattern);
    }

    private void LoadSelectedPattern()
    {
        Bass = new BassEditorViewModel(CurrentBass, MarkDirty, AuditionBass);
        Rhythm = new RhythmEditorViewModel(CurrentRhythm, MarkDirty, AuditionDrum);
        BassGenerator.Target = Bass;
        OnPropertyChanged(nameof(PatternLabel));
    }

    private void MarkDirty() => IsDirty = true;

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
        _controller?.Dispose();
        _controller = null;
    }
}
