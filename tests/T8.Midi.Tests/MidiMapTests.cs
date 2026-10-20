using System.Text.Json;
using T8.Core.Patterns;

namespace T8.Midi.Tests;

public class MidiMapTests
{
    [Fact]
    public void Defaults_match_the_t8_midi_chart()
    {
        var map = new T8MidiMap();
        Assert.Equal((2, 10, 16), (map.BassChannel, map.RhythmChannel, map.ProgramChangeChannel));
        Assert.Equal(36, map.RhythmNotes[RhythmTrack.BD]);
        Assert.Empty(map.Validate());
        Assert.Equal(6, T8MidiMap.PlayableTracks.Count);
    }

    [Theory]
    [InlineData(-1, 10, 16, "Bass channel must be 1–16 or OFF.")]
    [InlineData(2, 17, 16, "Rhythm channel must be 1–16 or OFF.")]
    [InlineData(2, 10, 0, "Program change channel must be 1–16.")]
    [InlineData(5, 5, 16, "Bass and rhythm need different channels.")]
    public void Validate_reports_bad_channels(int bass, int rhythm, int pc, string error)
    {
        var map = new T8MidiMap { BassChannel = bass, RhythmChannel = rhythm, ProgramChangeChannel = pc };
        Assert.Contains(error, map.Validate());
    }

    [Theory]
    [InlineData(T8MidiMap.Off, 10)]
    [InlineData(2, T8MidiMap.Off)]
    [InlineData(T8MidiMap.Off, T8MidiMap.Off)] // both off: not "the same channel"
    public void Bass_and_rhythm_can_be_off(int bass, int rhythm)
    {
        var map = new T8MidiMap { BassChannel = bass, RhythmChannel = rhythm };
        Assert.Empty(map.Validate());
        Assert.Equal(bass != 0, map.BassOn);
        Assert.Equal(rhythm != 0, map.RhythmOn);
        Assert.Equal("OFF", T8MidiMap.ChannelName(T8MidiMap.Off));
        Assert.Equal("7", T8MidiMap.ChannelName(7));
    }

    [Fact]
    public void Program_change_channel_cannot_be_off() =>
        Assert.Contains("Program change channel must be 1–16.", new T8MidiMap { ProgramChangeChannel = T8MidiMap.Off }.Validate());

    [Fact]
    public void Validate_reports_bad_notes()
    {
        var map = new T8MidiMap();
        map.RhythmNotes[RhythmTrack.SD] = 128;
        Assert.Contains("SD note must be 0–127.", map.Validate());
    }

    [Fact]
    public void Clone_is_independent()
    {
        var map = new T8MidiMap { BassChannel = 4 };
        var copy = map.Clone();
        copy.BassChannel = 5;
        copy.RhythmNotes[RhythmTrack.BD] = 35;

        Assert.Equal(4, map.BassChannel);
        Assert.Equal(36, map.RhythmNotes[RhythmTrack.BD]);
        Assert.Equal(35, copy.RhythmNotes[RhythmTrack.BD]);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var map = new T8MidiMap { BassChannel = 3, RhythmChannel = 9, ProgramChangeChannel = 1 };
        map.RhythmNotes[RhythmTrack.SD] = 40;

        var back = JsonSerializer.Deserialize<T8MidiMap>(JsonSerializer.Serialize(map))!;

        Assert.Equal((3, 9, 1), (back.BassChannel, back.RhythmChannel, back.ProgramChangeChannel));
        Assert.Equal(40, back.RhythmNotes[RhythmTrack.SD]);
        Assert.Equal(6, back.RhythmNotes.Count);
    }

    [Fact]
    public void Controller_and_builder_use_custom_drum_notes()
    {
        var map = new T8MidiMap();
        map.RhythmNotes[RhythmTrack.BD] = 35;
        var rhythm = new RhythmPattern { Length = 1 };
        rhythm.Steps[0][RhythmTrack.BD].On = true;

        var notes = new PatternMidiBuilder { Map = map }.Build(null, rhythm)
            .Select(e => e.Event).OfType<Melanchall.DryWetMidi.Core.NoteOnEvent>();

        Assert.Equal(35, (int)Assert.Single(notes).NoteNumber);
    }
}
