using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using T8.Core.Patterns;
using T8.Core.Songs;

namespace T8.Midi;

/// <summary>Clock-driven song transport. No local timer and no generated MIDI clock.</summary>
public sealed class ExternalSongPlayer : IDisposable
{
    private readonly IInputDevice _input;
    private readonly IPatternPlayer _output;
    private readonly List<TimedEvent> _events;
    private readonly long _length;
    private readonly bool _loop;
    private readonly bool _programs;
    private readonly SongPlan _plan;
    private readonly object _gate = new();
    private long _clocks;
    private int _index;
    private readonly TimeProvider _time;
    private long _lastClock;
    public bool ClockLost { get; private set; }
    /// <summary>Called by the UI heartbeat; release held notes if a DAW disappears without Stop.</summary>
    public void CheckClockTimeout()
    {
        lock (_gate)
        {
            if (!IsRunning || _time.GetElapsedTime(_lastClock) < TimeSpan.FromSeconds(2)) return;
            IsRunning = false; ClockLost = true; _output.AllNotesOff();
            if (_programs) _output.SendExternalEvent(new StopEvent());
        }
    }
    public bool IsRunning { get; private set; }
    public bool Finished { get; private set; }
    public long CurrentStep => _clocks > 0 ? (_clocks - 1) / 6 : 0;

    public ExternalSongPlayer(IInputDevice input, IPatternPlayer output, Song song, PatternLibrary library, bool programs, bool loop, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _input = input;
        _output = output;
        _programs = programs;
        _loop = loop;
        _plan = SongPlan.Create(song, library);
        _length = (long)_plan.TotalSteps * PatternMidiBuilder.TicksPerStep;
        _events = new SongMidiExporter { Map = output.Map }.Build(song, library, song.Bpm ?? 120,
            programs ? SongMidiMode.Programs : SongMidiMode.Notes).GetTimedEvents().Where(e => e.Event is ChannelEvent && !programs).ToList();
        _input.EventReceived += Received;
    }

    private void Received(object? sender, MidiEventReceivedEventArgs e) => Receive(e.Event);
    public void Receive(MidiEvent message)
    {
        lock (_gate)
        {
            switch (message)
            {
                case StartEvent:
                    _output.AllNotesOff();
                    Finished = false;
                    _clocks = 0;
                    _index = 0;
                    IsRunning = true; ClockLost = false; _lastClock = _time.GetTimestamp();
                    // Select the first pattern before forwarding Start.
                    if (_programs)
                    {
                        _output.SendExternalEvent(new StopEvent());
                        SelectAt(0);
                    }
                    Dispatch(0);
                    if (_programs) _output.SendExternalEvent(message);
                    break;
                case ContinueEvent:
                    IsRunning = true; ClockLost = false; _lastClock = _time.GetTimestamp();
                    if (_programs) _output.SendExternalEvent(message);
                    break;
                case StopEvent:
                    IsRunning = false;
                    if (_programs) _output.SendExternalEvent(message);
                    _output.AllNotesOff();
                    break;
                case SongPositionPointerEvent position:
                    _output.AllNotesOff();
                    _clocks = (long)position.PointerValue * 6;
                    _index = 0;
                    var tick = _clocks * 4;
                    if (_loop && _length > 0) tick %= _length;
                    while (_index < _events.Count && _events[_index].Time < tick) _index++;
                    if (_programs) SelectAt(tick);
                    break;
                case TimingClockEvent when IsRunning:
                    _lastClock = _time.GetTimestamp();
                    var time = _clocks * 4; // 96 PPQ / 24 clocks
                    if (time >= _length)
                    {
                        if (!_loop)
                        {
                            Finished = true;
                            IsRunning = false;
                            if (_programs) _output.SendExternalEvent(new StopEvent());
                            _output.AllNotesOff();
                            return;
                        }
                        if (time % _length == 0) { _index = 0; _output.AllNotesOff(); }
                        time %= _length;
                    }
                    if (_programs && _clocks % 6 == 0)
                    {
                        var step = time / PatternMidiBuilder.TicksPerStep;
                        var index = _plan.PassAt(step);
                        var pass = _plan.Passes[index];
                        if (step == pass.StartStep + Math.Min(1, pass.Steps - 1))
                        {
                            var next = index + 1 < _plan.Passes.Count ? index + 1 : _loop ? 0 : -1;
                            if (next >= 0)
                            {
                                var upcoming = _plan.Passes[next];
                                if (upcoming.Bank != pass.Bank || upcoming.Pattern != pass.Pattern)
                                    _output.SelectPattern(upcoming.Bank, upcoming.Pattern);
                            }
                        }
                    }
                    Dispatch(time);
                    if (_programs) _output.SendExternalEvent(message);
                    _clocks++;
                    break;
            }
        }
    }

    private void SelectAt(long tick)
    {
        var pass = _plan.Passes[_plan.PassAt(tick / PatternMidiBuilder.TicksPerStep)];
        _output.SelectPattern(pass.Bank, pass.Pattern);
    }

    private void Dispatch(long time)
    {
        while (_index < _events.Count && _events[_index].Time <= time)
            _output.SendExternalEvent(_events[_index++].Event);
    }

    public void Dispose()
    {
        _input.EventReceived -= Received;
        _input.StopEventsListening();
        lock (_gate)
        {
            IsRunning = false;
            if (_programs) _output.SendExternalEvent(new StopEvent());
            _output.AllNotesOff();
        }
        (_input as IDisposable)?.Dispose();
    }
}
