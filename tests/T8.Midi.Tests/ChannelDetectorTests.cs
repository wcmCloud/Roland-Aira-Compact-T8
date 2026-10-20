using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class ChannelDetectorTests
{
    private static readonly MidiPortInfo T8 = new("T-8", "T-8");

    private static NoteOnEvent Note(int note, int channel, int velocity = 100) =>
        new((SevenBitNumber)note, (SevenBitNumber)velocity) { Channel = (FourBitNumber)(channel - 1) };

    private static ProgramChangeEvent Program(int program, int channel) =>
        new((SevenBitNumber)program) { Channel = (FourBitNumber)(channel - 1) };

    private static FakePortProvider T8Sending(params MidiEvent[] events)
    {
        var ports = FakePortProvider.WithT8();
        ports.InputEvents.AddRange(events);
        return ports;
    }

    [Fact]
    public async Task Finds_bass_rhythm_and_program_change_channels()
    {
        var ports = T8Sending(Note(36, 5), Note(42, 5), Note(48, 6), Note(51, 6), Program(2, 7), new TimingClockEvent());

        var result = await new ChannelDetector(ports).RunAsync(T8, TimeSpan.FromSeconds(5));

        Assert.Equal((6, 5, 7), (result.BassChannel, result.RhythmChannel, result.ProgramChangeChannel));
        Assert.Equal("Detected bass 6, rhythm 5, pattern select 7.", result.Summary);
        Assert.True(ports.OpenedInputs.Single().IsDisposed);
    }

    [Fact]
    public void Bass_notes_on_drum_note_numbers_stay_bass()
    {
        // C2 (36) is both a bass note and the kick's note number.
        var result = ChannelDetector.Analyze([Note(36, 2), Note(36, 2), Note(43, 2), Note(36, 10), Note(38, 10)], new T8MidiMap(), "T-8");

        Assert.Equal(2, result.BassChannel);
        Assert.Equal(10, result.RhythmChannel);
    }

    [Fact]
    public void Ignores_note_offs_sent_as_zero_velocity_note_ons()
    {
        var result = ChannelDetector.Analyze([Note(48, 3, velocity: 0)], new T8MidiMap(), "T-8");
        Assert.Null(result.BassChannel);
    }

    [Fact]
    public void Uses_the_configured_drum_notes()
    {
        var map = new T8MidiMap();
        map.RhythmNotes[Core.Patterns.RhythmTrack.BD] = 60;

        var result = ChannelDetector.Analyze([Note(60, 4)], map, "T-8");

        Assert.Equal(4, result.RhythmChannel);
        Assert.Null(result.BassChannel);
    }

    [Fact]
    public async Task Stops_soon_after_bass_and_rhythm_are_heard()
    {
        var ports = T8Sending(Note(36, 5), Note(51, 6));
        var detector = new ChannelDetector(ports) { ExtraTime = TimeSpan.FromMilliseconds(100) };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await detector.RunAsync(T8, TimeSpan.FromSeconds(30));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Equal((6, 5), (result.BassChannel, result.RhythmChannel));
        Assert.Null(result.ProgramChangeChannel);
    }

    [Fact]
    public void Reads_a_real_t8_capture()
    {
        // Captured from a T-8 set to rhCh 5 and bACh 6: bass plays 36 and 46, which are also drum notes.
        MidiEvent[] capture =
        [
            Note(36, 5), Note(46, 6), Note(36, 5, velocity: 0), Note(36, 6), Note(47, 5), Note(42, 5),
            Note(39, 6), Note(38, 5), Note(48, 6), Note(43, 6), Note(55, 6),
        ];

        var result = ChannelDetector.Analyze(capture, new T8MidiMap(), "T-8 MIDI OUT");

        Assert.Equal((6, 5), (result.BassChannel, result.RhythmChannel));
    }

    [Fact]
    public async Task Reports_what_was_not_seen()
    {
        var ports = T8Sending(Note(38, 9));

        var result = await new ChannelDetector(ports).RunAsync(T8, TimeSpan.FromMilliseconds(100));

        Assert.Equal(9, result.RhythmChannel);
        Assert.Contains("Not seen: bass", result.Summary);
        Assert.Contains("set it to the T-8's Pc.Ch by hand", result.Summary);
        var map = result.ApplyTo(new T8MidiMap());
        Assert.Equal((2, 9, 16), (map.BassChannel, map.RhythmChannel, map.ProgramChangeChannel));
    }

    [Fact]
    public async Task Explains_when_the_t8_only_sends_clock()
    {
        var result = await new ChannelDetector(FakePortProvider.WithT8()).RunAsync(T8, TimeSpan.FromMilliseconds(100));

        Assert.False(result.FoundAny);
        Assert.Contains("Press Start on the T-8", result.Summary);
    }

    [Fact]
    public async Task Explains_a_silent_or_missing_input()
    {
        var silent = FakePortProvider.WithT8();
        silent.InputsRespond = false;
        var quiet = await new ChannelDetector(silent).RunAsync(T8, TimeSpan.FromMilliseconds(100));
        Assert.Contains("Nothing came from T-8", quiet.Summary);

        var noInput = FakePortProvider.WithT8();
        noInput.Inputs = [new MidiPortInfo("Other", "Other")];
        var missing = await new ChannelDetector(noInput).RunAsync(T8, TimeSpan.FromMilliseconds(100));
        Assert.Null(missing.InputPortName);
        Assert.Contains("No T-8 MIDI input found", missing.Summary);
    }
}
