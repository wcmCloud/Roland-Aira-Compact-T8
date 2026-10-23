using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using T8.Core.Patterns;

namespace T8.Midi;

/// <summary>
/// Plays one pattern from an external MIDI clock. No local timer and no generated clock.
/// Sequencer mode forwards Start, Continue, Stop and clock to the T-8. Notes mode sends the
/// pattern's notes and keeps the T-8 sequencer stopped.
/// </summary>
public sealed class ExternalPatternPlayer : IDisposable
{
    private readonly IInputDevice _input;
    private readonly IPatternPlayer _output;
    private readonly bool _programs;
    private readonly object _gate = new();
    private readonly Func<long> _nowMs;
    private List<TimedEvent> _events = [];
    private List<TimedEvent> _fillEvents = [];
    private long _loopTicks;
    private long _fillTicks;
    private long _clocks;
    private long _fillClock;
    private int _index;
    private int _fillIndex;
    private int _bank;
    private int _pattern;
    private long _lastClockMs;
    private bool _fillQueued;
    private bool _fillPlaying;
    private bool _disposed;

    public bool IsRunning { get; private set; }

    /// <summary>Raised when Start, Continue or Stop changes transport. Not raised for each clock.</summary>
    public event Action? Changed;

    /// <summary>The 16th-note step since Start, or null while stopped or during a fill.</summary>
    public long? CurrentStep
    {
        get
        {
            lock (_gate)
                return IsRunning && !_fillPlaying && _clocks > 0 ? (_clocks - 1) / 6 : null;
        }
    }

    public ExternalPatternPlayer(
        IInputDevice input,
        IPatternPlayer output,
        BassPattern? bass,
        RhythmPattern? rhythm,
        RhythmPattern? fill,
        int bank,
        int pattern,
        bool programs,
        Func<long>? nowMs = null)
    {
        _input = input;
        _output = output;
        _programs = programs;
        _bank = bank;
        _pattern = pattern;
        _nowMs = nowMs ?? (() => Environment.TickCount64);
        Build(bass, rhythm, fill);
        _input.EventReceived += Received;
    }

    /// <summary>Swaps the pattern without leaving the clock. The DAW keeps the position.</summary>
    public void Use(BassPattern? bass, RhythmPattern? rhythm, RhythmPattern? fill, int bank, int pattern)
    {
        lock (_gate)
        {
            _bank = bank;
            _pattern = pattern;
            Build(bass, rhythm, fill);
            if (_programs)
                _output.SelectPattern(bank, pattern);
            else
            {
                _output.AllNotesOff();
                SeekNotes(_loopTicks > 0 ? (_clocks * 4) % _loopTicks : 0);
            }
        }
    }

    /// <summary>Plays the fill once when the current loop ends. False when there is no fill or the clock is stopped.</summary>
    public bool QueueFill()
    {
        lock (_gate)
        {
            if (!IsRunning || _programs || _fillTicks == 0)
                return false;
            _fillQueued = true;
            return true;
        }
    }

    /// <summary>
    /// Silences output when clocks have stopped arriving without a Stop. Stays armed for the next Start.
    /// </summary>
    public bool SilenceIfQuiet(int quietMs)
    {
        lock (_gate)
        {
            if (!IsRunning || _nowMs() - _lastClockMs < quietMs)
                return false;
            IsRunning = false;
            _fillPlaying = false;
            _fillQueued = false;
            if (_programs)
                _output.SendExternalEvent(new StopEvent());
            _output.AllNotesOff();
            return true;
        }
    }

    private void Received(object? sender, MidiEventReceivedEventArgs e) => Receive(e.Event);

    public void Receive(MidiEvent message)
    {
        var notify = false;
        lock (_gate)
            notify = Handle(message);
        if (notify)
            Changed?.Invoke();
    }

