namespace T8.Core.Patterns;

public enum RhythmStyle
{
    Techno,
    House,
    Electro,
    Breakbeat,
    HipHop,
    Trap,
    DrumAndBass,
    Minimal,
    Random,
}

/// <summary>Rules for <see cref="RhythmGenerator"/>. Percentages are 0–100.</summary>
public sealed class RhythmGeneratorOptions
{
    public RhythmStyle Style { get; set; } = RhythmStyle.Techno;

    /// <summary>How busy the pattern is: 50 follows the style, lower thins it, higher fills it.</summary>
    public int Density { get; set; } = 50;

    /// <summary>How far to stray from the style: extra hits off the template and dropped optional hits.</summary>
    public int Variation { get; set; } = 20;

    public bool BassDrum { get; set; } = true;
    public bool Snare { get; set; } = true;
    public bool Tom { get; set; } = true;
    public bool Clap { get; set; } = true;
    public bool ClosedHat { get; set; } = true;
    public bool OpenHat { get; set; } = true;

    /// <summary>Chance of an accent on a beat (off-beat steps get a third of it).</summary>
    public int AccentChance { get; set; } = 30;

    /// <summary>Chance of quiet extra snare and hat notes between the main hits.</summary>
    public int GhostChance { get; set; } = 15;

    /// <summary>Chance that a hat or snare hit becomes a roll (sub steps), or a snare a flam.</summary>
    public int RollChance { get; set; } = 10;

    /// <summary>
    /// Chance that an optional hit gets a T-8 step probability below 100%, so the T-8 itself varies
    /// the pattern on every loop.
    /// </summary>
    public int LiveVariation { get; set; } = 0;

    /// <summary>Put a snare/tom fill in the last four steps.</summary>
    public bool Fill { get; set; }

    public int Seed { get; set; } = 1;

    /// <summary>Pattern number (1–16) in the current bank that Make fill writes to.</summary>
    public int FillPattern { get; set; } = 16;

    public RhythmGeneratorOptions Clone() => (RhythmGeneratorOptions)MemberwiseClone();
}

/// <summary>
/// Writes drum patterns from a style template: each style gives every instrument a chance per 16th
/// note, with the defining hits (like a four-on-the-floor kick) always on. Density scales the other
/// chances, variation adds and drops hits, and accents, ghost notes, rolls, T-8 step probability and
/// a fill add movement. Hits are written for the T-8's sounds: BD, SD, tom (LT), clap (HT), CH, OH.
/// </summary>
public static class RhythmGenerator
{
    /// <summary>
    /// Chance per 16th note: '-' never, '1'–'9' 10–90 %, 'X' always (a defining hit).
    /// Order: BD, SD, tom, clap, CH, OH.
    /// </summary>
    private static readonly Dictionary<RhythmStyle, string[]> Templates = new()
    {
        [RhythmStyle.Techno] =
        [
            "X---X---X---X---",
            "----------2---3-",
            "-------2------3-",
            "----X-------X---",
            "4-6-4-6-4-6-4-6-",
            "--X---X---X---X-",
        ],
        [RhythmStyle.House] =
        [
            "X---X---X---X---",
            "-------3-----3--",
            "----------2-----",
            "----X-------X---",
            "X-3-X-3-X-3-X-3-",
            "--X---X---X---X-",
        ],
        [RhythmStyle.Electro] =
        [
            "X------X--X---3-",
            "----X-------X---",
            "------3-------4-",
            "----5-------5---",
            "X-X-X-X-X-X-X-X-",
            "-------------3--",
        ],
        [RhythmStyle.Breakbeat] =
        [
            "X-----X---X---3-",
            "----X--3-3--X--4",
            "--------------3-",
            "----2-------2---",
            "X-X-X-X-X-X-X-X-",
            "-------3-------4",
        ],
        [RhythmStyle.HipHop] =
        [
            "X------3X-5-----",
            "----X-------X---",
            "---------------2",
            "----5-------5---",
            "X-X-X-X-X-X-X-X-",
            "-------------3--",
        ],
        [RhythmStyle.Trap] =
        [
            "X------3-3X----3",
            "--------X-------",
            "-------------3-3",
            "--------X-------",
            "XXXXXXXXXXXXXXXX",
            "---------------3",
        ],
        [RhythmStyle.DrumAndBass] =
        [
            "X---------X-----",
            "----X-------X--3",
            "-------------2--",
            "----3-------3---",
            "X-X-X-X-X-X-X-X-",
            "-------3-------3",
        ],
        [RhythmStyle.Minimal] =
        [
            "X---X---X---X---",
            "---------------2",
            "--3-----3--3----",
            "-------3----X---",
            "-3X3-3X3-3X3-3X3",
            "--------------3-",
        ],
        [RhythmStyle.Random] =
        [
            "6-3-5-3-6-3-5-3-",
            "2-2-6-2-2-2-6-2-",
            "1-1-1-2-1-1-1-2-",
            "1-1-3-1-1-1-3-1-",
            "5454545454545454",
            "1-2-1-2-1-2-1-2-",
        ],
    };

