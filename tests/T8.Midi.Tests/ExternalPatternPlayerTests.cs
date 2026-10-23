using Melanchall.DryWetMidi.Core;
using T8.Core.Patterns;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class ExternalPatternPlayerTests
{
    private static RhythmPattern Kick(int length = 4)
    {
        var rhythm = new RhythmPattern { Length = length };
        rhythm.Steps[0][RhythmTrack.BD].On = true;
        rhythm.Steps[0][RhythmTrack.BD].Velocity = 9;
        return rhythm;
    }

    [Fact]
    public void Notes_mode_plays_on_the_external_clock_and_does_not_forward_it()
    {
        using var output = new FakeOutputDevice();
        using var t8 = new T8Controller(output);
        using var input = new FakeInputDevice();
        using var player = new ExternalPatternPlayer(input, t8, null, Kick(), null, 1, 1, programs: false);
        input.StartEventsListening();

        input.Receive(new TimingClockEvent());
        Assert.Empty(output.Events);
        input.Receive(new StartEvent());
        Assert.Contains(output.Events, e => e is NoteOnEvent n && n.NoteNumber == 36);
        Assert.DoesNotContain(output.Events, e => e is TimingClockEvent or StartEvent);
        var notes = output.Events.OfType<NoteOnEvent>().Count();

        for (var i = 0; i < 6; i++)
            input.Receive(new TimingClockEvent());
        Assert.Equal(0, player.CurrentStep);
        input.Receive(new StopEvent());
        input.Receive(new TimingClockEvent());
        Assert.Null(player.CurrentStep);
        Assert.Equal(notes, output.Events.OfType<NoteOnEvent>().Count());
        input.Receive(new ContinueEvent());
        Assert.True(player.IsRunning);
    }

    [Fact]
    public void Sequencer_mode_forwards_transport_and_clock_without_notes()
    {
        using var output = new FakeOutputDevice();
        using var t8 = new T8Controller(output);
        using var input = new FakeInputDevice();
        using var player = new ExternalPatternPlayer(input, t8, null, Kick(), null, 2, 3, programs: true);
        input.StartEventsListening();

        input.Receive(new StartEvent());
        input.Receive(new TimingClockEvent());
        input.Receive(new StopEvent());

        Assert.Equal(18, output.Events.OfType<ProgramChangeEvent>().Single().ProgramNumber);
        Assert.Contains(output.Events, e => e is StartEvent);
        Assert.Single(output.Events.OfType<TimingClockEvent>());
        Assert.Contains(output.Events, e => e is StopEvent);
        Assert.DoesNotContain(output.Events, e => e is NoteOnEvent);
        Assert.False(player.IsRunning);
    }

    [Fact]
    public void A_quiet_clock_is_silenced_and_stays_armed_for_the_next_start()
    {
        var now = 0L;
        using var output = new FakeOutputDevice();
        using var t8 = new T8Controller(output);
        using var input = new FakeInputDevice();
        using var player = new ExternalPatternPlayer(input, t8, null, Kick(), null, 1, 1, false, () => now);
        input.StartEventsListening();
        input.Receive(new StartEvent());
        output.Clear();

        Assert.False(player.SilenceIfQuiet(1000));
        now = 1000;
        Assert.True(player.SilenceIfQuiet(1000));
        Assert.False(player.IsRunning);
        Assert.Contains(output.Events, e => e is ControlChangeEvent);
        input.Receive(new TimingClockEvent());
        Assert.DoesNotContain(output.Events, e => e is NoteOnEvent);
    }

    [Fact]
    public void A_queued_fill_plays_once_when_the_loop_ends()
    {
        var fill = new RhythmPattern { Length = 4 };
        fill.Steps[0][RhythmTrack.SD].On = true;
        fill.Steps[0][RhythmTrack.SD].Velocity = 9;
        using var output = new FakeOutputDevice();
        using var t8 = new T8Controller(output);
        using var input = new FakeInputDevice();
        using var player = new ExternalPatternPlayer(input, t8, null, Kick(), fill, 1, 1, false);
        input.StartEventsListening();
        input.Receive(new StartEvent());
        Assert.True(player.QueueFill());

        for (var i = 0; i < 24; i++)
            input.Receive(new TimingClockEvent());
        Assert.DoesNotContain(output.Events, e => e is NoteOnEvent n && n.NoteNumber == 38);
        input.Receive(new TimingClockEvent());
        Assert.Contains(output.Events, e => e is NoteOnEvent n && n.NoteNumber == 38);
        Assert.Null(player.CurrentStep);
    }

    [Fact]
    public void The_loop_restarts_the_downbeat_without_choking_sounding_drums()
    {
        using var output = new FakeOutputDevice();
        using var t8 = new T8Controller(output);
        using var input = new FakeInputDevice();
        using var player = new ExternalPatternPlayer(input, t8, null, Kick(), null, 1, 1, false);
        input.StartEventsListening();
        input.Receive(new StartEvent());
        output.Clear();

        for (var i = 0; i < 25; i++)
            input.Receive(new TimingClockEvent());

        Assert.Contains(output.Events, e => e is NoteOnEvent n && n.NoteNumber == 36);
        Assert.DoesNotContain(output.Events, e => e is ControlChangeEvent cc && (int)cc.ControlNumber is 120 or 123);
    }
}
