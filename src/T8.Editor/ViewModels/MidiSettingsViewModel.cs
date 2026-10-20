using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Core.Patterns;
using T8.Editor.Services;
using T8.Midi;
using T8.Midi.Ports;

namespace T8.Editor.ViewModels;

/// <summary>
/// MIDI setup: which port, which channels and notes, and how Play behaves. Edits a copy of the
/// settings; the caller applies <see cref="ToSettings"/> when the user presses Save.
/// </summary>
public sealed partial class MidiSettingsViewModel : ObservableObject
{
    private readonly IMidiPortProvider _ports;
    private readonly EditorSettings _original;

    [ObservableProperty]
    private MidiPortInfo? _selectedPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Errors), nameof(IsValid))]
    private int _bassChannel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Errors), nameof(IsValid))]
    private int _rhythmChannel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Errors), nameof(IsValid))]
    private int _programChangeChannel;

    [ObservableProperty]
    private PlayMode _playMode;

    [ObservableProperty]
    private bool _followSelection;

    [ObservableProperty]
    private bool _followT8;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private bool _isTesting;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DetectChannelsCommand))]
    private bool _isDetecting;

    [ObservableProperty]
    private string _detectResult = "";

    [ObservableProperty]
    private string _testResult = "";

    [ObservableProperty]
    private bool? _testPassed;

    public MidiSettingsViewModel(IMidiPortProvider ports, EditorSettings settings)
    {
        _ports = ports;
        _original = settings.Clone();
        _bassChannel = settings.Midi.BassChannel;
        _rhythmChannel = settings.Midi.RhythmChannel;
        _programChangeChannel = settings.Midi.ProgramChangeChannel;
        _playMode = settings.PlayMode;
        _followSelection = settings.FollowSelection;
        _followT8 = settings.FollowT8;
        DrumNotes = new ObservableCollection<DrumNoteViewModel>(
            T8MidiMap.PlayableTracks.Select(track => new DrumNoteViewModel(track, settings.Midi.RhythmNotes.GetValueOrDefault(track), OnNoteChanged)));
        RefreshPorts();
        SelectedPort = Ports.FirstOrDefault(p => p.Name == settings.OutputPortName) ?? MidiPorts.FindT8(Ports) ?? Ports.FirstOrDefault();
    }

    public ObservableCollection<MidiPortInfo> Ports { get; } = new();
    public ObservableCollection<DrumNoteViewModel> DrumNotes { get; }

    public static IReadOnlyList<int> Channels { get; } = Enumerable.Range(1, 16).ToList();
    public static IReadOnlyList<PlayMode> PlayModes { get; } = Enum.GetValues<PlayMode>();

    public IReadOnlyList<string> Errors => BuildMap().Validate();
    public bool IsValid => Errors.Count == 0;

    /// <summary>How to set the T-8 up so it matches these settings.</summary>
    public string T8SetupHelp =>
        $"On the T-8, hold SHIFT and press OCT+ (MENU), then set bACh to {BassChannel}, rhCh to {RhythmChannel} " +
        $"and Pc.Ch to {ProgramChangeChannel}. Turn rXPc on so the editor can switch patterns, tXPc on so the editor follows " +
        "the patterns you pick on the T-8, and set SYnC to AUTO or USB so the T-8 follows the editor's clock.";

    partial void OnBassChannelChanged(int value) => OnPropertyChanged(nameof(T8SetupHelp));
    partial void OnRhythmChannelChanged(int value) => OnPropertyChanged(nameof(T8SetupHelp));
    partial void OnProgramChangeChannelChanged(int value) => OnPropertyChanged(nameof(T8SetupHelp));

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
            TestResult = $"Couldn't list MIDI ports: {ex.Message}";
        }
        SelectedPort = Ports.FirstOrDefault(p => p.Id == previous) ?? MidiPorts.FindT8(Ports) ?? Ports.FirstOrDefault();
    }

    [RelayCommand]
    private void ResetToT8Defaults()
    {
        var defaults = new T8MidiMap();
        BassChannel = defaults.BassChannel;
        RhythmChannel = defaults.RhythmChannel;
        ProgramChangeChannel = defaults.ProgramChangeChannel;
        foreach (var drum in DrumNotes)
            drum.Note = defaults.RhythmNotes[drum.Track];
    }

    private bool CanTest() => !IsTesting;

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestConnectionAsync()
    {
        if (SelectedPort is null)
        {
            TestPassed = false;
            TestResult = "Pick a MIDI output first. If the T-8 isn't listed, check the USB cable and press Refresh.";
            return;
        }

        IsTesting = true;
        TestPassed = null;
        TestResult = $"Testing {SelectedPort.Name}… you should hear kick, snare, hats and a bass note.";
        try
        {
            var result = await new ConnectionTest(_ports, BuildMap()).RunAsync(SelectedPort);
            TestPassed = result.Success;
            TestResult = result.Summary + Environment.NewLine + string.Join(Environment.NewLine, result.Log);
        }
        catch (Exception ex)
        {
            TestPassed = false;
            TestResult = $"Test failed: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>How long Detect listens for the T-8 before giving up.</summary>
    public TimeSpan DetectTime { get; init; } = TimeSpan.FromSeconds(20);

    private bool CanDetect() => !IsDetecting;

    [RelayCommand(CanExecute = nameof(CanDetect))]
    private async Task DetectChannelsAsync()
    {
        if (SelectedPort is null)
        {
            DetectResult = "Pick a MIDI output first. If the T-8 isn't listed, check the USB cable and press Refresh.";
            return;
        }

        IsDetecting = true;
        DetectResult = $"Listening for {DetectTime.TotalSeconds:0} s… press Start on the T-8 and let a pattern with drums and bass play.";
        try
        {
            var result = await new ChannelDetector(_ports, BuildMap()).RunAsync(SelectedPort, DetectTime);
            BassChannel = result.BassChannel ?? BassChannel;
            RhythmChannel = result.RhythmChannel ?? RhythmChannel;
            ProgramChangeChannel = result.ProgramChangeChannel ?? ProgramChangeChannel;
            DetectResult = result.Summary + (result.FoundAny ? " Press Save to keep them." : "");
        }
        catch (Exception ex)
        {
            DetectResult = $"Detect failed: {ex.Message}";
        }
        finally
        {
            IsDetecting = false;
        }
    }

    public T8MidiMap BuildMap() => new()
    {
        BassChannel = BassChannel,
        RhythmChannel = RhythmChannel,
        ProgramChangeChannel = ProgramChangeChannel,
        RhythmNotes = DrumNotes.ToDictionary(d => d.Track, d => d.Note),
    };

    public EditorSettings ToSettings()
    {
        var settings = _original.Clone();
        settings.OutputPortName = SelectedPort?.Name;
        settings.Midi = BuildMap();
        settings.PlayMode = PlayMode;
        settings.FollowSelection = FollowSelection;
        settings.FollowT8 = FollowT8;
        return settings;
    }

    private void OnNoteChanged()
    {
        OnPropertyChanged(nameof(Errors));
        OnPropertyChanged(nameof(IsValid));
    }
}

public sealed partial class DrumNoteViewModel(RhythmTrack track, int note, Action changed) : ObservableObject
{
    private int _note = note;

    public RhythmTrack Track { get; } = track;

    public string Name => Track switch
    {
        RhythmTrack.BD => "Bass drum",
        RhythmTrack.SD => "Snare drum",
        RhythmTrack.LT => "Tom",
        RhythmTrack.HT => "Hand clap",
        RhythmTrack.CH => "Closed hi-hat",
        RhythmTrack.OH => "Open hi-hat",
        _ => Track.ToString(),
    };

    public int Note
    {
        get => _note;
        set
        {
            if (SetProperty(ref _note, Math.Clamp(value, 0, 127)))
                changed();
        }
    }
}
