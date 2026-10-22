using T8.Core.Patterns;

namespace T8.Core.Tests;

public class BassGeneratorTests
{
    private static BassPattern Generate(BassGeneratorOptions options, int length = 16)
    {
        var pattern = new BassPattern { Length = length };
        BassGenerator.Generate(pattern, options);
        return pattern;
    }

    public static TheoryData<Scale> AllScales() => new(Enum.GetValues<Scale>());

    [Theory]
    [MemberData(nameof(AllScales))]
    public void Every_note_is_in_the_scale_and_the_range(Scale scale)
    {
        var options = new BassGeneratorOptions { Root = 4, Scale = scale, LowestOctave = 1, Octaves = 2, NoteChance = 100, OctaveJumpChance = 50 };
        var allowed = BassGenerator.Intervals(scale).Select(i => (4 + i) % 12).ToHashSet();

        for (var seed = 1; seed <= 20; seed++)
        {
            options.Seed = seed;
            var notes = Generate(options, 32).Steps.Where(s => s.State == BassStepState.Note).Select(s => s.Note).ToList();
            Assert.NotEmpty(notes);
            Assert.All(notes, n => Assert.Contains(n % 12, allowed));
            Assert.All(notes, n => Assert.InRange(n, 24, 48)); // C1 … C3
        }
    }

    [Fact]
    public void Starts_on_the_root_in_the_lowest_octave()
    {
        var pattern = Generate(new BassGeneratorOptions { Root = 4, Scale = Scale.MinorPentatonic, LowestOctave = 1 });
        Assert.Equal(BassStepState.Note, pattern.Steps[0].State);
        Assert.Equal(28, pattern.Steps[0].Note); // E1
    }

    [Fact]
    public void Same_seed_gives_the_same_line_and_another_seed_a_different_one()
    {
        var options = new BassGeneratorOptions { Seed = 42 };
        var a = Generate(options);
        var b = Generate(options);
        Assert.Equal(Describe(a), Describe(b));

        options.Seed = 43;
        Assert.NotEqual(Describe(a), Describe(Generate(options)));
    }

    [Fact]
    public void Each_chord_starts_on_its_root()
    {
        // A natural minor, i – VI – III – VII, 4 steps per chord: A, F, C, G.
        var options = new BassGeneratorOptions
        {
            Root = 9, Scale = Scale.NaturalMinor, Progression = Progression.OneSixThreeSeven, StepsPerChord = 4, LowestOctave = 1,
        };
        var pattern = Generate(options);
        var roots = new[] { 0, 4, 8, 12 }.Select(i => pattern.Steps[i].Note % 12).ToList();
        Assert.Equal([9, 5, 0, 7], roots);
    }

    [Fact]
    public void Only_fills_the_length_and_turns_the_rest_off()
    {
        var pattern = new BassPattern { Length = 32 };
        foreach (var step in pattern.Steps)
        {
            step.State = BassStepState.Note;
            step.Slide = true;
        }

        BassGenerator.Generate(pattern, new BassGeneratorOptions(), length: 8);

        Assert.All(pattern.Steps.Skip(8), s => Assert.Equal(BassStepState.Off, s.State));
        Assert.All(pattern.Steps.Skip(8), s => Assert.False(s.Slide));
    }

    [Fact]
    public void Zero_note_chance_leaves_only_chord_starts()
    {
        var pattern = Generate(new BassGeneratorOptions { NoteChance = 0, TieChance = 0, StepsPerChord = 16 }, 32);
        var notes = pattern.Steps.Select((s, i) => (s, i)).Where(x => x.s.State == BassStepState.Note).Select(x => x.i);
        // Beats still get a 25% bonus, so only check the chord starts are always there.
        Assert.Contains(0, notes);
        Assert.Contains(16, notes);
    }

    [Fact]
    public void Ties_hold_the_previous_note()
    {
        var options = new BassGeneratorOptions { NoteChance = 30, TieChance = 100 };
        var pattern = Generate(options, 32);
        for (var i = 1; i < 32; i++)
        {
            if (pattern.Steps[i].State == BassStepState.Tie)
                Assert.NotEqual(BassStepState.Off, pattern.Steps[i - 1].State);
        }
        Assert.Contains(pattern.Steps, s => s.State == BassStepState.Tie);
    }

    [Theory]
    [InlineData(Scale.HarmonicMinor, 5, new[] { 11, 3, 6 })] // E harmonic minor, V = B major: B D# F#
    [InlineData(Scale.MinorPentatonic, 1, new[] { 4, 7, 9 })] // E minor pentatonic: E G A
    [InlineData(Scale.Major, 4, new[] { 9, 1, 4 })] // E major, IV = A major: A C# E
    public void Chords_are_stacked_from_the_scale(Scale scale, int degree, int[] expected)
    {
        var tones = BassGenerator.ChordTones(4, scale, degree);
        Assert.Equal(expected, tones);
    }

