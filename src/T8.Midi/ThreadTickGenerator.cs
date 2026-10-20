using System.Diagnostics;
using Melanchall.DryWetMidi.Multimedia;

namespace T8.Midi;

/// <summary>
/// A tick generator on its own high-priority thread, timed with a stopwatch. DryWetMIDI's
/// high-precision generator is Windows/macOS only, and its regular one ticks irregularly on Linux
/// (about 80 ticks a second for a 1 ms interval), which leaves gaps of hundreds of milliseconds in
/// playback. This one keeps to the interval the way <see cref="MidiClockSender"/> does.
/// </summary>
public sealed class ThreadTickGenerator : TickGenerator
{
    private Thread? _thread;
    private volatile bool _running;

    protected override void Start(TimeSpan interval)
    {
        if (_running)
            return;
        _running = true;
        _thread = new Thread(() => Run(interval)) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "T-8 playback clock" };
        _thread.Start();
    }

    protected override void Stop()
    {
        _running = false;
        if (_thread is { } thread && thread != Thread.CurrentThread)
            thread.Join();
        _thread = null;
    }

    private void Run(TimeSpan interval)
    {
        var stopwatch = Stopwatch.StartNew();
        var next = interval.TotalMilliseconds;
        while (_running)
        {
            // Sleep most of the gap, then spin for the last moment so ticks land on time.
            var remaining = next - stopwatch.Elapsed.TotalMilliseconds;
            if (remaining > 1.5)
            {
                Thread.Sleep(1);
                continue;
            }
            if (remaining > 0)
            {
                Thread.SpinWait(30);
                continue;
            }

            GenerateTick();
            next += interval.TotalMilliseconds;
            // After a long stall (e.g. the computer was busy), don't fire a burst of catch-up ticks.
            if (stopwatch.Elapsed.TotalMilliseconds - next > 50)
                next = stopwatch.Elapsed.TotalMilliseconds + interval.TotalMilliseconds;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Stop();
        base.Dispose(disposing);
    }
}
