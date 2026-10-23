namespace T8.Audio;

public enum AudioEventKind { Drum, BassOn, BassOff, Silence }
public readonly record struct AudioEvent(double Tick, AudioEventKind Kind, int Note = 0, int Velocity = 100);
public sealed record AudioTimeline(AudioEvent[] Events, double Length, double MainLength, double Bpm, bool Loop, double Start = 0);

/// <summary>
/// Single audio-thread owner; commands enter via bounded queues in OfflinePlayer.
/// Each drum keeps one sample position. A new hit restarts that voice instead of stacking another
/// copy, and a loop or pattern edit leaves an unreleased tail playing.
/// </summary>
public sealed class AudioRenderer
{
    private struct Voice { public float[]? Data; public int Position, Drum; public double Gain, Target, Attack, Fade, Tone, Decay, Low; }
    private readonly Voice[] _voices = new Voice[6];
    private readonly int[] _held = new int[128];
    private float[][] _kit;
    private SoundPreset _preset;
    private BassSound _sound;
    private AudioTimeline? _timeline, _pending;
    private double _pendingAt;
    private int _index, _bassNote = -1;
    private double _tick, _phase, _frequency, _target, _envelope, _velocity, _low, _band, _volume = .45, _bassAge;
    private bool _gate, _fill, _paused;
    private double _jumpAt = -1, _jumpTo;
    public int SampleRate { get; }
    public bool Running { get; private set; }
    public bool Finished { get; private set; }
    public bool JumpPending => _jumpAt >= 0;
    public long? CurrentStep => Running ? (long)(_tick / 24) : null;
    public double Position => _tick;
    public double Volume { get; set; } = .45;
    public AudioRenderer(SampleBank bank)
    {
        SampleRate = bank.SampleRate; _preset = SoundPreset.All[0]; _sound = _preset.Bass; _kit = bank.Kit(_preset);
    }
    public void SetSound(SoundPreset preset, float[][] kit, BassSound sound) { _preset = preset; _kit = kit; _sound = sound; }
    public void Start(AudioTimeline timeline, bool preservePosition = false)
    {
        if (preservePosition && Running)
        {
            _pending = timeline; _pendingAt = (Math.Floor(_tick / 24) + 1) * 24; return;
        }
        Begin(timeline, keepDrums: false);
    }
    private void Begin(AudioTimeline timeline, bool keepDrums)
    {
        // A live edit releases the bass at the next step. Drum samples keep the position they already have.
        if (keepDrums) ReleaseBass(); else Silence();
        _pending = null;
        _timeline = timeline; _tick = timeline.Start;
        _index = 0; while (_index < timeline.Events.Length && timeline.Events[_index].Tick < _tick) _index++;
        _fill = false; _jumpAt = -1; _paused = false; Running = true; Finished = false;
    }
    private void ReleaseBass() { Array.Clear(_held); _gate = false; _bassNote = -1; }
    public void Stop() { Running = false; _paused = false; Finished = false; _timeline = null; _pending = null; _tick = 0; _jumpAt = -1; Silence(); }
    public void Pause() { _paused = Running; Running = false; Silence(); }
    public void Resume() { if (_paused) { Running = true; _paused = false; } }
    public void QueueFill() { if (Running && _timeline is { } t && t.Length > t.MainLength) _fill = true; }
    public void QueueJump(double at, double to) { _jumpAt = at; _jumpTo = to; }
    public void Silence()
    {
        Array.Clear(_held); _gate = false; _bassNote = -1;
        for (int i = 0; i < _voices.Length; i++) { _voices[i].Target = 0; _voices[i].Attack = 0; _voices[i].Fade = 1.0 / (SampleRate * .004); }
    }
    public void Apply(AudioEvent e)
    {
        switch (e.Kind)
        {
            case AudioEventKind.Silence: Silence(); break;
            case AudioEventKind.Drum:
                if ((uint)e.Note >= (uint)_voices.Length) return;
                if (e.Note == 4) { _voices[5].Target = 0; _voices[5].Attack = 0; _voices[5].Fade = 1.0 / (SampleRate * .003); }
                var level = Math.Clamp(e.Velocity, 0, 127) / 127.0;
                _voices[e.Note] = new Voice { Data = _kit[e.Note], Drum = e.Note, Target = level, Attack = level / (SampleRate * .0015), Tone = _preset.Tone,
                    Decay = Math.Exp(-(1 / _preset.DrumDecay - 1) / (SampleRate * .2)) };
                break;
            case AudioEventKind.BassOn:
                if (e.Note is < 0 or > 127) return;
                _held[e.Note]++;
                _target = 440 * Math.Pow(2, (e.Note - 69) / 12.0);
                if (!_gate) { _frequency = _target; _bassAge = 0; }
                _gate = true; _bassNote = e.Note; _velocity = Math.Clamp(e.Velocity, 0, 127) / 127.0;
                break;
            case AudioEventKind.BassOff:
                if (e.Note is < 0 or > 127) return;
                _held[e.Note] = Math.Max(0, _held[e.Note] - 1);
                if (e.Note == _bassNote && _held[e.Note] == 0) _gate = false;
                break;
        }
    }
    public void Render(Span<float> stereo)
    {
        for (int frame = 0; frame < stereo.Length / 2; frame++)
        {
            if (Running && _pending is { } pending && _tick + 1e-8 >= _pendingAt)
            {
                var position = _tick % pending.MainLength;
                Begin(pending, keepDrums: true); _tick = position;
                while (_index < pending.Events.Length && pending.Events[_index].Tick < _tick - 1e-8) _index++;
            }
            if (Running && _timeline is { } t)
            {
                if (_jumpAt >= 0 && _tick >= _jumpAt) { _tick = _jumpTo + (_tick - _jumpAt); _index = 0; while (_index < t.Events.Length && t.Events[_index].Tick < _tick - 1e-8) _index++; _jumpAt = -1; Silence(); }
                var end = _fill ? t.Length : t.MainLength;
                if (_tick + 1e-8 >= end)
                {
                    if (!t.Loop) { Running = false; Finished = true; Silence(); }
                    // Let drum samples ring across the loop. Choking them here cuts every hat and kick
                    // off at the bar line. Note-offs in the pattern already release the bass.
                    else { _tick -= end; _index = 0; _fill = false; }
                }
                if (Running)
                {
                    while (_index < t.Events.Length && t.Events[_index].Tick <= _tick + 1e-8) Apply(t.Events[_index++]);
                    _tick += t.Bpm * 96 / (60 * SampleRate);
                }
            }
            double value = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                ref var v = ref _voices[i];
                if (v.Data is null || v.Position >= v.Data.Length) { v.Data = null; continue; }
                v.Low += v.Tone * (v.Data[v.Position++] - v.Low);
                if (v.Gain < v.Target) v.Gain = Math.Min(v.Target, v.Gain + v.Attack);
                value += v.Low * v.Gain;
                v.Gain = Math.Max(0, v.Gain * v.Decay - v.Fade);
                if (v.Position >= v.Data.Length || v.Gain < .00001) v.Data = null;
            }
            value += BassSample();
            _volume += .002 * (Math.Clamp(Volume, 0, 1) - _volume);
            value = Math.Tanh(value * _preset.Drive) / Math.Max(1, Math.Sqrt(_preset.Drive)) * _volume;
            stereo[frame * 2] = stereo[frame * 2 + 1] = (float)value;
        }
    }
    private double BassSample()
    {
        _envelope += (_gate ? 1 - _envelope : -_envelope) * (_gate ? .004 : 1 / (SampleRate * .015));
        if (_envelope < .000001) { _low = _band = 0; return 0; }
        _frequency += (_target - _frequency) * (1 - Math.Exp(-1 / (SampleRate * _sound.Slide)));
        var dt = Math.Min(.45, _frequency / SampleRate);
        _phase = (_phase + dt) % 1;
        double osc = 2 * _phase - 1 - Blep(_phase, dt);
        if (_sound.Wave == BassWave.Square) osc = (_phase < .5 ? 1 : -1) + Blep(_phase, dt) - Blep((_phase + .5) % 1, dt);
        // Stable topology-preserving state-variable low pass.
        double g = Math.Tan(Math.PI * Math.Min(_sound.Cutoff * (.4 + Math.Exp(-_bassAge++ / (SampleRate * _sound.Decay)) * (1 + _velocity)), SampleRate * .2) / SampleRate);
        double k = 1 / _sound.Resonance, a = 1 / (1 + g * (g + k));
        double band = a * (_band + g * (osc - _low));
        double low = _low + g * band;
        _band = 2 * band - _band; _low = 2 * low - _low;
        return low * _envelope * _velocity * .3;
    }
    private static double Blep(double t, double dt)
    {
        if (t < dt) { t /= dt; return t + t - t * t - 1; }
        if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
        return 0;
    }
}
