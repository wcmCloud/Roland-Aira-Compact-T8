using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using T8.Audio;
using T8.Editor.Services;
using T8.Midi;
namespace T8.Editor.ViewModels;

public sealed partial class MainWindowViewModel
{
    private long _lastPortCheck;
    private void CheckOutputConnection()
    {
        if (System.Diagnostics.Stopwatch.GetElapsedTime(_lastPortCheck).TotalSeconds < 2) return;
        _lastPortCheck = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_controller is null || _controller.IsOffline || SelectedPort is null) return;
        try
        {
            if (_ports.ListOutputs().Any(p => p.Id == SelectedPort.Id)) return;
            SelectedPort = null;
            Status = "MIDI output disconnected. Press Play to use Offline in Auto mode.";
        }
        catch (Exception ex) { Status = $"MIDI output unavailable: {ex.Message}"; }
    }
    private readonly Func<T8MidiMap, OfflineSettings, OfflinePlayer> _offlineFactory;
    public const string SystemAudioOutput = "System default";
    [ObservableProperty] private OutputMode _outputMode;
    [ObservableProperty] private SoundPreset _preset = SoundPreset.All[0];
    [ObservableProperty] private double _offlineVolume = .45;
    [ObservableProperty] private string _audioOutput = SystemAudioOutput;
    public static IReadOnlyList<OutputMode> OutputModes { get; } = Enum.GetValues<OutputMode>();
    public static IReadOnlyList<SoundPreset> Presets => SoundPreset.All;
    public static IReadOnlyList<BassWave> BassWaves { get; } = Enum.GetValues<BassWave>();
    public ObservableCollection<string> AudioOutputs { get; } = new() { SystemAudioOutput };
    private bool WantsOffline => OutputMode == OutputMode.Offline || OutputMode == OutputMode.Auto && (SelectedPort is null || IsT8DriveConnected);
    public bool IsOffline => _controller?.IsOffline ?? WantsOffline;
    public string OutputSummary => IsOffline ? $"Offline · {Preset.Name}" : "T-8 · " + (SelectedPort?.Name ?? "not connected");
    private BassSound BassSound => Settings.Offline.Bass ?? Preset.Bass;
    public BassWave Wave { get => BassSound.Wave; set => SetBass(BassSound with { Wave = value }); }
    public double Cutoff { get => BassSound.Cutoff; set => SetBass(BassSound with { Cutoff = value }); }
    public double Resonance { get => BassSound.Resonance; set => SetBass(BassSound with { Resonance = value }); }
    public double Decay { get => BassSound.Decay; set => SetBass(BassSound with { Decay = value }); }
    public double Slide { get => BassSound.Slide; set => SetBass(BassSound with { Slide = value }); }
    public double KickVolume { get => Settings.Offline.Levels.Kick; set => SetLevels(Settings.Offline.Levels with { Kick = value }, nameof(KickVolume)); }
    public double SnareVolume { get => Settings.Offline.Levels.Snare; set => SetLevels(Settings.Offline.Levels with { Snare = value }, nameof(SnareVolume)); }
    public double TomVolume { get => Settings.Offline.Levels.Tom; set => SetLevels(Settings.Offline.Levels with { Tom = value }, nameof(TomVolume)); }
    public double ClapVolume { get => Settings.Offline.Levels.Clap; set => SetLevels(Settings.Offline.Levels with { Clap = value }, nameof(ClapVolume)); }
    public double ClosedHatVolume { get => Settings.Offline.Levels.ClosedHat; set => SetLevels(Settings.Offline.Levels with { ClosedHat = value }, nameof(ClosedHatVolume)); }
    public double OpenHatVolume { get => Settings.Offline.Levels.OpenHat; set => SetLevels(Settings.Offline.Levels with { OpenHat = value }, nameof(OpenHatVolume)); }
    public double BassVolume { get => Settings.Offline.Levels.Bass; set => SetLevels(Settings.Offline.Levels with { Bass = value }, nameof(BassVolume)); }
    private void SetLevels(InstrumentLevels levels, string propertyName)
    {
        Settings.Offline.Levels = levels.Sanitize();
        ApplySound();
        OnPropertyChanged(propertyName);
    }
    private void SetBass(BassSound sound) { Settings.Offline.Bass = OfflineSettings.Sanitize(sound); ApplySound(); NotifyBass(); }
    private void NotifyBass() { foreach (var name in new[] { nameof(Wave), nameof(Cutoff), nameof(Resonance), nameof(Decay), nameof(Slide) }) OnPropertyChanged(name); }
    private void NotifyOutput()
    {
        OnPropertyChanged(nameof(IsOffline)); OnPropertyChanged(nameof(OutputSummary)); NotifyMidiSummary();
    }
    private void ResetOutput()
    {
        Song?.Stop(); StopPlayback(); CloseController(); CloseListener();
        OpenListener(); NotifyOutput();
    }
    partial void OnOutputModeChanged(OutputMode value)
    {
        Settings.OutputMode = value; ResetOutput(); SaveSettings();
    }
    partial void OnPresetChanged(SoundPreset value)
    {
        Settings.Offline.PresetId = value.Id; Settings.Offline.Bass = null;
        ApplySound(); NotifyBass(); NotifyOutput();
    }
    partial void OnOfflineVolumeChanged(double value) { Settings.Offline.Volume = value; ApplySound(); }
    private void ApplySound() { (_controller as OfflinePlayer)?.Configure(Settings.Offline); SaveSettings(); }
    partial void OnAudioOutputChanged(string value)
    {
        Settings.Offline.DeviceName = value == SystemAudioOutput ? null : value;
        if (IsOffline) ResetOutput(); SaveSettings();
    }
    partial void OnT8DrivePathChanged(string? value)
    {
        if (value is not null && _controller?.IsOffline == false) ResetOutput();
        NotifyOutput();
    }
    [RelayCommand]
    private void RefreshAudioOutputs()
    {
        try
        {
            var selected = AudioOutput;
            var outputs = AudioDevice.ListOutputs();
            AudioOutputs.Clear(); AudioOutputs.Add(SystemAudioOutput);
            foreach (var name in outputs.Distinct()) AudioOutputs.Add(name);
            AudioOutput = AudioOutputs.Contains(selected) ? selected : SystemAudioOutput;
        }
        catch (Exception ex) { Status = $"Couldn't list audio outputs: {ex.Message}"; }
    }
}