    private static readonly RhythmTrack[] TemplateTracks =
        [RhythmTrack.BD, RhythmTrack.SD, RhythmTrack.LT, RhythmTrack.HT, RhythmTrack.CH, RhythmTrack.OH];

    public static string DisplayName(RhythmStyle style) => style switch
    {
        RhythmStyle.HipHop => "Hip-hop / boom bap",
        RhythmStyle.DrumAndBass => "Drum & bass",
        RhythmStyle.Minimal => "Minimal / acid",
        RhythmStyle.Random => "Random (no style)",
        _ => style.ToString(),
    };

    /// <summary>The style's chance (0–100) for a track on a step, and whether it is a defining hit.</summary>
    public static (int Chance, bool Anchor) TemplateChance(RhythmStyle style, RhythmTrack track, int step)
    {
        var index = Array.IndexOf(TemplateTracks, track);
        if (index < 0)
            return (0, false);
        var c = Templates[style][index][step % 16];
        return c switch
        {
            'X' => (100, true),
            >= '1' and <= '9' => ((c - '0') * 10, false),
            _ => (0, false),
        };
    }

    /// <summary>Fills steps 1 to <paramref name="length"/> with a new pattern and clears the rest.</summary>
    public static void Generate(RhythmPattern pattern, RhythmGeneratorOptions options, int? length = null)
    {
        var steps = Math.Clamp(length ?? pattern.Length, 1, RhythmPattern.StepCount);
        var random = new Random(options.Seed);
        var density = Math.Clamp(options.Density, 0, 100) / 50.0; // 1 = as the style
        var variation = Math.Clamp(options.Variation, 0, 100) / 100.0;

        foreach (var step in pattern.Steps)
        {
            foreach (var cell in step.Cells)
                Reset(cell);
        }

        for (var n = 0; n < steps; n++)
        {
            var step = pattern.Steps[n];
            var onBeat = n % 4 == 0;
            var inFill = options.Fill && steps >= 8 && n >= steps - 4;

            foreach (var track in TemplateTracks)
            {
                if (!Enabled(options, track))
                    continue;

                var (chance, anchor) = TemplateChance(options.Style, track, n);
                double p = anchor ? 1.0 : Math.Min(1.0, chance / 100.0 * density);
                if (!anchor && chance > 0)
                    p *= 1 - variation * 0.5; // variation drops optional hits…
                if (chance == 0)
                    p = variation * 0.12 * density; // …and adds hits off the template.
                if (inFill && track is RhythmTrack.SD or RhythmTrack.LT)
                    p = Math.Max(p, 0.55);

                if (random.NextDouble() >= p)
                {
                    // Ghost notes: quiet snare and hat hits between the main ones.
                    if (track is RhythmTrack.SD or RhythmTrack.CH && !onBeat && random.Next(100) < options.GhostChance)
                        Hit(step[track], velocity: 3);
                    continue;
                }

                var cell = step[track];
                var velocity = anchor ? 9 : onBeat ? 8 : 7;
                velocity += random.Next(3) - 1; // a little human variation
                Hit(cell, Math.Clamp(velocity, 4, 10));

                if (!anchor && random.Next(100) < options.LiveVariation)
                    cell.Probability = 5 + random.Next(4); // P.50 – P.80

                if (random.Next(100) < options.RollChance)
                {
                    if (track is RhythmTrack.CH or RhythmTrack.OH)
                        cell.SubStep = (SubStep)(1 + random.Next(3)); // 1/2, 1/3 or 1/4
                    else if (track == RhythmTrack.SD)
                        cell.SubStep = random.Next(2) == 0 ? SubStep.Flam : SubStep.Half;
                }
                if (inFill && track is RhythmTrack.SD or RhythmTrack.LT && random.Next(2) == 0)
                    cell.SubStep = SubStep.Half;
            }

            // The T-8 chokes the closed hat with the open hat; keep the open one.
            if (step[RhythmTrack.OH].On && step[RhythmTrack.CH].On)
                Reset(step[RhythmTrack.CH]);

            var accentChance = onBeat ? options.AccentChance : options.AccentChance / 3;
            if (step.Cells.Any(c => c.On) && random.Next(100) < accentChance)
                Hit(step[RhythmTrack.AC], velocity: 10);
        }
    }

