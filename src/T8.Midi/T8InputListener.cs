using System.Diagnostics;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using T8.Core.Patterns;
using T8.Midi.Ports;

namespace T8.Midi;

/// <summary>
/// Follows what the T-8 transmits (MIDI Implementation Chart): Start, Stop and Clock when its own
/// sequencer plays, a program change on Pc.Ch when its pattern changes (needs tXPc on), and the bass
/// notes played on its keyboard. Events are raised on the MIDI input thread.
/// </summary>
public sealed class T8InputListener : IDisposable
{
    private const int ClocksPerStep = 6; // 24 per quarter note, 4 steps per quarter note

    private readonly IInputDevice _input;
    private readonly T8MidiMap _map;
    private readonly Stopwatch _clockTimer = new();
    private readonly Func<double> _nowMs;
    private readonly Queue<double> _clockGaps = new();
    private long _clocks;
    private volatile bool _running;
    private double _lastClockMs = -1;

    /// <param name="nowMs">Time source in milliseconds for the tempo measurement; tests pass their own.</param>
    public T8InputListener(IInputDevice input, T8MidiMap map, Func<double>? nowMs = null)
    {
        _input = input;
        _map = map;
        _clockTimer.Start(); // one time base for the tempo and for "sequencer active"
        _nowMs = nowMs ?? (() => _clockTimer.Elapsed.TotalMilliseconds);
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

    /// <summary>The T-8's bass range: the notes it sends and plays.</summary>
    public const int LowestBassNote = 12, HighestBassNote = 96;

    /// <summary>How long after the T-8's last Start, Continue or clock its notes still count as its sequencer's.</summary>
    public const double SequencerQuietMs = 1000;

    /// <summary>
    /// A bass key was pressed or released on the T-8: a note on (velocity 0 counts as off) or note off on the bass
    /// channel (bACh), in the T-8's range. <c>FromSequencer</c> is true while the T-8's own sequencer is (or was
    /// within the last second) playing, when the note is probably its pattern rather than a key.
    /// </summary>
    public event EventHandler<BassKeyEvent>? BassNote;

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
                MarkActivity();
                break;
            case ContinueEvent:
                SetRunning(true);
                MarkActivity();
                break;
            case StopEvent:
                SetRunning(false);
                Volatile.Write(ref _lastActivityMs, double.NegativeInfinity);
                break;
            case TimingClockEvent:
                MarkActivity();
                if (_running)
                {
                    Interlocked.Increment(ref _clocks);
                    MeasureClock();
                }
                break;
            case NoteOnEvent note when IsBassKey(note.Channel, note.NoteNumber):
                BassNote?.Invoke(this, new BassKeyEvent(note.NoteNumber, note.Velocity > 0, IsSequencerActive));
                break;
            case NoteOffEvent note when IsBassKey(note.Channel, note.NoteNumber):
                BassNote?.Invoke(this, new BassKeyEvent(note.NoteNumber, false, IsSequencerActive));
                break;
            case ProgramChangeEvent program when program.Channel + 1 == _map.ProgramChangeChannel && program.ProgramNumber < 64:
                PatternChanged?.Invoke(this, (program.ProgramNumber / PatternLibrary.PatternsPerBank + 1,
                    program.ProgramNumber % PatternLibrary.PatternsPerBank + 1));
                break;
        }
    }

    private bool IsBassKey(Melanchall.DryWetMidi.Common.FourBitNumber channel, int note) =>
        _map.BassChannel != 0 && channel + 1 == _map.BassChannel && note is >= LowestBassNote and <= HighestBassNote;

    private double _lastActivityMs = double.NegativeInfinity;

    /// <summary>
    /// True while the T-8's sequencer is playing: from Start or Continue, and while its clock keeps coming, until
    /// Stop or a second without clock.
    /// </summary>
    public bool IsSequencerActive => _nowMs() - Volatile.Read(ref _lastActivityMs) < SequencerQuietMs;

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e) => Receive(e.Event);

    private void MarkActivity() => Volatile.Write(ref _lastActivityMs, _nowMs());

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
        RunningChanged?.Invoke(this, running);
    }

    private void MeasureClock()
    {
        var now = _nowMs();
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

/// <summary>A bass key on the T-8: the note, pressed or released, and whether its sequencer was playing.</summary>
public readonly record struct BassKeyEvent(int Note, bool Pressed, bool FromSequencer);
