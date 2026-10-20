using System.Diagnostics;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace T8.Midi;

/// <summary>
/// Sends MIDI Start, 24-per-quarter-note timing clock and Stop, so the T-8 plays its own
/// pattern at our tempo. The T-8's MIDI Clock Sync (SYnC) must be AUTO or USB.
/// </summary>
public sealed class MidiClockSender : IDisposable
{
    private const int ClocksPerQuarterNote = 24;

    private readonly IOutputDevice _output;
    private Thread? _thread;
    private volatile bool _running;
    private double _bpm;

    public MidiClockSender(IOutputDevice output, double bpm = 120)
    {
        _output = output;
        Bpm = bpm;
    }

    public double Bpm
    {
        get => _bpm;
        set => _bpm = Math.Clamp(value, 20, 300);
    }

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running)
            return;

        _running = true;
        _output.SendEvent(new StartEvent());
        _thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "T-8 MIDI clock" };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        _thread?.Join();
        _thread = null;
        _output.SendEvent(new StopEvent());
    }

    private void Run()
    {
        var stopwatch = Stopwatch.StartNew();
        var nextTick = 0.0;
        var clock = new TimingClockEvent();

        while (_running)
        {
            _output.SendEvent(clock);
            nextTick += 60_000.0 / (Bpm * ClocksPerQuarterNote);

            // Sleep most of the gap, then spin for the last millisecond for steady timing.
            while (_running)
            {
                var remaining = nextTick - stopwatch.Elapsed.TotalMilliseconds;
                if (remaining <= 0)
                    break;
                if (remaining > 2)
                    Thread.Sleep(1);
                else
                    Thread.SpinWait(50);
            }
        }
    }

    public void Dispose() => Stop();
}
