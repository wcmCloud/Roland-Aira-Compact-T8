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
    private long _clocksSent;

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

    /// <summary>
    /// Raised on the clock thread at the start of each 16th-note step, with the step counted from
    /// Start, right after the step's first clock is sent.
    /// </summary>
    public event Action<long>? StepStarted;

    /// <summary>Timing clocks sent since the last <see cref="Start"/>.</summary>
    public long ClocksSent => Interlocked.Read(ref _clocksSent);

    /// <summary>
    /// The 16th-note step the T-8's sequencer is on, counted from Start (6 clocks per step), or null
    /// before the first clock. The T-8 loops each part on its own length, so take it modulo the length.
    /// </summary>
    public long? CurrentStep => ClocksSent is > 0 and var clocks ? (clocks - 1) / (ClocksPerQuarterNote / 4) : null;

    /// <summary>Sends Start (the T-8 plays from the top of its pattern) and starts the clock.</summary>
    public void Start()
    {
        if (_running)
            return;

        Interlocked.Exchange(ref _clocksSent, 0);
        Begin(new StartEvent());
    }

    /// <summary>
    /// Sends Continue (recognized by the T-8: it resumes where it stopped) and restarts the clock,
    /// keeping the position.
    /// </summary>
    public void Continue()
    {
        if (_running)
            return;

        Begin(new ContinueEvent());
    }

    /// <summary>Stops the clock and sends Stop. The position is kept for <see cref="Continue"/>.</summary>
    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        // Stop may be called from a StepStarted handler on the clock thread itself.
        if (_thread is { } thread && thread != Thread.CurrentThread)
            thread.Join();
        _thread = null;
        _output.SendEvent(new StopEvent());
    }

    private void Begin(MidiEvent first)
    {
        _running = true;
        _output.SendEvent(first);
        _thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "T-8 MIDI clock" };
        _thread.Start();
    }

    private void Run()
    {
        var stopwatch = Stopwatch.StartNew();
        var nextTick = 0.0;
        var clock = new TimingClockEvent();

        while (_running)
        {
            _output.SendEvent(clock);
            var clocks = Interlocked.Increment(ref _clocksSent);
            if ((clocks - 1) % (ClocksPerQuarterNote / 4) == 0)
                StepStarted?.Invoke((clocks - 1) / (ClocksPerQuarterNote / 4));
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
