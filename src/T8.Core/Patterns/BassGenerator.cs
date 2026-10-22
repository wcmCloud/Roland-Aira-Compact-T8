namespace T8.Core.Patterns;

public enum Scale
{
    Major,
    NaturalMinor,
    HarmonicMinor,
    MelodicMinor,
    MajorPentatonic,
    MinorPentatonic,
    Blues,
    Dorian,
    Phrygian,
    Lydian,
    Mixolydian,
    Locrian,
    PhrygianDominant,
}

/// <summary>Chord changes as scale degrees (1-based); each chord is a triad built from the scale.</summary>
public enum Progression
{
    /// <summary>Stay on the root chord.</summary>
    None,
    /// <summary>I – IV – V – I (major) or i – iv – v – i (minor).</summary>
    OneFourFive,
    /// <summary>I – V – vi – IV, the pop progression.</summary>
    OneFiveSixFour,
    /// <summary>i – VI – III – VII, the minor pop progression (degrees 1–6–3–7).</summary>
    OneSixThreeSeven,
    /// <summary>i – VII – VI – VII, the Aeolian rock/trance turnaround.</summary>
    OneSevenSixSeven,
    /// <summary>i – iv, two-chord vamp.</summary>
    OneFour,
    /// <summary>ii – V – I, jazz cadence.</summary>
    TwoFiveOne,
}

/// <summary>Rules for <see cref="BassGenerator"/>. Percentages are 0–100.</summary>
public sealed class BassGeneratorOptions
{
    /// <summary>Key root, 0 = C … 11 = B.</summary>
    public int Root { get; set; } = 4; // E

    public Scale Scale { get; set; } = Scale.MinorPentatonic;
    public Progression Progression { get; set; } = Progression.None;

    /// <summary>Steps per chord: 4, 8 or 16.</summary>
    public int StepsPerChord { get; set; } = 16;

    /// <summary>Octave of the lowest note, as on the T-8 display (C1 = MIDI 24).</summary>
    public int LowestOctave { get; set; } = 1;

    /// <summary>How many octaves the line may span (1–4).</summary>
    public int Octaves { get; set; } = 2;

    /// <summary>Chance that a step plays a note. Steps on the beat get a bonus.</summary>
    public int NoteChance { get; set; } = 65;

    /// <summary>Chance that a note on the beat is a chord tone (root, third or fifth) rather than any scale note.</summary>
    public int ChordToneChance { get; set; } = 75;

    /// <summary>Chance that an empty step after a note holds it (tie) instead of resting.</summary>
    public int TieChance { get; set; } = 20;

    public int SlideChance { get; set; } = 15;
    public int AccentChance { get; set; } = 25;

    /// <summary>Chance of jumping an octave up or down, the classic acid move.</summary>
    public int OctaveJumpChance { get; set; } = 15;

    /// <summary>Seed for the random choices; the same seed and options give the same line.</summary>
    public int Seed { get; set; } = 1;

    /// <summary>
    /// Vary keeps the octave of a chord root that falls on the beat. The rhythm and the pitches
    /// always stay; this stops those roots changing octave.
    /// </summary>
    public bool LockRoots { get; set; } = true;

    public BassGeneratorOptions Clone() => (BassGeneratorOptions)MemberwiseClone();
}

/// <summary>
/// Writes bass lines that follow a key, a scale and optionally a chord progression. Step 1 and each
/// chord change start on the chord's root; other notes on the beat prefer chord tones; the line moves
/// mostly by small intervals, with occasional octave jumps. <see cref="Generate"/> replaces the whole
/// line. <see cref="Vary"/> keeps the rhythm and the pitches, and by default the octave of a root on
/// the beat. Notes stay within the T-8's range.
/// </summary>
public static class BassGenerator
{
    public const int MinNote = 12; // C0
    public const int MaxNote = 96; // C7

    public static readonly IReadOnlyList<string> KeyNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>Semitones above the root for each scale.</summary>
    public static IReadOnlyList<int> Intervals(Scale scale) => scale switch
    {
        Scale.Major => [0, 2, 4, 5, 7, 9, 11],
        Scale.NaturalMinor => [0, 2, 3, 5, 7, 8, 10],
        Scale.HarmonicMinor => [0, 2, 3, 5, 7, 8, 11],
        Scale.MelodicMinor => [0, 2, 3, 5, 7, 9, 11],
        Scale.MajorPentatonic => [0, 2, 4, 7, 9],
        Scale.MinorPentatonic => [0, 3, 5, 7, 10],
        Scale.Blues => [0, 3, 5, 6, 7, 10],
        Scale.Dorian => [0, 2, 3, 5, 7, 9, 10],
        Scale.Phrygian => [0, 1, 3, 5, 7, 8, 10],
        Scale.Lydian => [0, 2, 4, 6, 7, 9, 11],
        Scale.Mixolydian => [0, 2, 4, 5, 7, 9, 10],
        Scale.Locrian => [0, 1, 3, 5, 6, 8, 10],
        Scale.PhrygianDominant => [0, 1, 4, 5, 7, 8, 10],
        _ => throw new ArgumentOutOfRangeException(nameof(scale)),
    };

