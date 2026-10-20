using T8.Core.Patterns;

namespace T8.Core.Tests;

public class RhythmGeneratorTests
{
    private static RhythmPattern Generate(RhythmGeneratorOptions options, int length = 16)
    {
        var pattern = new RhythmPattern { Length = length };
        RhythmGenerator.Generate(pattern, options);
        return pattern;
    }

    private static IEnumerable<int> Hits(RhythmPattern pattern, RhythmTrack track) =>
        pattern.Steps.Select((s, i) => (s, i)).Where(x => x.s[track].On && x.s[track].Velocity > 3).Select(x => x.i);

    public static TheoryData<RhythmStyle> AllStyles() => new(Enum.GetValues<RhythmStyle>());

    [Theory]
    [MemberData(nameof(AllStyles))]
    public void Defining_hits_are_always_there(RhythmStyle style)
    {
        for (var seed = 1; seed <= 10; seed++)
        {
            var pattern = Generate(new RhythmGeneratorOptions { Style = style, Seed = seed, Density = 0, Variation = 100 });
            foreach (var track in new[] { RhythmTrack.BD, RhythmTrack.SD, RhythmTrack.LT, RhythmTrack.HT, RhythmTrack.OH })
            {
                for (var n = 0; n < 16; n++)
                {
                    if (RhythmGenerator.TemplateChance(style, track, n).Anchor)
                        Assert.True(pattern.Steps[n][track].On, $"{style} {track} step {n + 1}");
                }
            }
        }
    }

    [Fact]
    public void Techno_has_four_on_the_floor_and_offbeat_open_hats()
    {
        var pattern = Generate(new RhythmGeneratorOptions { Style = RhythmStyle.Techno, Variation = 0 });
        Assert.Equal([0, 4, 8, 12], Hits(pattern, RhythmTrack.BD));
        Assert.Equal([2, 6, 10, 14], Hits(pattern, RhythmTrack.OH));
        Assert.Contains(4, Hits(pattern, RhythmTrack.HT));
    }

    [Fact]
    public void Same_seed_gives_the_same_pattern_and_another_seed_a_different_one()
    {
        var options = new RhythmGeneratorOptions { Style = RhythmStyle.Breakbeat, Seed = 3, Variation = 50 };
        var a = Describe(Generate(options, 32));
        Assert.Equal(a, Describe(Generate(options, 32)));
        options.Seed = 4;
        Assert.NotEqual(a, Describe(Generate(options, 32)));
    }

    [Fact]
    public void Disabled_instruments_stay_silent_and_cy_is_never_used()
    {
        var options = new RhythmGeneratorOptions { Style = RhythmStyle.Random, Snare = false, OpenHat = false, Density = 100, Variation = 100 };
        var pattern = Generate(options, 32);
        Assert.DoesNotContain(pattern.Steps, s => s[RhythmTrack.SD].On || s[RhythmTrack.OH].On || s[RhythmTrack.CY].On);
        Assert.Contains(pattern.Steps, s => s[RhythmTrack.BD].On);
    }

    [Fact]
    public void Only_fills_the_length_and_clears_the_rest()
    {
        var pattern = new RhythmPattern { Length = 32 };
        foreach (var step in pattern.Steps)
            foreach (var cell in step.Cells)
                cell.On = true;

        RhythmGenerator.Generate(pattern, new RhythmGeneratorOptions(), length: 12);

        Assert.All(pattern.Steps.Skip(12), s => Assert.All(s.Cells, c => Assert.False(c.On)));
        Assert.All(pattern.Steps.Skip(12), s => Assert.All(s.Cells, c => Assert.Equal("000AA", c.ToString())));
    }

