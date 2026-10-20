using Melanchall.DryWetMidi.Core;
using T8.Core.Patterns;
using T8.Core.Songs;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class SongPlaybackTests
{
    private readonly FakeOutputDevice _device = new();

    /// <summary>Bank 1 patterns 1 and 2, two steps long, each with a kick on step 1.</summary>
    private static (Song Song, PatternLibrary Library) TwoPatterns(bool fill = false)
    {
        var library = PatternLibrary.CreateBlank();
        foreach (var p in new[] { 0, 1, 15 })
        {
            var rhythm = library.Rhythm[0, p];
            rhythm.Length = 2;
            rhythm.Steps[0][p == 15 ? RhythmTrack.SD : RhythmTrack.BD].On = true;
            rhythm.Steps[0][p == 15 ? RhythmTrack.SD : RhythmTrack.BD].Velocity = 9;
        }
        var song = new Song
        {
            Entries =
            [
                new SongEntry { Bank = 1, Pattern = 1, Repeats = 2, Fill = fill ? new SongFill(1, 16) : null },
                new SongEntry { Bank = 1, Pattern = 2, Repeats = 1 },
            ],
        };
        return (song, library);
    }

    [Fact]
    public void Preview_plays_the_song_once_and_finishes()
    {
        var (song, library) = TwoPatterns(fill: true);
        var plan = SongPlan.Create(song, library);
        using var t8 = new T8Controller(_device);

        t8.PlaySong(plan, library, bpm: 300, loop: false); // a step is 50 ms; 4 passes of 2 steps
        Assert.False(t8.PreviewFinished);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!t8.PreviewFinished && DateTime.UtcNow < deadline)
            Thread.Sleep(20);

        Assert.True(t8.PreviewFinished);
        var notes = _device.Events.OfType<NoteOnEvent>().Select(n => (int)n.NoteNumber).ToList();
        Assert.Equal([36, 36, 38, 36], notes); // pattern 1 twice, the fill's snare, pattern 2
    }

    [Fact]
    public void Preview_can_start_from_a_later_pass()
    {
        var (song, library) = TwoPatterns();
        var plan = SongPlan.Create(song, library);
        using var t8 = new T8Controller(_device);

        // Looping, so a slow machine can't reach the end before the position is read.
        t8.PlaySong(plan, library, bpm: 60, loop: true, fromPass: 2);

        Assert.InRange(t8.CurrentStep!.Value, 4, 5); // pass 3 starts at step 4
        t8.StopPreview();
    }

    [Fact]
    public void Sync_driver_selects_the_first_pattern_then_queues_each_change_one_pass_ahead()
    {
        var (song, library) = TwoPatterns(fill: true);
        var plan = SongPlan.Create(song, library);
        using var t8 = new T8Controller(_device);
        using var driver = new SongSyncDriver(t8, plan, loop: false);

        driver.Start(bpm: 300);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!driver.Finished && DateTime.UtcNow < deadline)
            Thread.Sleep(20);

        Assert.True(driver.Finished);
        var events = _device.Events.Where(e => e is ProgramChangeEvent or StartEvent or StopEvent).ToList();
        // Pattern 1 before Start; the fill (16, program 15) queued during pattern 1's second pass;
        // pattern 2 (program 1) queued during the fill; Stop at the end.
        Assert.IsType<ProgramChangeEvent>(events[0]);
        Assert.Equal(0, (int)((ProgramChangeEvent)events[0]).ProgramNumber);
        Assert.IsType<StartEvent>(events[1]);
        Assert.Equal([15, 1], events.OfType<ProgramChangeEvent>().Skip(1).Select(p => (int)p.ProgramNumber));
        Assert.IsType<StopEvent>(events[^1]);
    }

    [Fact]
    public void Sync_driver_loops_back_to_the_first_pattern()
    {
        var (song, library) = TwoPatterns();
        var plan = SongPlan.Create(song, library);
        using var t8 = new T8Controller(_device);
        using var driver = new SongSyncDriver(t8, plan, loop: true);

        driver.Start(bpm: 300);
        Thread.Sleep(450); // past the 300 ms song
        driver.Stop();

        Assert.False(driver.Finished);
        var programs = _device.Events.OfType<ProgramChangeEvent>().Select(p => (int)p.ProgramNumber).ToList();
        Assert.Equal([0, 1, 0], programs.Take(3));
    }

    /// <summary>Bank 1 patterns 1, 2 and 3, each 2 steps long with one drum on step 1: BD, SD, CH.</summary>
    private static (Song Song, PatternLibrary Library) ThreeParts()
    {
        var library = PatternLibrary.CreateBlank();
        var drums = new[] { RhythmTrack.BD, RhythmTrack.SD, RhythmTrack.CH };
        for (var p = 0; p < 3; p++)
        {
            library.Rhythm[0, p].Length = 2;
            library.Rhythm[0, p].Steps[0][drums[p]].On = true;
            library.Rhythm[0, p].Steps[0][drums[p]].Velocity = 9;
        }
        var song = new Song
        {
            Entries = [new SongEntry { Bank = 1, Pattern = 1 }, new SongEntry { Bank = 1, Pattern = 2 }, new SongEntry { Bank = 1, Pattern = 3 }],
        };
        return (song, library);
    }

    [Fact]
    public void Preview_jumps_to_a_queued_part_when_the_current_pass_ends()
    {
        var (song, library) = ThreeParts();
        var plan = SongPlan.Create(song, library);
        using var t8 = new T8Controller(_device);

        t8.PlaySong(plan, library, bpm: 60, loop: false); // a step is 250 ms
        t8.QueueSongJump(atStep: 2, toStep: 4);           // after part 1, go to part 3
        Assert.True(t8.IsSongJumpPending);
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (!t8.PreviewFinished && DateTime.UtcNow < deadline)
            Thread.Sleep(20);

        Assert.False(t8.IsSongJumpPending);
        Assert.Equal([36, 42], _device.Events.OfType<NoteOnEvent>().Select(n => (int)n.NoteNumber)); // BD, then CH; SD skipped
    }

    [Fact]
    public void Sync_driver_queues_a_chosen_pass_in_place_of_the_next_one()
    {
        var (song, library) = ThreeParts();
        var plan = SongPlan.Create(song, library);
        using var t8 = new T8Controller(_device);
        using var driver = new SongSyncDriver(t8, plan, loop: false);

        driver.Start(bpm: 300);
        driver.QueuePass(2);
        Assert.True(driver.IsJumpPending);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!driver.Finished && DateTime.UtcNow < deadline)
            Thread.Sleep(20);

        Assert.True(driver.Finished);
        Assert.False(driver.IsJumpPending);
        // Pattern 1 to start; pattern 3 queued at once (replacing pattern 2); then the song ends after pattern 3.
        var programs = _device.Events.OfType<ProgramChangeEvent>().Select(p => (int)p.ProgramNumber).ToList();
        Assert.Equal(0, programs[0]);
        Assert.Equal(2, programs[^1]);
        Assert.DoesNotContain(1, programs.Skip(programs.IndexOf(2)));
    }
}
