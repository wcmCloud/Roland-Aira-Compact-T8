using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using T8.Core.Patterns;
using T8.Core.Songs;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class SongMidiExportTests
{
    private static (Song, PatternLibrary) Arrangement()
    {
        var library = PatternLibrary.CreateBlank();
        for (var p = 0; p < 16; p++)
        {
            library.Rhythm[0, p].Length = 4;
            library.Rhythm[0, p].Steps[0][RhythmTrack.BD].On = true;
        }
        return (new Song { Entries = [
            new SongEntry { Bank = 1, Pattern = 1, Repeats = 2, Fill = new SongFill(1, 16) },
            new SongEntry { Bank = 1, Pattern = 2, Repeats = 1 } ] }, library);
    }

    [Fact]
    public void Programs_are_queued_one_pass_ahead_with_fills_and_no_redundant_repeats()
    {
        var (song, library) = Arrangement();
        var file = new SongMidiExporter().Build(song, library, 123, SongMidiMode.Programs);
        var programs = file.GetTimedEvents().Where(e => e.Event is ProgramChangeEvent).ToList();
        Assert.Equal([0L, 120L, 216L], programs.Select(e => e.Time));
        Assert.Equal([0, 15, 1], programs.Select(e => (int)((ProgramChangeEvent)e.Event).ProgramNumber));
        Assert.All(programs, e => Assert.Equal(15, (int)((ProgramChangeEvent)e.Event).Channel));
        Assert.Contains(file.GetTimedEvents(), e => e.Event is SetTempoEvent);
        Assert.Equal(384, file.GetTimedEvents().Max(e => e.Time));
    }

    [Fact]
    public void Notes_include_repeats_and_fill_and_respect_off_channels()
    {
        var (song, library) = Arrangement();
        var exporter = new SongMidiExporter { Seed = 1 };
        Assert.Equal([0L, 96L, 192L, 288L], exporter.Build(song, library, 120, SongMidiMode.Notes)
            .GetTimedEvents().Where(e => e.Event is NoteOnEvent).Select(e => e.Time));
        exporter.Map.RhythmChannel = T8MidiMap.Off;
        exporter.Map.BassChannel = T8MidiMap.Off;
        var file = exporter.Build(song, library, 120, SongMidiMode.Notes);
        Assert.DoesNotContain(file.GetTimedEvents(), e => e.Event is NoteOnEvent);
        Assert.Equal(384, file.GetTimedEvents().Max(e => e.Time));
    }

    [Fact]
    public void External_transport_waits_for_start_stops_resumes_and_resets()
    {
        var (song, library) = Arrangement();
        using var output = new FakeOutputDevice();
        using var t8 = new T8Controller(output);
        var input = new FakeInputDevice();
        using var player = new ExternalSongPlayer(input, t8, song, library, false, false);
        input.StartEventsListening();
        input.Receive(new TimingClockEvent());
        Assert.Empty(output.Events);
        input.Receive(new StartEvent());
        Assert.Single(output.Events.OfType<NoteOnEvent>());
        for (var i = 0; i < 7; i++) input.Receive(new TimingClockEvent());
        Assert.Equal(1, player.CurrentStep);
        input.Receive(new StopEvent());
        input.Receive(new TimingClockEvent());
        Assert.Equal(1, player.CurrentStep);
        input.Receive(new ContinueEvent());
        Assert.True(player.IsRunning);
        input.Receive(new StartEvent());
        Assert.Equal(0, player.CurrentStep);
        Assert.Equal(2, output.Events.OfType<NoteOnEvent>().Count());
        Assert.DoesNotContain(output.Events, e => e is TimingClockEvent);
    }

    [Fact]
    public void External_program_mode_forwards_clock_and_queues_the_loop_start_in_advance()
    {
        var (song, library) = Arrangement();
        using var output = new FakeOutputDevice();
        using var t8 = new T8Controller(output);
        using var player = new ExternalSongPlayer(new FakeInputDevice(), t8, song, library, true, true);
        player.Receive(new StartEvent());
        for (var i = 0; i < 16 * 6; i++) player.Receive(new TimingClockEvent());
        Assert.Equal([0, 15, 1, 0], output.Events.OfType<ProgramChangeEvent>().Select(e => (int)e.ProgramNumber));
        Assert.Equal(96, output.Events.OfType<TimingClockEvent>().Count());
    }
}
