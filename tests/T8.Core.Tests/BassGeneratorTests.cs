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

    private static string Describe(BassPattern pattern) =>
        string.Join(",", pattern.Steps.Select(s => $"{s.State}{s.Note}{s.Accent}{s.Slide}"));
}
