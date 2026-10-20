using T8.Core.Prm;

namespace T8.Core.Patterns;

/// <summary>Rhythm tracks in file order. AC is the shared accent track.</summary>
public enum RhythmTrack
{
    AC,
    BD,
    SD,
    LT,
    HT,
    CY,
    CH,
    OH,
}

public enum SubStep
{
    Off = 0,
    Half = 1,    // 1_2
    Third = 2,   // 1_3
    Quarter = 3, // 1_4
    Flam = 4,    // FLAN
}

/// <summary>
/// One track on one step, stored as a five-character code such as "170AA".
/// Each character is a value 0–10, written 0–9 then A.
/// </summary>
public sealed class RhythmCell
{
    public bool On { get; set; }

    /// <summary>Velocity v.0–v.10. Default for a new note is 7.</summary>
    public int Velocity { get; set; } = 7;

    public SubStep SubStep { get; set; } = SubStep.Off;

    /// <summary>Probability in tens of percent: 10 = P.100.</summary>
    public int Probability { get; set; } = 10;

    /// <summary>Sub step probability in tens of percent: 10 = P.100.</summary>
    public int SubStepProbability { get; set; } = 10;

    public RhythmCell Clone() => (RhythmCell)MemberwiseClone();

    public static RhythmCell Parse(string code)
    {
        if (code.Length != 5)
            throw new FormatException($"Rhythm cell code must be 5 characters, got '{code}'.");

        var on = Digit(code[0], 1);
        var subStep = Digit(code[2], 4);
        return new RhythmCell
        {
            On = on == 1,
            Velocity = Digit(code[1], 10),
            SubStep = (SubStep)subStep,
            Probability = Digit(code[3], 10),
            SubStepProbability = Digit(code[4], 10),
        };

        int Digit(char c, int max)
        {
            var value = c switch
            {
                >= '0' and <= '9' => c - '0',
                'A' => 10,
                _ => -1,
            };
            if (value < 0 || value > max)
                throw new FormatException($"Unexpected character '{c}' in rhythm cell code '{code}'.");
            return value;
        }
    }

    public override string ToString() =>
        $"{(On ? '1' : '0')}{Encode(Velocity)}{Encode((int)SubStep)}{Encode(Probability)}{Encode(SubStepProbability)}";

    private static char Encode(int value) => value switch
    {
        >= 0 and <= 9 => (char)('0' + value),
        10 => 'A',
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Rhythm cell values must be 0–10."),
    };
}

public sealed class RhythmStep
{
    public RhythmCell[] Cells { get; } =
        Enumerable.Range(0, RhythmPattern.TrackCount).Select(_ => new RhythmCell()).ToArray();

    public RhythmCell this[RhythmTrack track] => Cells[(int)track];
}

/// <summary>A rhythm pattern as stored in T8_RHYTHM_PTNbb_nn.PRM.</summary>
public sealed class RhythmPattern
{
    public const int StepCount = 32;
    public const int TrackCount = 8;

    /// <summary>Last step, 1–32.</summary>
    public int Length { get; set; } = 16;

    /// <summary>The SCALE value. Every factory pattern has 1; mapping to 16/32/16t/8t etc. unconfirmed.</summary>
    public int Scale { get; set; } = 1;

    /// <summary>Shuffle amount. Factory patterns use 0–200.</summary>
    public int Shuffle { get; set; }

    /// <summary>Flam amount. Factory patterns use 20 or 36.</summary>
    public int Flam { get; set; } = 36;

    public RhythmStep[] Steps { get; } = Enumerable.Range(0, StepCount).Select(_ => new RhythmStep()).ToArray();

    public static RhythmPattern FromPrm(PrmDocument doc)
    {
        var pattern = new RhythmPattern
        {
            Length = PrmDocument.ParseInt(doc["LENGTH"]),
            Scale = PrmDocument.ParseInt(doc["SCALE"]),
            Shuffle = PrmDocument.ParseInt(doc["SHUFFLE"]),
            Flam = PrmDocument.ParseInt(doc["FLAM"]),
        };

        for (var i = 0; i < StepCount; i++)
        {
            var fields = PrmDocument.ParseFields(doc[$"STEP {i + 1}"]);
            foreach (var track in Enum.GetValues<RhythmTrack>())
                pattern.Steps[i].Cells[(int)track] = RhythmCell.Parse(fields[track.ToString()]);
        }

        return pattern;
    }

    public PrmDocument ToPrm()
    {
        var doc = new PrmDocument();
        doc.Add("LENGTH", Length.ToString());
        doc.Add("SCALE", Scale.ToString());
        doc.Add("SHUFFLE", Shuffle.ToString());
        doc.Add("FLAM", Flam.ToString());
        for (var i = 0; i < StepCount; i++)
        {
            var cells = Enum.GetValues<RhythmTrack>()
                .Select(track => $"{track}={Steps[i][track]}");
            doc.Add($"STEP {i + 1}", string.Join(' ', cells));
        }
        return doc;
    }

    public static RhythmPattern Load(string path) => FromPrm(PrmDocument.Load(path));
    public void Save(string path) => ToPrm().Save(path);
}
