using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using T8.Core.Patterns;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class ControllerTests
{
    private readonly FakeOutputDevice _device = new();

    private T8Controller CreateController() => new(_device);

    [Fact]
    public void Select_pattern_sends_program_change_on_channel_16()
    {
        using var t8 = CreateController();
        t8.SelectPattern(3, 7);

        var pc = Assert.IsType<ProgramChangeEvent>(Assert.Single(_device.Events));
        Assert.Equal(15, (int)pc.Channel);
        Assert.Equal(38, (int)pc.ProgramNumber);
    }

    [Fact]
    public void Select_pattern_uses_custom_channel()
    {
        using var t8 = new T8Controller(_device, new T8MidiMap { ProgramChangeChannel = 1 });
        t8.SelectPattern(1, 1);
        Assert.Equal(0, (int)((ProgramChangeEvent)_device.Events[0]).Channel);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 17)]
    public void Select_pattern_rejects_out_of_range(int bank, int pattern)
    {
        using var t8 = CreateController();
        Assert.Throws<ArgumentOutOfRangeException>(() => t8.SelectPattern(bank, pattern));
    }

    [Fact]
    public void Trigger_drum_sends_note_on_and_off_on_rhythm_channel()
    {
        using var t8 = CreateController();
        t8.TriggerDrum(RhythmTrack.SD, 90);

        var on = Assert.IsType<NoteOnEvent>(_device.Events[0]);
        var off = Assert.IsType<NoteOffEvent>(_device.Events[1]);
        Assert.Equal((38, 90, 9), ((int)on.NoteNumber, (int)on.Velocity, (int)on.Channel));
        Assert.Equal(38, (int)off.NoteNumber);
    }

    [Theory]
    [InlineData(RhythmTrack.AC)]
    [InlineData(RhythmTrack.CY)]
    public void Trigger_drum_rejects_tracks_without_a_sound(RhythmTrack track)
    {
        using var t8 = CreateController();
        Assert.Throws<ArgumentException>(() => t8.TriggerDrum(track));
    }

    [Fact]
    public void Bass_notes_go_to_channel_2()
    {
        using var t8 = CreateController();
        t8.BassNoteOn(48, 127);
        t8.BassNoteOff(48);

        var on = Assert.IsType<NoteOnEvent>(_device.Events[0]);
        Assert.Equal((48, 127, 1), ((int)on.NoteNumber, (int)on.Velocity, (int)on.Channel));
        Assert.IsType<NoteOffEvent>(_device.Events[1]);
    }

    [Fact]
    public void All_notes_off_is_sent_to_bass_and_rhythm()
    {
        using var t8 = CreateController();
        t8.AllNotesOff();

        var channels = _device.Events.Cast<ControlChangeEvent>().Select(e => ((int)e.ControlNumber, (int)e.Channel));
        Assert.Equal([(120, 1), (123, 1), (120, 9), (123, 9)], channels);
    }

    [Fact]
    public void Preview_plays_pattern_and_stops()
    {
        var rhythm = new RhythmPattern { Length = 16 };
        foreach (var step in rhythm.Steps)
            step[RhythmTrack.CH].On = true;

        using var t8 = CreateController();
        t8.PlayPreview(null, rhythm, bpm: 300);
        Assert.True(t8.IsPreviewPlaying);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!_device.Events.OfType<NoteOnEvent>().Any() && DateTime.UtcNow < deadline)
            Thread.Sleep(10);

        t8.StopPreview();
        Assert.False(t8.IsPreviewPlaying);
        Assert.Contains(_device.Events, e => e is NoteOnEvent n && n.NoteNumber == 42);
        Assert.Contains(_device.Events, e => e is ControlChangeEvent c && c.ControlNumber == 123);
    }

    [Fact]
    public void Preview_loops_at_the_end_of_the_last_step_not_after_the_last_note()
    {
        var rhythm = new RhythmPattern { Length = 16 };
        rhythm.Steps[0][RhythmTrack.BD].On = true;

        using var t8 = CreateController();
        var duration = t8.PlayPreview(null, rhythm, bpm: 120).GetDuration<MidiTimeSpan>();
        t8.StopPreview();

        Assert.Equal(16 * PatternMidiBuilder.TicksPerStep, duration.TimeSpan);
        Assert.DoesNotContain(_device.Events, e => e is MarkerEvent);
    }

    [Fact]
    public void Current_step_follows_the_preview_and_the_clock()
    {
        var rhythm = new RhythmPattern { Length = 16 };
        using var t8 = CreateController();
        Assert.Null(t8.CurrentStep);

        t8.PlayPreview(null, rhythm, bpm: 120);
        Thread.Sleep(300); // 120 BPM: a step is 125 ms
        Assert.InRange(t8.CurrentStep!.Value, 1, 15);
        t8.StopPreview();
        Assert.Null(t8.CurrentStep);

        t8.Clock.Start();
        Thread.Sleep(100);
        Assert.NotNull(t8.CurrentStep);
        t8.Clock.Stop();
        Assert.Null(t8.CurrentStep);
    }

    [Fact]
    public void Queued_fill_plays_once_after_the_loop_then_the_main_pattern_returns()
    {
        var main = new RhythmPattern { Length = 4 };
        main.Steps[0][RhythmTrack.BD].On = true;
        main.Steps[0][RhythmTrack.BD].Velocity = 9;
        var fill = new RhythmPattern { Length = 4 };
        fill.Steps[0][RhythmTrack.SD].On = true;
        fill.Steps[0][RhythmTrack.SD].Velocity = 9;

        using var t8 = CreateController();
        var finished = new ManualResetEventSlim();
        t8.FillFinished += (_, _) => finished.Set();
        Assert.False(t8.QueueFill()); // nothing playing yet

        t8.PlayPreview(null, main, bpm: 300, fill: fill); // a step is 50 ms, a loop 200 ms
        Thread.Sleep(250);
        Assert.DoesNotContain(_device.Events, e => e is NoteOnEvent n && n.NoteNumber == 38);

        Assert.True(t8.QueueFill());
        Assert.True(t8.IsFillPending);
        Assert.True(finished.Wait(TimeSpan.FromSeconds(3)));
        Assert.False(t8.IsFillPending);
        var afterFill = _device.Events.Count;
        Thread.Sleep(300);
        t8.StopPreview();

        var notes = _device.Events.OfType<NoteOnEvent>().Select(n => (int)n.NoteNumber).ToList();
        Assert.Equal(1, notes.Count(n => n == 38));
        Assert.Contains(_device.Events.Skip(afterFill), e => e is NoteOnEvent n && n.NoteNumber == 36);
    }

    [Fact]
    public void Preview_without_a_fill_cannot_queue_one()
    {
        using var t8 = CreateController();
        t8.PlayPreview(null, new RhythmPattern(), bpm: 120);
        Assert.False(t8.QueueFill());
        t8.StopPreview();
    }

    [Fact]
    public void Preview_can_pause_and_resume()
    {
        using var t8 = CreateController();
        t8.PlayPreview(null, new RhythmPattern(), bpm: 120);

        t8.PausePreview();
        Assert.True(t8.IsPreviewPaused);
        Assert.Contains(_device.Events, e => e is ControlChangeEvent c && c.ControlNumber == 120);

        t8.ResumePreview();
        Assert.True(t8.IsPreviewPlaying);
        t8.StopPreview();
    }

    [Fact]
    public void Starting_a_new_preview_replaces_the_old_one()
    {
        using var t8 = CreateController();
        var first = t8.PlayPreview(new BassPattern(), null, 120);
        var second = t8.PlayPreview(new BassPattern(), null, 120);

        Assert.NotSame(first, second);
        Assert.False(first.IsRunning);
        t8.StopPreview();
    }

    [Fact]
    public void Stop_preview_without_preview_does_nothing()
    {
        using var t8 = CreateController();
        t8.StopPreview();
        Assert.Empty(_device.Events);
    }

    [Fact]
    public void Dispose_closes_the_port()
    {
        var t8 = CreateController();
        t8.Dispose();
        Assert.True(_device.IsDisposed);
    }

    [Fact]
    public void Connect_opens_the_t8_port()
    {
        var ports = FakePortProvider.WithT8();
        using var t8 = T8Controller.Connect(ports);

        Assert.NotNull(t8);
        Assert.Single(ports.Opened);
    }

    [Fact]
    public void Connect_returns_null_without_a_t8()
    {
        var ports = new FakePortProvider(new MidiPortInfo("IAC Bus 1", "IAC Bus 1"));
        Assert.Null(T8Controller.Connect(ports));
        Assert.Empty(ports.Opened);
    }
}