    public static string DisplayName(Scale scale) => scale switch
    {
        Scale.NaturalMinor => "Natural minor (Aeolian)",
        Scale.HarmonicMinor => "Harmonic minor",
        Scale.MelodicMinor => "Melodic minor",
        Scale.MajorPentatonic => "Major pentatonic",
        Scale.MinorPentatonic => "Minor pentatonic",
        Scale.PhrygianDominant => "Phrygian dominant",
        Scale.Major => "Major (Ionian)",
        _ => scale.ToString(),
    };

    public static string DisplayName(Progression progression) => progression switch
    {
        Progression.None => "None (root only)",
        Progression.OneFourFive => "I – IV – V – I",
        Progression.OneFiveSixFour => "I – V – vi – IV",
        Progression.OneSixThreeSeven => "i – VI – III – VII",
        Progression.OneSevenSixSeven => "i – VII – VI – VII",
        Progression.OneFour => "i – iv",
        Progression.TwoFiveOne => "ii – V – I",
        _ => progression.ToString(),
    };

    /// <summary>Scale degrees (1-based) of the chords, in order.</summary>
    public static IReadOnlyList<int> Degrees(Progression progression) => progression switch
    {
        Progression.None => [1],
        Progression.OneFourFive => [1, 4, 5, 1],
        Progression.OneFiveSixFour => [1, 5, 6, 4],
        Progression.OneSixThreeSeven => [1, 6, 3, 7],
        Progression.OneSevenSixSeven => [1, 7, 6, 7],
        Progression.OneFour => [1, 4],
        Progression.TwoFiveOne => [2, 5, 1, 1],
        _ => throw new ArgumentOutOfRangeException(nameof(progression)),
    };

    /// <summary>Pitch classes (0–11) of the scale in this key.</summary>
    public static IReadOnlyList<int> ScaleClasses(int root, Scale scale) =>
        Intervals(scale).Select(i => (root + i) % 12).ToList();

    /// <summary>Bits 0–11 set for each pitch class, so a keyboard can test a note with one shift.</summary>
    public static int PitchClassMask(IEnumerable<int> pitchClasses)
    {
        var mask = 0;
        foreach (var pitch in pitchClasses)
            mask |= 1 << (((pitch % 12) + 12) % 12);
        return mask;
    }

    /// <summary>Pitch classes (0–11) of the chord on a scale degree: root, third and fifth stacked from the scale.</summary>
    public static IReadOnlyList<int> ChordTones(int root, Scale scale, int degree)
    {
        var intervals = Intervals(scale);
        var count = intervals.Count;
        // Five-note scales skip steps already, so stack every scale note instead of every other one.
        var stride = count >= 7 ? 2 : 1;
        var start = (degree - 1) % count;
        return Enumerable.Range(0, 3)
            .Select(i => (root + intervals[(start + i * stride) % count]) % 12)
            .ToList();
    }

    /// <summary>
    /// Root, third and fifth of the chord sounding at a step (0-based), from the progression and how many
    /// steps each chord lasts. This is the chord <see cref="Generate"/> writes against for that step.
    /// </summary>
    public static IReadOnlyList<int> ChordAt(int root, Scale scale, Progression progression, int stepsPerChord, int stepIndex)
    {
        var degrees = Degrees(progression);
        var index = Math.Max(0, stepIndex) / Math.Max(1, stepsPerChord);
        return ChordTones(root, scale, degrees[index % degrees.Count]);
    }

    /// <summary>A key and scale read off a bass line.</summary>
    public readonly record struct DetectedKey(int Root, Scale Scale);

