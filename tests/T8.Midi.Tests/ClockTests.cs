using Melanchall.DryWetMidi.Core;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class ClockTests
{
    [Fact]
    public void Clock_sends_start_ticks_and_stop()
    {
        var device = new FakeOutputDevice();
        using var clock = new MidiClockSender(device, bpm: 150);

        clock.Start();
        Assert.True(clock.IsRunning);
        Thread.Sleep(400);
        clock.Stop();
        Assert.False(clock.IsRunning);

        var events = device.Events;
        Assert.IsType<StartEvent>(events[0]);
        Assert.IsType<StopEvent>(events[^1]);

        // 150 BPM = 60 clocks per second, so 0.4 s is about 24 ticks.
        var ticks = events.Count(e => e is TimingClockEvent);
        Assert.InRange(ticks, 12, 36);
    }

    [Fact]
    public void Start_and_stop_are_idempotent()
    {
        var device = new FakeOutputDevice();
        using var clock = new MidiClockSender(device);

        clock.Stop();
        clock.Start();
        clock.Start();
        clock.Stop();
        clock.Stop();

        Assert.Single(device.Events.OfType<StartEvent>());
        Assert.Single(device.Events.OfType<StopEvent>());
    }

    [Theory]
    [InlineData(5, 20)]
    [InlineData(128, 128)]
    [InlineData(999, 300)]
    public void Tempo_is_clamped(double requested, double expected)
    {
        using var clock = new MidiClockSender(new FakeOutputDevice(), requested);
        Assert.Equal(expected, clock.Bpm);
    }

    [Fact]
    public void Dispose_stops_a_running_clock()
    {
        var device = new FakeOutputDevice();
        var clock = new MidiClockSender(device);
        clock.Start();
        clock.Dispose();
        Assert.IsType<StopEvent>(device.Events[^1]);
    }

    [Fact]
    public void Current_step_counts_six_clocks_per_step_from_start()
    {
        var device = new FakeOutputDevice();
        using var clock = new MidiClockSender(device, bpm: 300);
        Assert.Null(clock.CurrentStep);

        clock.Start();
        Thread.Sleep(300);
        clock.Stop();

        var clocks = device.Events.Count(e => e is TimingClockEvent);
        Assert.Equal(clocks, clock.ClocksSent);
        Assert.Equal((clocks - 1) / 6, clock.CurrentStep);

        clock.Start();
        Assert.True(clock.ClocksSent < clocks);
        clock.Stop();
    }
}

public class ThreadTickGeneratorTests
{
    [Fact]
    public void Ticks_steadily_at_the_interval()
    {
        using var clock = new Melanchall.DryWetMidi.Multimedia.MidiClock(false, new ThreadTickGenerator(), TimeSpan.FromMilliseconds(2));
        var ticks = 0;
        clock.Ticked += (_, _) => Interlocked.Increment(ref ticks);

        clock.Start();
        Thread.Sleep(500);
        clock.Stop();

        // 2 ms for 0.5 s is 250 ticks; allow for a slow test machine.
        Assert.InRange(ticks, 150, 260);
    }
}