    [Fact]
    public void Closed_hat_never_plays_with_the_open_hat()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var pattern = Generate(new RhythmGeneratorOptions { Style = RhythmStyle.Random, Seed = seed, Density = 100, Variation = 100 }, 32);
            Assert.DoesNotContain(pattern.Steps, s => s[RhythmTrack.OH].On && s[RhythmTrack.CH].On);
        }
    }

    [Fact]
    public void Live_variation_sets_t8_step_probability_on_optional_hits_only()
    {
        var pattern = Generate(new RhythmGeneratorOptions { Style = RhythmStyle.Techno, LiveVariation = 100, Density = 100 }, 32);
        var cells = pattern.Steps.SelectMany(s => s.Cells).Where(c => c.On).ToList();
        Assert.Contains(cells, c => c.Probability is >= 5 and <= 8);
        // Four-on-the-floor kicks are defining hits and keep P.100.
        Assert.All(new[] { 0, 4, 8, 12 }, n => Assert.Equal(10, pattern.Steps[n][RhythmTrack.BD].Probability));
    }

    [Fact]
    public void Rolls_use_sub_steps_and_accents_use_the_accent_track()
    {
        var pattern = Generate(new RhythmGeneratorOptions { Style = RhythmStyle.Trap, RollChance = 100, AccentChance = 100 }, 16);
        Assert.Contains(pattern.Steps, s => s[RhythmTrack.CH].SubStep is SubStep.Half or SubStep.Third or SubStep.Quarter);
        Assert.True(pattern.Steps[0][RhythmTrack.AC].On);
    }

    [Fact]
    public void Fill_puts_snare_or_tom_in_the_last_four_steps()
    {
        for (var seed = 1; seed <= 10; seed++)
        {
            var pattern = Generate(new RhythmGeneratorOptions { Style = RhythmStyle.House, Fill = true, Seed = seed }, 16);
            var fillHits = pattern.Steps.Skip(12).Count(s => s[RhythmTrack.SD].On || s[RhythmTrack.LT].On);
            Assert.True(fillHits >= 1, $"seed {seed}");
        }
    }

    [Fact]
    public void Names_cover_every_style()
    {
        Assert.All(Enum.GetValues<RhythmStyle>(), s => Assert.False(string.IsNullOrEmpty(RhythmGenerator.DisplayName(s))));
        Assert.Equal("Drum & bass", RhythmGenerator.DisplayName(RhythmStyle.DrumAndBass));
    }

    private static string Describe(RhythmPattern pattern) =>
        string.Join(",", pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString()));
}

public class RhythmFillTests
{
    private static RhythmPattern Groove()
    {
        var groove = new RhythmPattern { Length = 16, Shuffle = 40 };
        RhythmGenerator.Generate(groove, new RhythmGeneratorOptions { Style = RhythmStyle.House, Variation = 0 });
        return groove;
    }

    [Fact]
    public void Keeps_the_groove_for_the_first_half_and_settings()
    {
        var groove = Groove();
        var fill = new RhythmPattern();
        RhythmGenerator.GenerateFill(groove, fill, new RhythmGeneratorOptions());

        Assert.Equal((16, 40), (fill.Length, fill.Shuffle));
        for (var n = 0; n < 8; n++)
            Assert.Equal(groove.Steps[n].Cells.Select(c => c.ToString()), fill.Steps[n].Cells.Select(c => c.ToString()));
        Assert.NotEqual(Describe(groove), Describe(fill));
    }

    [Fact]
    public void Builds_up_with_snare_or_tom_and_lands_on_kick_open_hat_and_accent()
    {
        for (var seed = 1; seed <= 10; seed++)
        {
            var fill = new RhythmPattern();
            RhythmGenerator.GenerateFill(Groove(), fill, new RhythmGeneratorOptions { Seed = seed });

            var secondHalf = fill.Steps.Skip(8).Take(8).Count(s => s[RhythmTrack.SD].On || s[RhythmTrack.LT].On);
            Assert.True(secondHalf >= 2, $"seed {seed}: {secondHalf} hits");
            var last = fill.Steps[15];
            Assert.True(last[RhythmTrack.BD].On && last[RhythmTrack.OH].On && last[RhythmTrack.AC].On);
            Assert.False(last[RhythmTrack.CH].On);
        }
    }

    [Fact]
    public void Gets_denser_towards_the_end()
    {
        var early = 0;
        var late = 0;
        for (var seed = 1; seed <= 40; seed++)
        {
            var fill = new RhythmPattern();
            RhythmGenerator.GenerateFill(Groove(), fill, new RhythmGeneratorOptions { Seed = seed });
            early += fill.Steps.Skip(8).Take(3).Count(s => s[RhythmTrack.SD].On || s[RhythmTrack.LT].On);
            late += fill.Steps.Skip(12).Take(3).Count(s => s[RhythmTrack.SD].On || s[RhythmTrack.LT].On);
        }
        Assert.True(late > early, $"early {early}, late {late}");
    }

    [Fact]
    public void Leaves_the_source_untouched()
    {
        var groove = Groove();
        var before = Describe(groove);
        RhythmGenerator.GenerateFill(groove, new RhythmPattern(), new RhythmGeneratorOptions());
        Assert.Equal(before, Describe(groove));
    }

    private static string Describe(RhythmPattern pattern) =>
        string.Join(",", pattern.Steps.SelectMany(s => s.Cells).Select(c => c.ToString()));
}