    [Fact]
    public void Detects_the_key_and_the_tightest_scale_that_holds_the_line()
    {
        // E, G and A, with E first and on the beat.
        var pentatonic = Line(40, 43, 45, 47, 40, 38, 43, 45);
        Assert.Equal(new BassGenerator.DetectedKey(4, Scale.MinorPentatonic), BassGenerator.DetectKey(pentatonic));

        // G, A and B, and the line sits on G: G major pentatonic.
        var major = Line(43, 45, 47, 38, 43, 45, 47, 40);
        Assert.Equal(new BassGenerator.DetectedKey(7, Scale.MajorPentatonic), BassGenerator.DetectKey(major));

        // Every note of E natural minor, each one repeated or on the beat. F# and C rule out the pentatonic.
        var minor = Line(40, 42, 42, 43, 43, 45, 45, 47, 47, 36, 36, 38, 38, 40);
        Assert.Equal(new BassGenerator.DetectedKey(4, Scale.NaturalMinor), BassGenerator.DetectKey(minor));

        // D# instead of D is the leading tone, so this is E harmonic minor rather than E major or the natural minor.
        var harmonic = Line(40, 42, 42, 43, 43, 45, 45, 47, 47, 36, 36, 39, 39, 40);
        Assert.Equal(new BassGenerator.DetectedKey(4, Scale.HarmonicMinor), BassGenerator.DetectKey(harmonic));

        // Bb, repeated, makes it the blues scale rather than the pentatonic.
        var blues = Line(40, 43, 45, 46, 40, 46, 38, 47);
        Assert.Equal(new BassGenerator.DetectedKey(4, Scale.Blues), BassGenerator.DetectKey(blues));

        // One F off the beat does not pull the line off E minor pentatonic.
        var passing = Line(40, 43, 41, 45, 40, 38, 43, 47);
        Assert.Equal(new BassGenerator.DetectedKey(4, Scale.MinorPentatonic), BassGenerator.DetectKey(passing));

        Assert.Null(BassGenerator.DetectKey(new BassPattern()));
    }

    [Fact]
    public void The_chord_at_a_step_follows_the_progression()
    {
        // E natural minor, i – VI – III – VII, four steps each. Step 5 (index 4) is VI: C E G.
        Assert.Equal([4, 7, 11], BassGenerator.ChordAt(4, Scale.NaturalMinor, Progression.OneSixThreeSeven, 4, 0));
        Assert.Equal([0, 4, 7], BassGenerator.ChordAt(4, Scale.NaturalMinor, Progression.OneSixThreeSeven, 4, 4));
        Assert.Equal([4, 7, 9], BassGenerator.ScaleClasses(4, Scale.MinorPentatonic).Take(3));
        Assert.Equal(BassGenerator.PitchClassMask([4, 7, 9]), BassGenerator.PitchClassMask(BassGenerator.ChordTones(4, Scale.MinorPentatonic, 1)));
    }

    [Fact]
    public void Options_clone_is_independent()
    {
        var options = new BassGeneratorOptions { Seed = 5 };
        var clone = options.Clone();
        clone.Seed = 6;
        Assert.Equal(5, options.Seed);
    }

    [Fact]
    public void Names_cover_every_scale_and_progression()
    {
        Assert.All(Enum.GetValues<Scale>(), s => Assert.False(string.IsNullOrEmpty(BassGenerator.DisplayName(s))));
        Assert.All(Enum.GetValues<Progression>(), p => Assert.NotEmpty(BassGenerator.Degrees(p)));
        Assert.Equal("Minor pentatonic", BassGenerator.DisplayName(Scale.MinorPentatonic));
    }

    private static BassPattern Line(params int[] notes)
    {
        var pattern = new BassPattern { Length = notes.Length };
        for (var i = 0; i < notes.Length; i++)
        {
            pattern.Steps[i].State = BassStepState.Note;
            pattern.Steps[i].Note = notes[i];
        }
        return pattern;
    }

    private static string Describe(BassPattern pattern) =>
        string.Join(",", pattern.Steps.Select(s => $"{s.State}{s.Note}{s.Accent}{s.Slide}"));
}

