using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using T8.Audio;
using T8.Core.Patterns;
using T8.Editor.Services;
using T8.Editor.ViewModels;
using T8.Editor.Views;
using T8.Tests.Shared;
namespace T8.Editor.Tests;

public sealed class OfflineModeTests : IDisposable
{
    private readonly FakePortProvider _ports = FakePortProvider.WithT8();
    private readonly FakeDialogService _dialogs = new();
    private readonly MemorySettingsStore _store = new();
    private readonly List<FakeAudio> _devices = new();
    private MainWindowViewModel? _vm;
    private MainWindowViewModel Create(FakePortProvider? ports = null) => _vm = new(ports ?? _ports,_dialogs,_store,()=>[],offlineFactory:(map,settings)=>
    {var device=new FakeAudio();_devices.Add(device);return new OfflinePlayer(map,settings,device);});
    public void Dispose()=>_vm?.Dispose();
    [Fact]
    public void Auto_plays_without_midi_and_updates_playhead()
    {
        var vm=Create(new FakePortProvider());Assert.True(vm.IsOffline);vm.PlayCommand.Execute(null);Assert.True(vm.IsPlaying);
        _devices.Single().Pump();vm.UpdatePlayhead();Assert.Contains("Offline",vm.Status);Assert.Empty(_ports.Opened);
    }
    [Fact]
    public void Auto_uses_offline_while_the_t8_is_in_drive_mode()
    {
        var vm = Create();
        vm.T8DrivePath = "/Volumes/T-8";
        vm.PlayCommand.Execute(null);
        _devices.Single().Pump();
        Assert.True(vm.IsOffline);
        Assert.True(vm.IsPlaying);
        Assert.Empty(_ports.Opened);
    }

