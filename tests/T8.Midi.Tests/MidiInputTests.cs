using Melanchall.DryWetMidi.Core;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class MidiInputTests
{
    [Fact]
    public void Parser_reads_real_time_bytes()
    {
        var events = new MidiByteParser().Feed([0xFE, 0xF8, 0xFA, 0xFB, 0xFC]).ToList();
        Assert.Collection(events,
            e => Assert.IsType<ActiveSensingEvent>(e),
            e => Assert.IsType<TimingClockEvent>(e),
            e => Assert.IsType<StartEvent>(e),
            e => Assert.IsType<ContinueEvent>(e),
            e => Assert.IsType<StopEvent>(e));
    }

    [Fact]
    public void Parser_reads_channel_messages_with_running_status()
    {
        var events = new MidiByteParser().Feed([0x99, 36, 100, 38, 90, 0xCF, 5]).ToList();

        var first = Assert.IsType<NoteOnEvent>(events[0]);
        var second = Assert.IsType<NoteOnEvent>(events[1]);
        var program = Assert.IsType<ProgramChangeEvent>(events[2]);
        Assert.Equal((36, 100, 9), ((int)first.NoteNumber, (int)first.Velocity, (int)first.Channel));
        Assert.Equal(38, (int)second.NoteNumber);
        Assert.Equal((5, 15), ((int)program.ProgramNumber, (int)program.Channel));
    }

    [Fact]
    public void Parser_handles_messages_split_across_reads_and_clock_in_between()
    {
        var parser = new MidiByteParser();
        Assert.Empty(parser.Feed([0x91, 48]));
        var events = parser.Feed([0xF8, 127]).ToList();

        Assert.IsType<TimingClockEvent>(events[0]);
        Assert.Equal(48, (int)Assert.IsType<NoteOnEvent>(events[1]).NoteNumber);
    }

    [Fact]
    public void Parser_skips_sysex_and_stray_data()
    {
        var events = new MidiByteParser().Feed([0x10, 0xF0, 0x41, 0x10, 0x42, 0xF7, 0xFE, 0xB0, 123, 0]).ToList();

        Assert.IsType<ActiveSensingEvent>(events[0]);
        Assert.Equal(123, (int)Assert.IsType<ControlChangeEvent>(events[1]).ControlNumber);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void Alsa_input_reads_events_from_the_device_file()
    {
        using var temp = TestPaths.CreateTempFolder();
        var file = Path.Combine(temp.Path, "midiC1D0");
        File.WriteAllBytes(file, [0xFE, 0x99, 42, 80]);
        var received = new List<MidiEvent>();

        using (var input = new AlsaRawMidiInput(new MidiPortInfo("T-8", file)))
        {
            input.EventReceived += (_, e) => { lock (received) received.Add(e.Event); };
            input.StartEventsListening();
            Assert.True(input.IsListeningForEvents);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                lock (received)
                {
                    if (received.Count >= 2)
                        break;
                }
                Thread.Sleep(10);
            }
            input.StopEventsListening();
            Assert.False(input.IsListeningForEvents);
        }

        Assert.IsType<ActiveSensingEvent>(received[0]);
        Assert.Equal(42, (int)Assert.IsType<NoteOnEvent>(received[1]).NoteNumber);
    }

    [Fact]
    public void Matching_input_prefers_the_same_name_then_any_t8()
    {
        MidiPortInfo[] inputs = [new("IAC", "IAC"), new("T-8 MIDI", "x"), new("T-8", "y")];

        Assert.Equal("y", MidiPorts.FindMatchingInput(inputs, new MidiPortInfo("T-8", "T-8"))?.Id);
        Assert.Equal("x", MidiPorts.FindMatchingInput(inputs, new MidiPortInfo("Roland T8", "z"))?.Id);
        Assert.Null(MidiPorts.FindMatchingInput([new MidiPortInfo("IAC", "IAC")], new MidiPortInfo("Synth", "s")));
    }
}