public class BassVariationTests
{
    private static BassPattern Line()
    {
        var pattern = new BassPattern { Length = 16 };
        // E1 on the beat (the root), G1 off the beat, held for a step, accented and sliding.
        pattern.Steps[0].State = BassStepState.Note;
        pattern.Steps[0].Note = 28;
        pattern.Steps[0].Accent = true;
        pattern.Steps[1].State = BassStepState.Note;
        pattern.Steps[1].Note = 31;
        pattern.Steps[1].Slide = true;
        pattern.Steps[2].State = BassStepState.Tie;
        pattern.Steps[2].Note = 31;
        pattern.Steps[4].State = BassStepState.Note;
        pattern.Steps[4].Note = 31; // on the beat, but not the root
        return pattern;
    }

    [Fact]
    public void Vary_keeps_the_root_on_the_beat_and_jumps_other_notes()
    {
        var pattern = Line();
        BassGenerator.Vary(pattern, new BassGeneratorOptions
        {
            Root = 4, Scale = Scale.MinorPentatonic, OctaveJumpChance = 100, LockRoots = true,
            LowestOctave = 1, Octaves = 2, Seed = 1,
        });

        Assert.Equal(28, pattern.Steps[0].Note);
        Assert.True(pattern.Steps[0].Accent);
        Assert.Equal(43, pattern.Steps[1].Note); // G1 → G2, the only other octave in range
        Assert.True(pattern.Steps[1].Slide);
        Assert.Equal(BassStepState.Tie, pattern.Steps[2].State);
        Assert.Equal(43, pattern.Steps[2].Note);
        Assert.Equal(43, pattern.Steps[4].Note);
        Assert.All(pattern.Steps.Where(s => s.State != BassStepState.Off), s => Assert.Contains(s.Note % 12, new[] { 4, 7 }));
    }

    [Fact]
    public void Pitch_class_and_rhythm_stay_and_an_unlocked_root_can_jump()
    {
        var pattern = Line();
        BassGenerator.Vary(pattern, new BassGeneratorOptions
        {
            Root = 4, OctaveJumpChance = 100, LockRoots = false, LowestOctave = 1, Octaves = 2,
        });

        Assert.Equal(40, pattern.Steps[0].Note); // E1 → E2
        Assert.Equal(BassStepState.Note, pattern.Steps[0].State);
        Assert.Equal(4, pattern.Steps[0].Note % 12);
        Assert.Equal(7, pattern.Steps[1].Note % 12);
    }

    [Fact]
    public void No_jump_when_the_chance_is_zero_or_there_is_no_other_octave()
    {
        var held = Line();
        var before = held.Steps.Select(s => (s.State, s.Note, s.Accent, s.Slide)).ToList();
        BassGenerator.Vary(held, new BassGeneratorOptions { OctaveJumpChance = 0, LockRoots = false });
        Assert.Equal(before, held.Steps.Select(s => (s.State, s.Note, s.Accent, s.Slide)).ToList());

        var narrow = Line();
        BassGenerator.Vary(narrow, new BassGeneratorOptions
        {
            Root = 4, OctaveJumpChance = 100, LockRoots = true, LowestOctave = 1, Octaves = 1,
        });
        Assert.Equal(28, narrow.Steps[0].Note);
        Assert.Equal(31, narrow.Steps[1].Note); // G2 is outside a one-octave span from C1
    }

    [Fact]
    public void Same_seed_varies_the_same_way_and_leaves_steps_past_the_length()
    {
        var options = new BassGeneratorOptions { OctaveJumpChance = 40, LockRoots = true, Seed = 5, Octaves = 3 };
        var a = Line();
        a.Length = 8;
        a.Steps[10].State = BassStepState.Note;
        a.Steps[10].Note = 52;
        var b = a.Clone();
        BassGenerator.Vary(a, options);
        BassGenerator.Vary(b, options);
        Assert.Equal(Describe(a), Describe(b));
        Assert.Equal((BassStepState.Note, 52), (a.Steps[10].State, a.Steps[10].Note));

        var differed = false;
        for (var seed = 1; seed <= 12 && !differed; seed++)
        {
            options.Seed = seed;
            var other = Line();
            other.Length = 8;
            other.Steps[10].State = BassStepState.Note;
            other.Steps[10].Note = 52;
            BassGenerator.Vary(other, options);
            differed = Describe(other) != Describe(a);
        }
        Assert.True(differed);
    }

    [Fact]
    public void Zero_amount_changes_nothing()
    {
        var pattern = Line();
        var before = Describe(pattern);
        BassGenerator.Vary(pattern, new BassGeneratorOptions { OctaveJumpChance = 100, LockRoots = false }, amount: 0);
        Assert.Equal(before, Describe(pattern));
    }

    private static string Describe(BassPattern pattern) =>
        string.Join(",", pattern.Steps.Select(s => $"{s.State}{s.Note}{s.Accent}{s.Slide}"));
}