    /// <summary>
    /// Writes a fill for <paramref name="source"/> into <paramref name="target"/>: the same length,
    /// shuffle and flam, the groove kept for the first half, then a build-up of snare and tom hits
    /// that get denser, louder and rolled towards the end, finishing on kick, open hat and accent.
    /// Assign the target on the T-8 as its fill pattern (SHIFT + STEP 8) and trigger it with
    /// SHIFT + STEP 7.
    /// </summary>
    public static void GenerateFill(RhythmPattern source, RhythmPattern target, RhythmGeneratorOptions options)
    {
        var random = new Random(options.Seed ^ 0x5F11);
        target.Length = source.Length;
        target.Scale = source.Scale;
        target.Shuffle = source.Shuffle;
        target.Flam = source.Flam;
        for (var s = 0; s < RhythmPattern.StepCount; s++)
        {
            for (var t = 0; t < RhythmPattern.TrackCount; t++)
                Copy(source.Steps[s].Cells[t], target.Steps[s].Cells[t]);
        }

        var length = source.Length;
        var start = length >= 8 ? length / 2 : 0;
        for (var n = start; n < length; n++)
        {
            var step = target.Steps[n];
            // 0 at the start of the build-up, 1 on the last step.
            var t = length - 1 == start ? 1.0 : (double)(n - start) / (length - 1 - start);

            // Make room: the groove's snare, tom, clap and closed hat give way to the fill.
            foreach (var track in new[] { RhythmTrack.SD, RhythmTrack.LT, RhythmTrack.HT, RhythmTrack.CH })
            {
                if (random.NextDouble() < 0.4 + 0.5 * t)
                    Reset(step[track]);
            }
            if (n % 4 != 0)
                Reset(step[RhythmTrack.BD]);

            var drum = options.Tom && (!options.Snare || random.NextDouble() < 0.35 + 0.2 * t) ? RhythmTrack.LT : RhythmTrack.SD;
            if (!Enabled(options, drum))
                drum = Enabled(options, RhythmTrack.SD) ? RhythmTrack.SD : RhythmTrack.LT;
            if (Enabled(options, drum) && random.NextDouble() < 0.35 + 0.6 * t)
            {
                var cell = step[drum];
                Hit(cell, Math.Clamp(5 + (int)Math.Round(5 * t) + random.Next(2) - 1, 4, 10));
                var roll = random.NextDouble();
                if (roll < 0.15 + 0.5 * t)
                    cell.SubStep = t > 0.6 && random.Next(2) == 0 ? SubStep.Quarter : t > 0.3 ? SubStep.Third : SubStep.Half;
                if (random.Next(100) < options.AccentChance + (int)(40 * t))
                    Hit(step[RhythmTrack.AC], velocity: 10);
            }
        }

        // Land on the last step: kick, open hat and accent, like a crash.
        var last = target.Steps[length - 1];
        if (options.BassDrum)
            Hit(last[RhythmTrack.BD], 10);
        if (options.OpenHat)
        {
            Hit(last[RhythmTrack.OH], 10);
            Reset(last[RhythmTrack.CH]);
        }
        Hit(last[RhythmTrack.AC], 10);
    }

    private static void Copy(RhythmCell from, RhythmCell to)
    {
        to.On = from.On;
        to.Velocity = from.Velocity;
        to.SubStep = from.SubStep;
        to.Probability = from.Probability;
        to.SubStepProbability = from.SubStepProbability;
    }

    public static bool Enabled(RhythmGeneratorOptions options, RhythmTrack track) => track switch
    {
        RhythmTrack.BD => options.BassDrum,
        RhythmTrack.SD => options.Snare,
        RhythmTrack.LT => options.Tom,
        RhythmTrack.HT => options.Clap,
        RhythmTrack.CH => options.ClosedHat,
        RhythmTrack.OH => options.OpenHat,
        _ => false,
    };

    private static void Hit(RhythmCell cell, int velocity)
    {
        cell.On = true;
        cell.Velocity = velocity;
    }

    private static void Reset(RhythmCell cell)
    {
        cell.On = false;
        cell.Velocity = 0;
        cell.SubStep = SubStep.Off;
        cell.Probability = 10;
        cell.SubStepProbability = 10;
    }
}