    /// <summary>
    /// Guesses the key and scale from the notes in the pattern. Notes on the beat, and any pitch played
    /// more than once, have to fit; a single off-beat outlier does not. The tightest scale that holds
    /// those notes wins, and the root is the pitch the line leans on, starting with the first note.
    /// An empty line returns null.
    /// </summary>
    public static DetectedKey? DetectKey(BassPattern pattern)
    {
        var length = Math.Clamp(pattern.Length, 1, BassPattern.StepCount);
        var counts = new int[12];
        var onBeat = new bool[12];
        int? first = null;
        for (var i = 0; i < length; i++)
        {
            if (pattern.Steps[i].State != BassStepState.Note)
                continue;
            var pitch = ((pattern.Steps[i].Note % 12) + 12) % 12;
            counts[pitch]++;
            if (i % 4 == 0)
                onBeat[pitch] = true;
            first ??= pitch;
        }
        if (first is null)
            return null;

        var strong = new bool[12];
        var anyStrong = false;
        for (var pitch = 0; pitch < 12; pitch++)
        {
            // One passing note off the beat is not enough to drag the scale off.
            strong[pitch] = counts[pitch] >= 2 || onBeat[pitch] || pitch == first;
            anyStrong |= strong[pitch];
        }
        if (!anyStrong)
            return null;

        DetectedKey? best = null;
        var bestScore = (Outside: int.MaxValue, Size: int.MaxValue, Root: int.MinValue, First: 1, Rank: int.MaxValue);
        foreach (var scale in Enum.GetValues<Scale>())
        {
            var intervals = Intervals(scale);
            var rank = ScaleRank(scale);
            for (var root = 0; root < 12; root++)
            {
                var inScale = new bool[12];
                foreach (var interval in intervals)
                    inScale[(root + interval) % 12] = true;
                var outside = 0;
                for (var pitch = 0; pitch < 12; pitch++)
                    if (strong[pitch] && !inScale[pitch])
                        outside++;
                var rootWeight = counts[root] * (onBeat[root] ? 2 : 1) + (root == first ? 2 : 0);
                var score = (outside, intervals.Count, rootWeight, root == first ? 0 : 1, rank);
                if (best is not null && CompareDetected(score, bestScore) >= 0)
                    continue;
                best = new DetectedKey(root, scale);
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>Lower is a closer, more usual name when two scales fit equally.</summary>
    private static int ScaleRank(Scale scale) => scale switch
    {
        Scale.MinorPentatonic => 0,
        Scale.MajorPentatonic => 1,
        Scale.Blues => 2,
        Scale.NaturalMinor => 3,
        Scale.Major => 4,
        Scale.Dorian => 5,
        Scale.Mixolydian => 6,
        Scale.HarmonicMinor => 7,
        Scale.MelodicMinor => 8,
        Scale.Phrygian => 9,
        Scale.Lydian => 10,
        Scale.PhrygianDominant => 11,
        Scale.Locrian => 12,
        _ => 13,
    };

    private static int CompareDetected(
        (int Outside, int Size, int Root, int First, int Rank) score,
        (int Outside, int Size, int Root, int First, int Rank) best)
    {
        var outside = score.Outside.CompareTo(best.Outside);
        if (outside != 0)
            return outside;
        var size = score.Size.CompareTo(best.Size);
        if (size != 0)
            return size;
        var root = best.Root.CompareTo(score.Root);
        if (root != 0)
            return root;
        var first = score.First.CompareTo(best.First);
        return first != 0 ? first : score.Rank.CompareTo(best.Rank);
    }

    /// <summary>Fills steps 1 to <paramref name="length"/> with a new line and turns the rest off.</summary>
    public static void Generate(BassPattern pattern, BassGeneratorOptions options, int? length = null)
    {
        var steps = Math.Clamp(length ?? pattern.Length, 1, BassPattern.StepCount);
        var random = new Random(options.Seed);
        var scaleClasses = ScaleClasses(options.Root, options.Scale).ToHashSet();
        var stepsPerChord = Math.Max(1, options.StepsPerChord);

        var low = Math.Clamp((options.LowestOctave + 1) * 12, MinNote, MaxNote);
        var high = Math.Clamp(low + Math.Clamp(options.Octaves, 1, 4) * 12, low, MaxNote);
        var scaleNotes = Enumerable.Range(low, high - low + 1).Where(n => scaleClasses.Contains(n % 12)).ToList();

        int? previous = null;
        var lastWasSound = false;

        foreach (var step in pattern.Steps)
        {
            step.State = BassStepState.Off;
            step.Accent = false;
            step.Slide = false;
        }

        for (var n = 0; n < steps; n++)
        {
            var step = pattern.Steps[n];
            var chord = ChordAt(options.Root, options.Scale, options.Progression, stepsPerChord, n);
            var chordChange = n % stepsPerChord == 0;
            var onBeat = n % 4 == 0;

            var chance = onBeat ? Math.Min(100, options.NoteChance + 25) : options.NoteChance;
            if (!chordChange && random.Next(100) >= chance)
            {
                // No new note: hold the last one or rest.
                if (lastWasSound && random.Next(100) < options.TieChance)
                {
                    step.State = BassStepState.Tie;
                    step.Note = pattern.Steps[n - 1].Note;
                }
                else
                {
                    lastWasSound = false;
                }
                continue;
            }

            int note;
            if (chordChange)
            {
                // Each chord starts on its root, in the lowest octave of the range.
                note = Nearest(scaleNotes.Where(x => x % 12 == chord[0]).ToList(), previous ?? low);
            }
            else
            {
                var useChord = onBeat && random.Next(100) < options.ChordToneChance;
                var candidates = useChord ? scaleNotes.Where(x => chord.Contains(x % 12)).ToList() : scaleNotes;
                if (candidates.Count == 0)
                    candidates = scaleNotes;
                note = PickNear(candidates, previous ?? low, random);
            }

            if (!chordChange && random.Next(100) < options.OctaveJumpChance)
            {
                var jumped = note + (random.Next(2) == 0 ? 12 : -12);
                if (jumped >= low && jumped <= high)
                    note = jumped;
            }

            step.State = BassStepState.Note;
            step.Note = note;
            step.Accent = random.Next(100) < options.AccentChance;
            step.Slide = random.Next(100) < options.SlideChance;
            previous = note;
            lastWasSound = true;
        }
    }

    /// <summary>
    /// Keeps the rhythm and the pitch of every note. Roots that fall on the beat keep their octave
    /// when <see cref="BassGeneratorOptions.LockRoots"/> is set; other notes may jump an octave,
    /// using <see cref="BassGeneratorOptions.OctaveJumpChance"/>. Ties follow the note they hold.
    /// <paramref name="amount"/> is 1 for a full variation and lower for one that stays closer.
    /// </summary>
    public static void Vary(BassPattern pattern, BassGeneratorOptions options, double amount = 1)
    {
        amount = Math.Clamp(amount, 0, 1);
        if (amount == 0)
            return;

        var random = new Random(unchecked(options.Seed * 31 + 0x0C7A));
        var steps = Math.Clamp(pattern.Length, 1, BassPattern.StepCount);
        var jumpChance = (int)Math.Round(Math.Clamp(options.OctaveJumpChance, 0, 100) * amount);
        var stepsPerChord = Math.Max(1, options.StepsPerChord);
        var low = Math.Clamp((options.LowestOctave + 1) * 12, MinNote, MaxNote);
        var high = Math.Clamp(low + Math.Clamp(options.Octaves, 1, 4) * 12, low, MaxNote);

        for (var n = 0; n < steps; n++)
        {
            var step = pattern.Steps[n];
            if (step.State == BassStepState.Tie)
            {
                if (n > 0)
                    step.Note = pattern.Steps[n - 1].Note;
                continue;
            }
            if (step.State != BassStepState.Note || jumpChance <= 0)
                continue;

            var chord = ChordAt(options.Root, options.Scale, options.Progression, stepsPerChord, n);
            if (options.LockRoots && n % 4 == 0 && step.Note % 12 == chord[0])
                continue;
            if (random.Next(100) >= jumpChance)
                continue;

            var others = OctavesOf(step.Note, low, high).Where(note => note != step.Note).ToList();
            if (others.Count == 0)
                continue;
            step.Note = others[random.Next(others.Count)];
        }
    }

    /// <summary>Every octave of <paramref name="note"/> inside the range, plus the note itself when it sits outside.</summary>
    private static List<int> OctavesOf(int note, int low, int high)
    {
        var notes = new List<int>();
        var pitch = ((note % 12) + 12) % 12;
        for (var candidate = pitch; candidate <= MaxNote; candidate += 12)
        {
            if (candidate >= MinNote && candidate >= low && candidate <= high)
                notes.Add(candidate);
        }
        if (note >= MinNote && note <= MaxNote && !notes.Contains(note))
            notes.Add(note);
        return notes;
    }

    private static int Nearest(IReadOnlyList<int> candidates, int target) =>
        candidates.Count == 0 ? target : candidates.MinBy(c => Math.Abs(c - target));

    /// <summary>Picks a note, favouring small steps from the previous note (weight 1 / (1 + distance)).</summary>
    private static int PickNear(IReadOnlyList<int> candidates, int previous, Random random)
    {
        var weights = candidates.Select(c => 1.0 / (1 + Math.Abs(c - previous) / 2.0)).ToList();
        var roll = random.NextDouble() * weights.Sum();
        for (var i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0)
                return candidates[i];
        }
        return candidates[^1];
    }
}