    [Fact]
    public void Explicit_offline_never_opens_hardware_even_in_sequencer_mode()
    {
        _store.Stored.PlayMode=PlayMode.SyncT8Sequencer;var vm=Create();vm.OutputMode=OutputMode.Offline;Assert.False(vm.PlaysOnT8);
        vm.SelectSlot(2,2);vm.PlayCommand.Execute(null);_devices.Single().Pump();Assert.Empty(_ports.Opened);
        vm.SelectOnT8Command.Execute(null);Assert.Empty(_ports.Opened);Assert.Contains("Select T-8",vm.Status);
    }
    [Fact]
    public void Auto_falls_back_when_midi_open_fails_but_explicit_hardware_does_not()
    {
        _ports.FailOutputs=true;var vm=Create();vm.PlayCommand.Execute(null);Assert.True(vm.IsOffline);Assert.Single(_devices);
        vm.OutputMode=OutputMode.T8;vm.PlayCommand.Execute(null);Assert.False(vm.IsPlaying);Assert.Single(_devices);Assert.Contains("Couldn't open",vm.Status);
    }
    [Fact]
    public void Explicit_hardware_without_port_reports_error()
    {
        var vm=Create(new FakePortProvider());vm.OutputMode=OutputMode.T8;vm.PlayCommand.Execute(null);
        Assert.False(vm.IsPlaying);Assert.Empty(_devices);Assert.Contains("unavailable",vm.Status);
    }
    [Fact]
    public void Switching_output_stops_song_and_disposes_audio()
    {
        var vm=Create();vm.OutputMode=OutputMode.Offline;vm.Song.AddEntry(1,1);vm.Song.PlayCommand.Execute(null);_devices.Single().Pump();
        Assert.True(vm.Song.IsPlaying);vm.OutputMode=OutputMode.T8;Assert.False(vm.Song.IsPlaying);Assert.True(_devices.Single().Disposed);
    }
    [Fact]
    public void Offline_works_with_saved_hardware_channel_remapping()
    {
        _store.Stored.Midi.BassChannel=5;_store.Stored.Midi.RhythmNotes[RhythmTrack.BD]=99;
        var vm=Create();vm.OutputMode=OutputMode.Offline;vm.PlayCommand.Execute(null);_devices.Single().Pump();Assert.Equal(99,vm.Settings.Midi.RhythmNotes[RhythmTrack.BD]);
    }
    [Fact]
    public void Device_failure_stops_transport_and_retry_creates_new_device()
    {
        var vm=Create();vm.OutputMode=OutputMode.Offline;vm.PlayCommand.Execute(null);_devices.Single().Pump();
        _devices[0].Error="Device unplugged";vm.UpdatePlayhead();Assert.False(vm.IsPlaying);Assert.Contains("unplugged",vm.Status);
        vm.PlayCommand.Execute(null);Assert.Equal(2,_devices.Count);Assert.True(vm.IsPlaying);
    }
    [Fact]
    public void Preset_and_tweaks_persist_without_dirtying_patterns()
    {
        var vm=Create();vm.Preset=SoundPreset.Find("acid");vm.Cutoff=1700;vm.Slide=.2;vm.OfflineVolume=.3;
        Assert.Equal("acid",_store.Stored.Offline.PresetId);Assert.Equal(1700,_store.Stored.Offline.Bass!.Cutoff);Assert.False(vm.IsDirty);
        vm.Preset=SoundPreset.Find("deep");Assert.Null(vm.Settings.Offline.Bass);Assert.Equal(500,vm.Cutoff);
    }
    [Fact]
    public void Offline_ignores_incoming_hardware_pattern_changes()
    {
        var vm=Create();vm.OutputMode=OutputMode.Offline;vm.OnT8PatternChanged(2,5);Assert.Equal(1,vm.SelectedBank);Assert.Equal(1,vm.SelectedPattern);
    }
    [Fact]
    public void Pause_resume_and_stop_work_without_hardware()
    {
        var vm=Create(new FakePortProvider());vm.PlayCommand.Execute(null);_devices.Single().Pump();
        vm.PauseCommand.Execute(null);Assert.True(vm.IsPaused);_devices[0].Pump();vm.PlayCommand.Execute(null);Assert.True(vm.IsPlaying);
        vm.StopCommand.Execute(null);Assert.False(vm.IsPlaying);Assert.False(vm.IsPaused);
    }
    [Fact]
    public void Daw_clock_song_uses_offline_notes_even_when_hardware_mode_is_sequencer()
    {
        _store.Stored.PlayMode=PlayMode.SyncT8Sequencer;var vm=Create();vm.OutputMode=OutputMode.Offline;
        vm.SelectedTab=MainWindowViewModel.SongTab;vm.Song.AddEntry(1,1);vm.Song.RefreshClockInputsCommand.Execute(null);vm.Song.FollowDawClock=true;
        vm.Song.PlayCommand.Execute(null);var input=_ports.OpenedInputs.Last();
        input.Receive(new Melanchall.DryWetMidi.Core.StartEvent());input.Receive(new Melanchall.DryWetMidi.Core.TimingClockEvent());
        _devices.Single().Pump();Assert.True(vm.Song.IsPlaying);Assert.Empty(_ports.Opened);
        vm.Song.StopCommand.Execute(null);Assert.True(input.IsDisposed);
    }
    [AvaloniaFact]
    public void Output_and_six_presets_are_available_in_view()
    {
        var vm=Create(new FakePortProvider());var window=new MainWindow {DataContext=vm};window.Show();
        Assert.Equal(3,window.FindControl<ComboBox>("OutputModePicker")!.ItemCount);
        Assert.Equal(6,window.FindControl<ComboBox>("OfflinePresetPicker")!.ItemCount);window.Close();
    }
    [AvaloniaFact]
    public void Sound_controls_follow_output_and_hardware_action_is_hidden_offline()
    {
        var vm = Create();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            foreach (var mode in new[] { OutputMode.Offline, OutputMode.T8, OutputMode.Offline })
            {
                vm.OutputMode = mode;
                Assert.Equal(mode == OutputMode.Offline, window.FindControl<WrapPanel>("OfflineSoundControls")!.IsVisible);
                Assert.Equal(mode == OutputMode.T8, window.FindControl<Button>("SelectOnT8Button")!.IsVisible);
                Assert.True(window.FindControl<Button>("PlayButton")!.IsVisible);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Clock_input_is_shown_only_when_follow_clock_is_enabled()
    {
        var vm = Create();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            foreach (var enabled in new[] { false, true, false })
            {
                vm.FollowExternalClock = enabled;
                Assert.Equal(enabled, window.FindControl<WrapPanel>("ClockInputRow")!.IsVisible);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Sound_settings_open_without_expanding_playback_card()
    {
        var vm = Create(new FakePortProvider());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            window.UpdateLayout();
            var card = window.FindControl<Border>("PlaybackCard")!;
            var height = card.Bounds.Height;
            var button = window.FindControl<Button>("OfflineSoundSettingsButton")!;
            var flyout = Assert.IsType<Flyout>(button.Flyout);
            flyout.ShowAt(button);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(flyout.IsOpen);
            Assert.Same(vm, ((StackPanel)((ScrollViewer)flyout.Content!).Content!).DataContext);
            var knobs = ((StackPanel)((ScrollViewer)flyout.Content!).Content!).GetLogicalDescendants().OfType<Knob>().Where(k => k.Name is not null).ToDictionary(k => k.Name!);
            knobs["OfflineCutoffKnob"].Value = 1800;
            knobs["OfflineResonanceKnob"].Value = 1.5;
            knobs["OfflineDecayKnob"].Value = .4;
            knobs["OfflineSlideKnob"].Value = .12;
            Assert.Equal(1800, vm.Cutoff);
            Assert.Equal(1.5, vm.Resonance);
            Assert.Equal(.4, vm.Decay);
            Assert.Equal(.12, vm.Slide);
            Assert.False(vm.IsDirty);
            Assert.Equal(height, card.Bounds.Height);
            flyout.Hide();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Volume_knob_updates_saved_volume_and_displays_percentage()
    {
        var vm = Create(new FakePortProvider());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            var knob = window.FindControl<Knob>("OfflineVolumeKnob")!;
            Assert.Equal("{0:P0}", knob.Format);
            Assert.Equal(.45, knob.Value);
            knob.Nudge(5);
            Assert.Equal(.5, vm.OfflineVolume, 6);
            Assert.Equal(.5, _store.Stored.Offline.Volume, 6);
            vm.OfflineVolume = .25;
            Assert.Equal(.25, knob.Value, 6);
            knob.Nudge(-100);
            Assert.Equal(0, vm.OfflineVolume);
            knob.Nudge(200);
            Assert.Equal(1, vm.OfflineVolume);
            Assert.False(vm.IsDirty);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Offline_sound_shares_output_row_with_aligned_settings()
    {
        var vm = Create(new FakePortProvider());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            window.UpdateLayout();
            var panel = window.FindControl<Border>("OutputPanel")!;
            var output = window.FindControl<ComboBox>("OutputModePicker")!;
            var preset = window.FindControl<ComboBox>("OfflinePresetPicker")!;
            var settings = window.FindControl<Button>("OfflineSoundSettingsButton")!;
            double Center(Avalonia.Visual control) =>
                control.TranslatePoint(new Avalonia.Point(0, control.Bounds.Height / 2), panel)!.Value.Y;
            Assert.InRange(Math.Abs(Center(output) - Center(preset)), 0, 1);
            Assert.InRange(Math.Abs(Center(settings) - Center(preset)), 0, 2);
            Assert.InRange(Center(settings), 0, panel.Bounds.Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Hardware_output_status_aligns_with_output_picker()
    {
        var vm = Create();
        vm.OutputMode = OutputMode.T8;
        var window = new MainWindow { DataContext = vm };
        window.Show();
        try
        {
            window.UpdateLayout();
            var picker = window.FindControl<ComboBox>("OutputModePicker")!;
            var status = window.FindControl<TextBlock>("OutputStatusText")!;
            var pickerCenter = picker.TranslatePoint(new Point(0, picker.Bounds.Height / 2), window)!.Value.Y;
            var statusCenter = status.TranslatePoint(new Point(0, status.Bounds.Height / 2), window)!.Value.Y;
            Assert.True(status.IsVisible);
            Assert.InRange(Math.Abs(pickerCenter - statusCenter), 0, 2);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void Instrument_mix_persists_across_presets_without_changing_pattern_data()
    {
        var vm = Create();
        vm.KickVolume = .1; vm.SnareVolume = .2; vm.TomVolume = .3;
        vm.ClapVolume = .4; vm.ClosedHatVolume = .5; vm.OpenHatVolume = .6; vm.BassVolume = 0;
        vm.Preset = SoundPreset.Find("deep");
        Assert.Equal(new InstrumentLevels(.1, .2, .3, .4, .5, .6, 0), _store.Stored.Offline.Levels);
        Assert.False(vm.IsDirty);
        Assert.Equal(.45, vm.OfflineVolume);
        vm.OutputMode = OutputMode.Offline;
        vm.PlayCommand.Execute(null);
        _devices.Single().Pump();
        vm.BassVolume = .7;
        _devices.Single().Pump();
        Assert.True(vm.IsPlaying);
        Assert.Equal(.7, _store.Stored.Offline.Levels.Bass);
    }

    private sealed class FakeAudio : IAudioDevice
    {
        private RenderAudio? _render;public bool Disposed {get;private set;}public bool IsRunning=>!Disposed;public string? Error {get;set;}
        public void Start(RenderAudio render)=>_render=render;
        public void Pump()=>_render!(new float[1024]);
        public void Dispose()=>Disposed=true;
    }
}
