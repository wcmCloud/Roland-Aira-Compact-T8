using System.Diagnostics;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using T8.Core.Patterns;
using T8.Midi.Ports;

namespace T8.Midi;

/// <summary>
/// Follows what the T-8 transmits (MIDI Implementation Chart): Start, Stop and Clock when its own
/// sequencer plays, and a program change on Pc.Ch when its pattern changes (needs tXPc on). Events
/// are raised on the MIDI input thread.
/// </summary>
public sealed class T8InputListener : IDisposable
{
    private const int ClocksPerStep = 6; // 24 per quarter note, 4 steps per quarter note

    private readonly IInputDevice _input;
    private readonly T8MidiMap _map;
    private readonly Stopwatch _clockTimer = new();
    private readonly Queue<double> _clockGaps = new();
    private long _clocks;
    private volatile bool _running;
    private double _lastClockMs = -1;

    public T8InputListener(IInputDevice input, T8MidiMap map)
    {
        _input = input;
        _map = map;
        _input.EventReceived += OnEventReceived;
    }

    /// <summary>Opens the T-8's MIDI input that belongs to <paramref name="output"/>, or returns null if there is none.</summary>
    public static T8InputListener? Open(IMidiPortProvider ports, MidiPortInfo output, T8MidiMap map)
    {
        var port = MidiPorts.FindMatchingInput(ports.ListInputs(), output);
        if (port is null)
            return null;
        var listener = new T8InputListener(ports.OpenInput(port), map);
        listener._input.StartEventsListening();
        return listener;
    }

    /// <summary>The T-8 changed pattern: bank 1–4 and pattern 1–16.</summary>
    public event EventHandler<(int Bank, int Pattern)>? PatternChanged;

    /// <summary>The T-8's sequencer started or stopped.</summary>
    public event EventHandler<bool>? RunningChanged;

    /// <summary>True between the T-8's Start (or Continue) and Stop.</summary>
    public bool IsRunning => _running;

    /// <summary>The step the T-8's sequencer is on, counted from Start, or null when it isn't playing.</summary>
    public long? CurrentStep => _running && Interlocked.Read(ref _clocks) is > 0 and var clocks ? (clocks - 1) / ClocksPerStep : null;

    /// <summary>The T-8's tempo, measured from its clock over the last beat, or null when not known.</summary>
    public double? Bpm
    {
        get
        {
            lock (_clockGaps)
            {
                if (!_running || _clockGaps.Count < 12)
                    return null;
                var msPerClock = _clockGaps.Average();
                return msPerClock > 0 ? 60_000.0 / (msPerClock * 24) : null;
            }
        }
    }

    /// <summary>Handles one received event; public so tests can feed events directly.</summary>
    public void Receive(MidiEvent midiEvent)
    {
        switch (midiEvent)
        {
            case StartEvent:
                Interlocked.Exchange(ref _clocks, 0);
                SetRunning(true);
                break;
            case ContinueEvent:
                SetRunning(true);
                break;
            case StopEvent:
                SetRunning(false);
                break;
            case TimingClockEvent when _running:
                Interlocked.Increment(ref _clocks);
                MeasureClock();
                break;
            case ProgramChangeEvent program when program.Channel + 1 == _map.ProgramChangeChannel && program.ProgramNumber < 64:
                PatternChanged?.Invoke(this, (program.ProgramNumber / PatternLibrary.PatternsPerBank + 1,
                    program.ProgramNumber % PatternLibrary.PatternsPerBank + 1));
                break;
        }
    }

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e) => Receive(e.Event);

    private void SetRunning(bool running)
    {
        if (_running == running)
            return;
        _running = running;
        lock (_clockGaps)
        {
            _clockGaps.Clear();
            _lastClockMs = -1;
        }
        _clockTimer.Restart();
        RunningChanged?.Invoke(this, running);
    }

    private void MeasureClock()
    {
        var now = _clockTimer.Elapsed.TotalMilliseconds;
        lock (_clockGaps)
        {
            if (_lastClockMs >= 0)
            {
                _clockGaps.Enqueue(now - _lastClockMs);
                if (_clockGaps.Count > 24)
                    _clockGaps.Dequeue();
            }
            _lastClockMs = now;
        }
    }

    public void Dispose()
    {
        _input.EventReceived -= OnEventReceived;
        try
        {
            _input.StopEventsListening();
        }
        catch (Exception)
        {
            // Closing is best effort.
        }
        (_input as IDisposable)?.Dispose();
    }
}
