using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class T8InputListenerTests
{
    private static T8InputListener Create(out FakeInputDevice input, int pcChannel = 16)
    {
        input = new FakeInputDevice();
        input.StartEventsListening();
        return new T8InputListener(input, new T8MidiMap { ProgramChangeChannel = pcChannel });
    }

    private static ProgramChangeEvent Program(int number, int channel) =>
        new((SevenBitNumber)number) { Channel = (FourBitNumber)(channel - 1) };

    [Fact]
    public void Program_changes_on_pc_channel_become_bank_and_pattern()
    {
        using var listener = Create(out var input, pcChannel: 7);
        var changes = new List<(int, int)>();
        listener.PatternChanged += (_, slot) => changes.Add(slot);

        input.Receive(Program(0, 7));
        input.Receive(Program(17, 7));
        input.Receive(Program(63, 7));
        input.Receive(Program(5, 16)); // another channel
        input.Receive(Program(70, 7)); // not a T-8 pattern

        Assert.Equal([(1, 1), (2, 2), (4, 16)], changes);
    }

    [Fact]
    public void Follows_the_t8_sequencer_with_start_clock_continue_and_stop()
    {
        using var listener = Create(out var input);
        var running = new List<bool>();
        listener.RunningChanged += (_, r) => running.Add(r);
        Assert.Null(listener.CurrentStep);

        input.Receive(new TimingClockEvent()); // ignored before Start
        input.Receive(new StartEvent());
        for (var i = 0; i < 13; i++)
            input.Receive(new TimingClockEvent());
        Assert.Equal(2, listener.CurrentStep); // 6 clocks per step

        input.Receive(new StopEvent());
        Assert.Null(listener.CurrentStep);
        input.Receive(new ContinueEvent());
        input.Receive(new TimingClockEvent());
        Assert.Equal(2, listener.CurrentStep); // Continue keeps the position

        input.Receive(new StartEvent());
        input.Receive(new TimingClockEvent());
        Assert.Equal(0, listener.CurrentStep); // Start goes back to the top
        Assert.Equal([true, false, true], running);
    }

    [Fact]
    public void Measures_the_t8_tempo_from_its_clock()
    {
        var now = 0.0;
        var input = new FakeInputDevice();
        input.StartEventsListening();
        using var listener = new T8InputListener(input, new T8MidiMap(), () => now);
        input.Receive(new StartEvent());
        Assert.Null(listener.Bpm);

        // 24 clocks per quarter note at 150 BPM: one every 16.67 ms.
        for (var i = 0; i < 26; i++)
        {
            input.Receive(new TimingClockEvent());
            now += 60_000.0 / (150 * 24);
        }

        Assert.Equal(150, listener.Bpm!.Value, precision: 6);
        input.Receive(new StopEvent());
        Assert.Null(listener.Bpm);
    }

    [Fact]
    public void Opens_the_t8_input_and_closes_it()
    {
        var ports = FakePortProvider.WithT8();
        var listener = T8InputListener.Open(ports, new MidiPortInfo("T-8", "T-8"), new T8MidiMap());
        Assert.NotNull(listener);
        Assert.True(ports.OpenedInputs.Single().IsListeningForEvents);
        listener!.Dispose();
        Assert.True(ports.OpenedInputs.Single().IsDisposed);

        ports.Inputs = [];
        Assert.Null(T8InputListener.Open(ports, new MidiPortInfo("T-8", "T-8"), new T8MidiMap()));
    }
}
