using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using T8.Core.Patterns;

namespace T8.Midi;

/// <summary>
/// Turns T-8 patterns into timed MIDI events so they can be previewed through the T-8's sounds
/// (or any synth) and exported as a .mid file. Uses DryWetMIDI's core API, which runs on every OS.
///
/// The timing of shuffle, flam and the velocity curve are approximations of the hardware.
/// </summary>
public sealed class PatternMidiBuilder
{
    public const short TicksPerQuarterNote = 96;
    public const int TicksPerStep = TicksPerQuarterNote / 4; // 16th notes; divisible by 2, 3 and 4

    private const int DrumGateTicks = 6;
    private const int BassGateTicks = TicksPerStep / 2;

    public T8MidiMap Map { get; init; } = new();

    /// <summary>Seed for probability rolls. Null uses a random seed each build.</summary>
    public int? Seed { get; init; }

    public static TempoMap CreateTempoMap(double bpm) =>
        TempoMap.Create(new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote), Tempo.FromBeatsPerMinute(bpm));

    /// <summary>
    /// Number of steps until bass and rhythm line up again (the T-8 loops each part on its own length).
    /// </summary>
    public static int LoopSteps(BassPattern? bass, RhythmPattern? rhythm)
    {
        var a = bass?.Length ?? 0;
        var b = rhythm?.Length ?? 0;
        if (a == 0) return b;
        if (b == 0) return a;
        return a / Gcd(a, b) * b;

        static int Gcd(int x, int y) => y == 0 ? x : Gcd(y, x % y);
    }

    public List<TimedEvent> Build(BassPattern? bass, RhythmPattern? rhythm)
    {
        var random = Seed is { } seed ? new Random(seed) : new Random();
        var totalSteps = LoopSteps(bass, rhythm);
        var events = new List<TimedEvent>();

        if (rhythm is not null)
            AddRhythm(events, rhythm, totalSteps, random);
        if (bass is not null)
            AddBass(events, bass, totalSteps);

        return events.OrderBy(e => e.Time).ThenBy(e => e.Event is NoteOnEvent ? 1 : 0).ToList();
    }

    public long LengthInTicks(BassPattern? bass, RhythmPattern? rhythm) =>
        (long)LoopSteps(bass, rhythm) * TicksPerStep;

    public MidiFile BuildMidiFile(BassPattern? bass, RhythmPattern? rhythm, double bpm)
    {
        var events = Build(bass, rhythm);
        var tempoMap = CreateTempoMap(bpm);
        var track = events.ToTrackChunk();
        var file = new MidiFile(track) { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote) };
        file.ReplaceTempoMap(tempoMap);
        return file;
    }

    private void AddRhythm(List<TimedEvent> events, RhythmPattern pattern, int totalSteps, Random random)
    {
        var channel = (FourBitNumber)(Map.RhythmChannel - 1);
        // Shuffle delays every second 16th; 200 (the largest factory value) is taken as a triplet feel.
        var shuffleTicks = TicksPerStep * pattern.Shuffle / 600;
        var flamTicks = Math.Max(1, TicksPerStep * pattern.Flam / 200);

        for (var n = 0; n < totalSteps; n++)
        {
            var step = pattern.Steps[n % pattern.Length];
            var stepStart = (long)n * TicksPerStep + (n % 2 == 1 ? shuffleTicks : 0);
            var accent = step[RhythmTrack.AC].On;

            foreach (var (track, note) in Map.RhythmNotes)
            {
                var cell = step[track];
                if (!cell.On || !Roll(random, cell.Probability))
                    continue;

                var velocity = Velocity(cell.Velocity, accent);
                switch (cell.SubStep)
                {
                    case SubStep.Off:
                        AddNote(events, channel, note, velocity, stepStart, DrumGateTicks);
                        break;
                    case SubStep.Flam:
                        AddNote(events, channel, note, Math.Max(1, velocity / 2), stepStart, DrumGateTicks / 2);
                        AddNote(events, channel, note, velocity, stepStart + flamTicks, DrumGateTicks);
                        break;
                    default:
                        var hits = (int)cell.SubStep + 1;
                        var spacing = TicksPerStep / hits;
                        for (var h = 0; h < hits; h++)
                        {
                            if (h > 0 && !Roll(random, cell.SubStepProbability))
                                continue;
                            AddNote(events, channel, note, velocity, stepStart + h * spacing, Math.Min(DrumGateTicks, spacing - 1));
                        }
                        break;
                }
            }
        }
    }

    private void AddBass(List<TimedEvent> events, BassPattern pattern, int totalSteps)
    {
        var channel = (FourBitNumber)(Map.BassChannel - 1);

        for (var n = 0; n < totalSteps; n++)
        {
            var step = pattern.Steps[n % pattern.Length];
            if (step.State != BassStepState.Note)
                continue;

            // Ties extend the note through following steps.
            var lastStep = n;
            while (lastStep + 1 < totalSteps && pattern.Steps[(lastStep + 1) % pattern.Length].State == BassStepState.Tie)
                lastStep++;

            var start = (long)n * TicksPerStep;
            var lastStepData = pattern.Steps[lastStep % pattern.Length];
            var nextIsNote = lastStep + 1 < totalSteps && pattern.Steps[(lastStep + 1) % pattern.Length].State == BassStepState.Note;

            // Slide: hold into the next note so a mono synth glides (legato overlap).
            var slide = (step.Slide || lastStepData.Slide) && nextIsNote;
            long length = slide
                ? (lastStep - n + 1) * TicksPerStep + 2
                : (lastStep - n) * TicksPerStep + BassGateTicks;

            AddNote(events, channel, step.Note, step.Accent ? 127 : 90, start, length);
            n = lastStep;
        }
    }

    private static void AddNote(List<TimedEvent> events, FourBitNumber channel, int note, int velocity, long time, long length)
    {
        var number = (SevenBitNumber)Math.Clamp(note, 0, 127);
        events.Add(new TimedEvent(new NoteOnEvent(number, (SevenBitNumber)Math.Clamp(velocity, 1, 127)) { Channel = channel }, time));
        events.Add(new TimedEvent(new NoteOffEvent(number, SevenBitNumber.MinValue) { Channel = channel }, time + Math.Max(1, length)));
    }

    /// <summary>T-8 velocity v.0–v.10 to MIDI velocity; accent adds a fixed boost.</summary>
    internal static int Velocity(int t8Velocity, bool accent)
    {
        var velocity = 10 + t8Velocity * 9; // v.7 → 73, v.10 → 100
        return Math.Clamp(accent ? velocity + 27 : velocity, 1, 127);
    }

    private static bool Roll(Random random, int probabilityTens) =>
        probabilityTens >= 10 || random.Next(10) < probabilityTens;
}
