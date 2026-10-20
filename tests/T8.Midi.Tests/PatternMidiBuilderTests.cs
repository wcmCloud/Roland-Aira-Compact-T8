using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using T8.Core.Patterns;
using T8.Midi;

using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class PatternMidiBuilderTests
{
    private const int Step = PatternMidiBuilder.TicksPerStep;

    private static List<(long Time, NoteOnEvent Note)> NoteOns(IEnumerable<TimedEvent> events) =>
        events.Where(e => e.Event is NoteOnEvent).Select(e => (e.Time, (NoteOnEvent)e.Event)).ToList();

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 16, 15)]
    [InlineData(4, 16, 63)]
    public void Program_numbers_cover_four_banks(int bank, int pattern, int expected) =>
        Assert.Equal(expected, T8MidiMap.ProgramNumber(bank, pattern));

    [Fact]
    public void Loop_length_lines_up_bass_and_rhythm() =>
        Assert.Equal(96, PatternMidiBuilder.LoopSteps(new BassPattern { Length = 12 }, new RhythmPattern { Length = 32 }));

    [Fact]
    public void Rhythm_steps_map_to_t8_notes_and_channel()
    {
        var rhythm = new RhythmPattern { Length = 4 };
        rhythm.Steps[0][RhythmTrack.BD].On = true;
        rhythm.Steps[2][RhythmTrack.CH].On = true;
        rhythm.Steps[2][RhythmTrack.AC].On = true;

        var notes = NoteOns(new PatternMidiBuilder { Seed = 1 }.Build(null, rhythm));

        Assert.Equal(2, notes.Count);
        Assert.Equal((0L, 36), (notes[0].Time, (int)notes[0].Note.NoteNumber));
        Assert.Equal((2L * Step, 42), (notes[1].Time, (int)notes[1].Note.NoteNumber));
        Assert.All(notes, n => Assert.Equal(9, (int)n.Note.Channel)); // channel 10
        Assert.True(notes[1].Note.Velocity > notes[0].Note.Velocity); // accented
    }

    [Fact]
    public void Sub_steps_split_the_step()
    {
        var rhythm = new RhythmPattern { Length = 1 };
        rhythm.Steps[0][RhythmTrack.SD].On = true;
        rhythm.Steps[0][RhythmTrack.SD].SubStep = SubStep.Quarter;

        var times = NoteOns(new PatternMidiBuilder { Seed = 1 }.Build(null, rhythm)).Select(n => n.Time);

        Assert.Equal(new long[] { 0, Step / 4, Step / 2, 3 * Step / 4 }, times);
    }

    [Fact]
    public void Zero_probability_never_plays()
    {
        var rhythm = new RhythmPattern { Length = 16 };
        foreach (var step in rhythm.Steps)
        {
            step[RhythmTrack.BD].On = true;
            step[RhythmTrack.BD].Probability = 0;
        }

        Assert.Empty(NoteOns(new PatternMidiBuilder().Build(null, rhythm)));
    }

    [Fact]
    public void Bass_tie_extends_note_and_slide_overlaps_next()
    {
        var bass = new BassPattern { Length = 4 };
        bass.Steps[0].State = BassStepState.Note;
        bass.Steps[0].Note = 36;
        bass.Steps[1].State = BassStepState.Tie;
        bass.Steps[1].Slide = true;
        bass.Steps[2].State = BassStepState.Note;
        bass.Steps[2].Note = 48;
        bass.Steps[2].Accent = true;

        var notes = new PatternMidiBuilder().Build(bass, null).ToTrackChunk().GetNotes().ToList();

        Assert.Equal(2, notes.Count);
        Assert.Equal(0, notes[0].Time);
        Assert.True(notes[0].EndTime > notes[1].Time, "slide should overlap the next note");
        Assert.Equal(2 * Step, notes[1].Time);
        Assert.Equal(127, (int)notes[1].Velocity);
        Assert.Equal(1, (int)notes[1].Channel); // channel 2
    }

    [Fact]
    public void Factory_patterns_export_to_midi_files()
    {
        var root = TestPaths.Backup;
        var library = PatternLibrary.Load(root);
        var file = new PatternMidiBuilder { Seed = 1 }.BuildMidiFile(library.Bass[0, 0], library.Rhythm[0, 0], 120);

        using var stream = new MemoryStream();
        file.Write(stream);
        Assert.True(stream.Length > 100);
        Assert.NotEmpty(file.GetNotes());
    }

    [Fact]
    public void Flam_adds_a_softer_grace_note_before_the_hit()
    {
        var rhythm = new RhythmPattern { Length = 1, Flam = 36 };
        rhythm.Steps[0][RhythmTrack.SD].On = true;
        rhythm.Steps[0][RhythmTrack.SD].Velocity = 10;
        rhythm.Steps[0][RhythmTrack.SD].SubStep = SubStep.Flam;

        var notes = NoteOns(new PatternMidiBuilder().Build(null, rhythm));

        Assert.Equal(2, notes.Count);
        Assert.Equal(0, notes[0].Time);
        Assert.True(notes[1].Time > 0);
        Assert.True(notes[0].Note.Velocity < notes[1].Note.Velocity);
    }

    [Fact]
    public void Shuffle_delays_every_second_step()
    {
        var rhythm = new RhythmPattern { Length = 2, Shuffle = 200 };
        rhythm.Steps[0][RhythmTrack.CH].On = true;
        rhythm.Steps[1][RhythmTrack.CH].On = true;

        var times = NoteOns(new PatternMidiBuilder().Build(null, rhythm)).Select(n => n.Time).ToList();

        Assert.Equal(0, times[0]);
        Assert.True(times[1] > Step, "second 16th should be late");
    }

    [Fact]
    public void Zero_sub_step_probability_keeps_only_the_first_hit()
    {
        var rhythm = new RhythmPattern { Length = 1 };
        var cell = rhythm.Steps[0][RhythmTrack.BD];
        cell.On = true;
        cell.SubStep = SubStep.Third;
        cell.SubStepProbability = 0;

        Assert.Single(NoteOns(new PatternMidiBuilder().Build(null, rhythm)));
    }

    [Fact]
    public void Cymbal_track_is_not_sent()
    {
        var rhythm = new RhythmPattern { Length = 1 };
        rhythm.Steps[0][RhythmTrack.CY].On = true;
        rhythm.Steps[0][RhythmTrack.AC].On = true;

        Assert.Empty(NoteOns(new PatternMidiBuilder().Build(null, rhythm)));
    }

    [Theory]
    [InlineData(0, false, 10)]
    [InlineData(7, false, 73)]
    [InlineData(10, false, 100)]
    [InlineData(10, true, 127)]
    public void Velocity_curve(int t8Velocity, bool accent, int expected) =>
        Assert.Equal(expected, PatternMidiBuilder.Velocity(t8Velocity, accent));

    [Fact]
    public void Same_seed_gives_same_probability_rolls()
    {
        var rhythm = new RhythmPattern { Length = 32 };
        foreach (var step in rhythm.Steps)
        {
            step[RhythmTrack.CH].On = true;
            step[RhythmTrack.CH].Probability = 5;
        }

        var a = NoteOns(new PatternMidiBuilder { Seed = 42 }.Build(null, rhythm)).Select(n => n.Time);
        var b = NoteOns(new PatternMidiBuilder { Seed = 42 }.Build(null, rhythm)).Select(n => n.Time);

        Assert.Equal(a, b);
        Assert.InRange(a.Count(), 1, 31);
    }

    [Fact]
    public void Bass_note_without_slide_uses_a_half_step_gate()
    {
        var bass = new BassPattern { Length = 2 };
        bass.Steps[0].State = BassStepState.Note;
        bass.Steps[1].State = BassStepState.Note;

        var notes = new PatternMidiBuilder().Build(bass, null).ToTrackChunk().GetNotes().ToList();

        Assert.Equal(2, notes.Count);
        Assert.Equal(Step / 2, notes[0].Length);
        Assert.Equal(90, (int)notes[0].Velocity);
    }

    [Fact]
    public void Bass_follows_custom_channel()
    {
        var bass = new BassPattern { Length = 1 };
        bass.Steps[0].State = BassStepState.Note;

        var builder = new PatternMidiBuilder { Map = new T8MidiMap { BassChannel = 5 } };
        Assert.All(NoteOns(builder.Build(bass, null)), n => Assert.Equal(4, (int)n.Note.Channel));
    }

    [Fact]
    public void Empty_input_builds_nothing()
    {
        Assert.Empty(new PatternMidiBuilder().Build(null, null));
        Assert.Equal(0, new PatternMidiBuilder().LengthInTicks(null, null));
    }

    [Fact]
    public void Exported_file_carries_tempo()
    {
        var file = new PatternMidiBuilder().BuildMidiFile(new BassPattern(), new RhythmPattern(), 133);
        var tempo = file.GetTempoMap().GetTempoAtTime(new MidiTimeSpan(0));
        Assert.Equal(133, Math.Round(tempo.BeatsPerMinute));
    }
}
