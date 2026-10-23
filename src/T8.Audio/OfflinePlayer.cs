using System.Collections.Concurrent;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using T8.Core.Patterns;
using T8.Core.Songs;
using T8.Midi;
namespace T8.Audio;

/// <summary>Audio-clock preview with the same musical event compiler as MIDI export.</summary>
public sealed class OfflinePlayer : IPatternPlayer
{
    private readonly SampleBank _bank;
    private readonly AudioRenderer _renderer;
    private readonly IAudioDevice _device;
    private readonly ConcurrentQueue<Action<AudioRenderer>> _commands = new();
    private int _commandCount;

    private bool _disposed;
    private volatile bool _running, _finished, _jumpPending;
    private long _step = -1;
    public bool IsOffline => true;
    public T8MidiMap Map { get; }
    public MidiClockSender Clock { get; } = new(new SilentOutput());
    public bool PreviewFinished => _finished;
    public bool IsSongJumpPending => _jumpPending;
    public long? CurrentStep => Interlocked.Read(ref _step) is var s && s >= 0 ? s : null;
    public string? Error => _device.Error ?? (!_device.IsRunning && !_disposed ? "Audio output stopped. Select an audio output and press Play to retry." : null);
    public OfflinePlayer(T8MidiMap hardwareMap, OfflineSettings settings, IAudioDevice? device = null)
    {
        // Canonical internal routing: user MIDI remapping must never change drum identities.
        Map = new T8MidiMap { BassChannel = hardwareMap.BassOn ? 2 : 0, RhythmChannel = hardwareMap.RhythmOn ? 10 : 0 };
        _bank = SampleBank.Shared(); _renderer = new AudioRenderer(_bank);
        _device = device ?? new AudioDevice(settings.DeviceName);
        Configure(settings);
        try { _device.Start(Render); } catch { _device.Dispose(); Clock.Dispose(); throw; }
    }
    public void Configure(OfflineSettings settings)
    {
        var copy = settings.Clone(); copy.Validate(); var preset = SoundPreset.Find(copy.PresetId); var kit = _bank.Kit(preset);
        var bass = OfflineSettings.Sanitize(copy.Bass ?? preset.Bass);
        Post(r => { r.SetSound(preset, kit, bass); r.Volume = copy.Volume; });
    }
    private void Post(Action<AudioRenderer> command)
    {
        if (_disposed) return;
        if (Interlocked.Increment(ref _commandCount) > 256)
        {
            Interlocked.Decrement(ref _commandCount);
            throw new InvalidOperationException("Audio command queue is full. Stop and retry playback.");
        }
        _commands.Enqueue(command);
    }
    private void Render(Span<float> buffer)
    {
        // Bounded work; no waiting on the UI or a producer thread.
        for (int i = 0; i < 256 && _commands.TryDequeue(out var command); i++) { Interlocked.Decrement(ref _commandCount); command(_renderer); }
        _renderer.Render(buffer);
        _running = _renderer.Running; _finished = _renderer.Finished; _jumpPending = _renderer.JumpPending;
        Interlocked.Exchange(ref _step, _renderer.CurrentStep ?? -1);
    }
    private AudioEvent[] Compile(IEnumerable<TimedEvent> events) => events.Select(e => Convert(e.Event, e.Time)).Where(e => e.HasValue).Select(e => e!.Value).OrderBy(e => e.Tick).ToArray();
    private AudioEvent? Convert(MidiEvent e, double tick = 0)
    {
        if (e is ControlChangeEvent cc && (int)cc.ControlNumber is 120 or 123) return new(tick, AudioEventKind.Silence);
        if (e is not NoteEvent n) return null;
        bool on = e is NoteOnEvent note && note.Velocity > 0;
        int velocity = e is NoteOnEvent no ? no.Velocity : 0;
        if (Map.BassOn && (int)n.Channel == Map.BassChannel - 1) return new(tick, on ? AudioEventKind.BassOn : AudioEventKind.BassOff, n.NoteNumber, velocity);
        if (on && Map.RhythmOn && (int)n.Channel == Map.RhythmChannel - 1)
        {
            var drum = Array.IndexOf(new[] { 36, 38, 47, 50, 42, 46 }, (int)n.NoteNumber);
            if (drum >= 0) return new(tick, AudioEventKind.Drum, drum, velocity);
        }
        return null;
    }
    private bool _hasFill;
    public void PlayPreview(BassPattern? bass, RhythmPattern? rhythm, double bpm, bool loop = true, int? seed = null, RhythmPattern? fill = null) =>
        Preview(bass, rhythm, bpm, loop, seed, fill, false);
    public void UpdatePreview(BassPattern? bass, RhythmPattern? rhythm, double bpm, RhythmPattern? fill) => Preview(bass, rhythm, bpm, true, null, fill, true);
    private void Preview(BassPattern? bass, RhythmPattern? rhythm, double bpm, bool loop, int? seed, RhythmPattern? fill, bool preserve)
    {
        if (!Map.BassOn) bass = null;
        if (!Map.RhythmOn) { rhythm = null; fill = null; }
        var builder = new PatternMidiBuilder { Map = Map, Seed = seed };
        long length = Math.Max(24, builder.LengthInTicks(bass, rhythm));
        var events = builder.Build(bass, rhythm); long end = length;
        _hasFill = fill is not null;
        if (fill is not null)
        {
            foreach (var e in builder.Build(bass, fill, fill.Length)) { e.Time += length; events.Add(e); }
            end += fill.Length * 24;
        }
        var timeline = new AudioTimeline(Compile(events), end, length, Math.Clamp(bpm, 20, 300), loop);
        _finished = false; Post(r => r.Start(timeline, preserve));
    }
    public void PlaySong(SongPlan plan, PatternLibrary library, double bpm, bool loop, int fromPass = 0)
    {
        if (plan.TotalSteps <= 0) return;
        var builder = new PatternMidiBuilder { Map = Map }; var events = new List<TimedEvent>();
        foreach (var pass in plan.Passes)
            foreach (var e in builder.Build(library.Bass[pass.Bank - 1, pass.Pattern - 1], library.Rhythm[pass.Bank - 1, pass.Pattern - 1], pass.Steps))
            { e.Time += pass.StartStep * 24; events.Add(e); }
        var start = plan.Passes[Math.Clamp(fromPass, 0, plan.Passes.Count - 1)].StartStep * 24;
        var timeline = new AudioTimeline(Compile(events), plan.TotalSteps * 24, plan.TotalSteps * 24, Math.Clamp(bpm, 20, 300), loop, start);
        _hasFill = false; _finished = false; Post(r => r.Start(timeline));
    }
    public void QueueSongJump(long atStep, long toStep) { _jumpPending = true; Post(r => r.QueueJump(atStep * 24, toStep * 24)); }
    public bool QueueFill() { if (!_hasFill) return false; Post(r => r.QueueFill()); return true; }
    public void PausePreview() => Post(r => r.Pause());
    public void ResumePreview() => Post(r => r.Resume());
    public void StopPreview() { _hasFill = false; _finished = false; _jumpPending = false; Post(r => r.Stop()); }
    public void AllNotesOff() => Post(r => r.Silence());
    public void TriggerDrum(RhythmTrack track, int velocity = 100)
    {
        int drum = Array.IndexOf(T8MidiMap.PlayableTracks.ToArray(), track);
        if (Map.RhythmOn && drum >= 0) Post(r => r.Apply(new(0, AudioEventKind.Drum, drum, velocity)));
    }
    public void BassNoteOn(int note, int velocity = 100) { if (Map.BassOn) Post(r => r.Apply(new(0, AudioEventKind.BassOn, note, velocity))); }
    public void BassNoteOff(int note) => Post(r => r.Apply(new(0, AudioEventKind.BassOff, note)));
    public void SelectPattern(int bank, int pattern) { }
    public void SendExternalEvent(MidiEvent midiEvent) { if (Convert(midiEvent) is { } e) Post(r => r.Apply(e)); }
    public void Dispose() { if (_disposed) return; _disposed = true; _device.Dispose(); _commands.Clear(); Clock.Dispose(); }
    private sealed class SilentOutput : IOutputDevice
    {
        public event EventHandler<MidiEventSentEventArgs>? EventSent { add { } remove { } }
        public void Dispose() { }
        public void PrepareForEventsSending() { }
        public void SendEvent(MidiEvent midiEvent) { }
    }
}
