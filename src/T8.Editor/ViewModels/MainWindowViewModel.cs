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
    private T8Controller? _controller;
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

    public MainWindowViewModel(IMidiPortProvider ports, IDialogService dialogs)
    {
        _ports = ports;
        _dialogs = dialogs;
        Library = PatternLibrary.CreateBlank();
        LoadSelectedPattern();
        RefreshPorts();
    }

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
        _controller?.Dispose();
        _controller = null;
    }

    partial void OnBpmChanged(double value)
    {
        if (IsPlaying)
            StartPlayback();
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

        SelectedPort = Ports.FirstOrDefault(p => p.Id == previous) ?? MidiPorts.FindT8(Ports) ?? Ports.FirstOrDefault();
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
        IsPlaying = false;
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
            _controller = new T8Controller(_ports.OpenOutput(SelectedPort));
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
        if (IsPlaying)
            StartPlayback();
    }

    private void LoadSelectedPattern()
    {
        Bass = new BassEditorViewModel(CurrentBass, MarkDirty, AuditionBass);
        Rhythm = new RhythmEditorViewModel(CurrentRhythm, MarkDirty, AuditionDrum);
        OnPropertyChanged(nameof(PatternLabel));
    }

    private void MarkDirty() => IsDirty = true;

    private async Task<bool> ConfirmDiscardAsync() =>
        !IsDirty || await _dialogs.ConfirmAsync("Unsaved changes", "Discard the changes you haven't saved?");

    public void Dispose()
    {
        _controller?.Dispose();
        _controller = null;
    }
}
