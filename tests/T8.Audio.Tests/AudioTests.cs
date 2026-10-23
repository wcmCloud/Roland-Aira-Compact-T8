using T8.Audio;
using T8.Core.Patterns;
using T8.Core.Songs;
using T8.Midi;
namespace T8.Audio.Tests;

public sealed class AudioTests
{
    private static readonly SampleBank Bank = new();
    public static IEnumerable<object[]> Presets => SoundPreset.All.Select(p => new object[] { p.Id });
    private static double Energy(float[] buffer) => buffer.Sum(x => (double)x * x);
    [Theory, MemberData(nameof(Presets))]
    public void Every_preset_contains_six_audible_finite_drum_voices(string id)
    {
        var preset = SoundPreset.Find(id); var kit = Bank.Kit(preset);
        Assert.Equal(6, kit.Length);
        for (int drum = 0; drum < 6; drum++)
        {
            var r = new AudioRenderer(Bank); r.SetSound(preset, kit, preset.Bass);
            r.Apply(new(0, AudioEventKind.Drum, drum)); var output = new float[48000]; r.Render(output);
            Assert.True(Energy(output) > .01);
            Assert.All(output, x => Assert.True(float.IsFinite(x) && Math.Abs(x) <= 1));
        }
    }
    [Fact]
    public void Presets_are_sonically_distinct()
    {
        var energies = SoundPreset.All.Select(p => { var r = new AudioRenderer(Bank); r.SetSound(p, Bank.Kit(p), p.Bass); r.Apply(new(0, AudioEventKind.Drum)); var b = new float[48000]; r.Render(b); return Math.Round(Energy(b), 5); }).ToArray();
        Assert.Equal(6, energies.Distinct().Count());
    }
    [Theory]
    [InlineData(44100)] [InlineData(48000)]
    public void Samples_resample_and_preserve_duration(int rate)
    {
        var bank = new SampleBank(rate); var kick = bank.Kit(SoundPreset.All[0])[0];
        Assert.InRange(kick.Length / (double)rate, 1.49, 1.51);
    }
    [Fact]
    public void A_drum_keeps_ringing_across_the_loop()
    {
        var preset = SoundPreset.Find("classic");
        var r = new AudioRenderer(Bank);
        r.SetSound(preset, Bank.Kit(preset), preset.Bass);
        // Kick on the second step of a 2-step loop. It is still sounding when the loop restarts.
        r.Start(new([new(24, AudioEventKind.Drum)], 48, 48, 120, true));
        var output = new float[28000];
        r.Render(output);
        double tail = 0;
        for (int i = 26000; i < output.Length; i++) tail += output[i] * output[i];
        Assert.True(tail > .01);
    }
    [Fact]
    public void Samples_are_decoded_once_and_reused()
    {
        var first = SampleBank.Shared();
        var second = SampleBank.Shared();
        Assert.Same(first, second);
        Assert.Same(first.Kit(SoundPreset.All[0]), second.Kit(SoundPreset.All[0]));
        Assert.Same(first.Kit(SoundPreset.All[0])[0], second.Kit(SoundPreset.All[0])[0]);
    }
    [Fact]
    public void Retriggering_a_drum_restarts_it_instead_of_stacking_another_copy()
    {
        float[] Hit(bool again)
        {
            var preset = SoundPreset.Find("classic");
            var r = new AudioRenderer(Bank);
            r.SetSound(preset, Bank.Kit(preset), preset.Bass);
            r.Apply(new(0, AudioEventKind.Drum));
            if (again) r.Render(new float[8000]);
            var output = new float[4000];
            if (again) r.Apply(new(0, AudioEventKind.Drum));
            r.Render(output);
            return output;
        }
        var fresh = Hit(false);
        var again = Hit(true);
        double error = 0, energy = 0;
        for (int i = 400; i < fresh.Length; i++)
        {
            var delta = fresh[i] - again[i];
            error += delta * delta;
            energy += fresh[i] * fresh[i];
        }
        Assert.True(error < energy * .05);
    }
    [Fact]
    public void Replacing_the_pattern_does_not_cut_a_ringing_drum()
    {
        var preset = SoundPreset.Find("classic");
        var r = new AudioRenderer(Bank);
        r.SetSound(preset, Bank.Kit(preset), preset.Bass);
        r.Start(new([new(0, AudioEventKind.Drum)], 96, 96, 120, true));
        r.Render(new float[2000]);
        r.Start(new([], 96, 96, 120, true), preservePosition: true);
        r.Render(new float[8000]);
        var tail = new float[8000];
        r.Render(tail);
        Assert.True(Energy(tail) > .01);
    }
    [Fact]
    public void Invalid_wave_is_rejected() => Assert.Throws<InvalidDataException>(() => SampleBank.ReadWave(new MemoryStream(new byte[32]), 48000));
    [Fact]
    public void Native_context_and_device_use_the_loaded_library_size()
    {
        // A too-small ma_context aborts the process on Linux instead of throwing.
        try { Assert.NotNull(AudioDevice.ListOutputs()); }
        catch (InvalidOperationException ex) { Assert.Contains("Audio device", ex.Message); }

        using var device = new AudioDevice();
        try
        {
            device.Start(buffer => buffer.Clear());
            Assert.True(device.IsRunning);
        }
        catch (InvalidOperationException ex) { Assert.Contains("Audio", ex.Message); }
    }
    [Fact]
    public void Timeline_uses_audio_frames_and_stops_at_exact_duration()
    {
        var r = new AudioRenderer(Bank); r.Start(new([new(24, AudioEventKind.Drum)], 48, 48, 120, false));
        var before = new float[12000]; r.Render(before); Assert.Equal(0, Energy(before)); Assert.InRange(r.Position, 23.99999, 24.00001);
        var after = new float[12002]; r.Render(after); Assert.True(Energy(after) > 0); Assert.True(r.Finished);
    }
    [Fact]
    public void Loop_has_no_accumulated_buffer_rounding_drift()
    {
        var r = new AudioRenderer(Bank); r.Start(new([], 96, 96, 137, true));
        var b = new float[512]; for (int i = 0; i < 10000; i++) r.Render(b);
        double expected = (2560000d * 137 * 96 / (60 * 48000)) % 96;
        Assert.InRange(Math.Abs(r.Position - expected), 0, .00001);
    }
    [Fact]
    public void Pause_preserves_position_and_stop_resets_it()
    {
        var r = new AudioRenderer(Bank); r.Start(new([], 384, 384, 120, true)); var b = new float[1024]; r.Render(b);
        var position = r.Position; r.Pause(); r.Render(b); Assert.Equal(position, r.Position); Assert.False(r.Finished);
        r.Resume(); r.Render(b); Assert.True(r.Position > position); r.Stop(); Assert.Equal(0, r.Position);
    }
    [Fact]
    public void Fill_runs_once_then_returns_to_main_loop()
    {
        var r = new AudioRenderer(Bank); r.Start(new([], 48, 24, 120, true)); r.QueueFill();
        r.Render(new float[18000]); Assert.InRange(r.Position, 35.99, 36.01);
        r.Render(new float[18000]); Assert.InRange(r.Position, 23.99, 24.01);
        r.Render(new float[6000]); Assert.InRange(r.Position, 11.99, 12.01);
    }
    [Fact]
    public void Jump_occurs_at_requested_boundary()
    {
        var r = new AudioRenderer(Bank); r.Start(new([], 384, 384, 120, true)); r.QueueJump(24, 96);
        r.Render(new float[12002]); Assert.False(r.JumpPending); Assert.InRange(r.Position, 96, 96.01);
    }
    [Theory]
    [InlineData(BassWave.Saw)] [InlineData(BassWave.Square)]
    public void Bass_release_eventually_silences_and_extreme_settings_remain_finite(BassWave wave)
    {
        var r = new AudioRenderer(Bank); var p = SoundPreset.All[0]; r.SetSound(p, Bank.Kit(p), new(wave,10000,4,.04,.005));
        r.Apply(new(0, AudioEventKind.BassOn, 96, 127)); var b = new float[48000]; r.Render(b); Assert.True(Energy(b)>1);
        Assert.All(b, x => Assert.True(float.IsFinite(x) && Math.Abs(x) < 1));
        r.Apply(new(0, AudioEventKind.BassOff,96)); r.Render(new float[96000]); r.Render(b); Assert.True(Energy(b)<1e-9);
    }
    [Fact]
    public void Old_note_off_does_not_cut_sliding_note()
    {
        var r = new AudioRenderer(Bank); r.Apply(new(0, AudioEventKind.BassOn, 36)); r.Render(new float[12000]);
        r.Apply(new(0, AudioEventKind.BassOn, 48)); r.Apply(new(0, AudioEventKind.BassOff,36));
        var b = new float[24000]; r.Render(b); Assert.True(Energy(b)>1);
    }
    [Fact]
    public void Same_pitch_overlap_survives_first_note_off()
    {
        var r = new AudioRenderer(Bank); r.Apply(new(0, AudioEventKind.BassOn,36)); r.Apply(new(0, AudioEventKind.BassOn,36)); r.Apply(new(0, AudioEventKind.BassOff,36));
        var b = new float[24000]; r.Render(b); Assert.True(Energy(b)>1);
    }
    [Fact]
    public void Accent_increases_bass_energy()
    {
        double Render(int velocity) { var r = new AudioRenderer(Bank); r.Apply(new(0,AudioEventKind.BassOn,36,velocity)); var b=new float[24000];r.Render(b);return Energy(b); }
        Assert.True(Render(127)>Render(70));
    }
    [Fact]
    public void Closed_hat_chokes_open_hat()
    {
        var r = new AudioRenderer(Bank); r.Apply(new(0, AudioEventKind.Drum,5)); r.Render(new float[1024]); r.Apply(new(0, AudioEventKind.Drum,4));
        r.Render(new float[48000]); var b = new float[12000]; r.Render(b); Assert.True(Energy(b)<1e-9);
    }
    [Fact]
    public void Heavy_polyphony_is_bounded_and_silence_releases_all_voices()
    {
        var r = new AudioRenderer(Bank); for(int i=0;i<500;i++) r.Apply(new(0, AudioEventKind.Drum,i%6,127));
        var b=new float[24000];r.Render(b);Assert.All(b,x=>Assert.InRange(x,-1,1));r.Silence();r.Render(b);r.Render(b);Assert.True(Energy(b)<1e-9);
    }
    [Fact]
    public void Custom_midi_notes_do_not_change_offline_instrument_mapping()
    {
        var map = new T8MidiMap(); map.RhythmNotes[RhythmTrack.BD]=99;map.BassChannel=7;
        var device=new FakeAudio();using var p=new OfflinePlayer(map,new(),device);
        var rhythm=new RhythmPattern();rhythm.Steps[0][RhythmTrack.BD].On=true;rhythm.Steps[0][RhythmTrack.BD].Velocity=7;
        p.PlayPreview(null,rhythm,120);Assert.True(Energy(device.Render(4096))>0);Assert.Equal(99,map.RhythmNotes[RhythmTrack.BD]);
    }
    [Fact]
    public void Disabled_parts_remain_silent_offline()
    {
        var device=new FakeAudio();using var p=new OfflinePlayer(new T8MidiMap {BassChannel=0,RhythmChannel=0},new(),device);
        p.TriggerDrum(RhythmTrack.BD);p.BassNoteOn(36);Assert.Equal(0,Energy(device.Render(4096)));
    }
    [Fact]
    public void Song_from_pass_finishes_without_hardware()
    {
        var lib=PatternLibrary.CreateBlank(); var song=new Song();song.Entries.Add(new(){Bank=1,Pattern=1,Repeats=2});
        lib.Rhythm[0,0].Length=1;lib.Bass[0,0].Length=1;
        var device=new FakeAudio();using var p=new OfflinePlayer(new(),new(),device);
        p.PlaySong(SongPlan.Create(song,lib),lib,120,false,1);device.Render(12002);Assert.True(p.PreviewFinished);
    }
    [Fact]
    public void Settings_recover_invalid_values_and_clone_independently()
    {
        var s=new OfflineSettings {PresetId="missing",Volume=double.NaN,Bass=new((BassWave)99,-1,double.PositiveInfinity,-1,-1)};s.Validate();
        Assert.Equal("classic",s.PresetId);Assert.Equal(.45,s.Volume);Assert.Equal(BassWave.Saw,s.Bass!.Wave);Assert.Equal(80,s.Bass.Cutoff);
        var c=s.Clone();c.PresetId="deep";Assert.Equal("classic",s.PresetId);
    }
    private sealed class FakeAudio : IAudioDevice
    {
        private RenderAudio? _render;public bool IsRunning {get;private set;}public string? Error=>null;
        public void Start(RenderAudio render){_render=render;IsRunning=true;}
        public float[] Render(int samples){var b=new float[samples];_render!(b);return b;}
        public void Dispose()=>IsRunning=false;
    }
}