    private bool Handle(MidiEvent message)
    {
        switch (message)
        {
            case StartEvent:
                _output.AllNotesOff();
                _clocks = 0;
                _fillClock = 0;
                _fillQueued = false;
                _fillPlaying = false;
                IsRunning = true;
                _lastClockMs = _nowMs();
                if (_programs)
                {
                    _output.SendExternalEvent(new StopEvent());
                    _output.SelectPattern(_bank, _pattern);
                    _output.SendExternalEvent(message);
                }
                else
                    SeekNotes(0);
                return true;
            case ContinueEvent:
                IsRunning = true;
                _lastClockMs = _nowMs();
                if (_programs)
                    _output.SendExternalEvent(message);
                return true;
            case StopEvent:
                IsRunning = false;
                _fillPlaying = false;
                _fillQueued = false;
                if (_programs)
                    _output.SendExternalEvent(message);
                _output.AllNotesOff();
                return true;
            case SongPositionPointerEvent position when !_programs:
                _output.AllNotesOff();
                _clocks = (long)position.PointerValue * 6;
                _fillPlaying = false;
                var tick = _clocks * 4;
                if (_loopTicks > 0)
                    tick %= _loopTicks;
                SeekNotes(tick);
                return false;
            case TimingClockEvent when IsRunning:
                _lastClockMs = _nowMs();
                OnClock(message);
                return false;
            default:
                return false;
        }
    }

    private void OnClock(MidiEvent clock)
    {
        if (_programs)
        {
            _output.SendExternalEvent(clock);
            _clocks++;
            return;
        }
        if (_loopTicks <= 0)
        {
            _clocks++;
            return;
        }
        if (_fillPlaying)
        {
            var time = _fillClock * 4;
            if (time >= _fillTicks)
            {
                _fillPlaying = false;
                _clocks = 0;
                ReleaseThrough(_fillEvents, _fillTicks);
                SeekNotes(0);
            }
            else
                Dispatch(_fillEvents, ref _fillIndex, time);
            _fillClock++;
        }
        else
        {
            var time = _clocks * 4;
            if (time >= _loopTicks && time % _loopTicks == 0)
            {
                // Note-offs only. All-sound-off would choke drum samples that are still playing.
                ReleaseThrough(_events, _loopTicks);
                if (_fillQueued)
                {
                    _fillQueued = false;
                    _fillPlaying = true;
                    _fillClock = 0;
                    _fillIndex = 0;
                    Dispatch(_fillEvents, ref _fillIndex, 0);
                    _fillClock++;
                    _clocks++;
                    return;
                }
                SeekNotes(0);
            }
            else
                Dispatch(_events, ref _index, time % _loopTicks);
        }
        _clocks++;
    }

    private void ReleaseThrough(List<TimedEvent> events, long end)
    {
        foreach (var timed in events)
            if (timed.Time >= end && timed.Event is NoteOffEvent off)
                _output.SendExternalEvent(off);
    }

    private void SeekNotes(long tick)
    {
        _index = 0;
        Dispatch(_events, ref _index, tick);
    }

    private void Dispatch(List<TimedEvent> events, ref int index, long time)
    {
        while (index < events.Count && events[index].Time <= time)
            _output.SendExternalEvent(events[index++].Event);
    }

    private void Build(BassPattern? bass, RhythmPattern? rhythm, RhythmPattern? fill)
    {
        if (!_output.Map.BassOn)
            bass = null;
        if (!_output.Map.RhythmOn)
        {
            rhythm = null;
            fill = null;
        }
        var builder = new PatternMidiBuilder { Map = _output.Map };
        _events = builder.Build(bass, rhythm).Where(e => e.Event is ChannelEvent).ToList();
        _loopTicks = builder.LengthInTicks(bass, rhythm);
        _fillEvents = [];
        _fillTicks = 0;
        if (fill is not null)
        {
            _fillEvents = builder.Build(bass, fill, fill.Length).Where(e => e.Event is ChannelEvent).ToList();
            _fillTicks = (long)fill.Length * PatternMidiBuilder.TicksPerStep;
        }
        if (_fillTicks == 0)
            _fillQueued = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _input.EventReceived -= Received;
        try
        {
            _input.StopEventsListening();
        }
        catch (Exception)
        {
            // Closing is best effort.
        }
        lock (_gate)
        {
            if (IsRunning && _programs)
                _output.SendExternalEvent(new StopEvent());
            IsRunning = false;
            _output.AllNotesOff();
        }
        (_input as IDisposable)?.Dispose();
    }
}
