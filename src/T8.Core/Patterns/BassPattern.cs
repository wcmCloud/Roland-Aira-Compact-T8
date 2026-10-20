using T8.Core.Prm;

namespace T8.Core.Patterns;

public enum BassStepState
{
    Off = 0,
    Note = 1,
    Tie = 2,
}

public sealed class BassStep
{
    public BassStepState State { get; set; } = BassStepState.Off;

    /// <summary>MIDI note number. The T-8 range is C0–C7; factory patterns use 12–72.</summary>
    public int Note { get; set; } = 36;

    public bool Accent { get; set; }
    public bool Slide { get; set; }

    public BassStep Clone() => (BassStep)MemberwiseClone();
}

/// <summary>A bass pattern as stored in T8_BASS_PTNbb_nn.PRM.</summary>
public sealed class BassPattern
{
    public const int StepCount = 32;

    /// <summary>Last step, 1–32.</summary>
    public int Length { get; set; } = 16;

    /// <summary>The TRIPLET flag. Every factory pattern has 0; meaning of other values unconfirmed.</summary>
    public int Triplet { get; set; }

    public BassStep[] Steps { get; } = Enumerable.Range(0, StepCount).Select(_ => new BassStep()).ToArray();

    public static BassPattern FromPrm(PrmDocument doc)
    {
        var pattern = new BassPattern
        {
            Length = PrmDocument.ParseInt(doc["LENGTH"]),
            Triplet = PrmDocument.ParseInt(doc["TRIPLET"]),
        };

        for (var i = 0; i < StepCount; i++)
        {
            var fields = PrmDocument.ParseFields(doc[$"STEP {i + 1}"]);
            var step = pattern.Steps[i];
            var state = PrmDocument.ParseInt(fields["STATE"]);
            if (!Enum.IsDefined(typeof(BassStepState), state))
                throw new FormatException($"Unknown bass step STATE={state} at step {i + 1}.");
            step.State = (BassStepState)state;
            step.Note = PrmDocument.ParseInt(fields["NOTE"]);
            step.Accent = PrmDocument.ParseInt(fields["ACCENT"]) != 0;
            step.Slide = PrmDocument.ParseInt(fields["SLIDE"]) != 0;
        }

        return pattern;
    }

    public PrmDocument ToPrm()
    {
        var doc = new PrmDocument();
        doc.Add("LENGTH", Length.ToString());
        doc.Add("TRIPLET", Triplet.ToString());
        for (var i = 0; i < StepCount; i++)
        {
            var s = Steps[i];
            doc.Add($"STEP {i + 1}",
                $"STATE={(int)s.State} NOTE={s.Note} ACCENT={(s.Accent ? 1 : 0)} SLIDE={(s.Slide ? 1 : 0)}");
        }
        return doc;
    }

    public BassPattern Clone() => FromPrm(ToPrm());

    /// <summary>Resets every step to the blank state the T-8 writes for an empty pattern.</summary>
    public void Clear()
    {
        Length = 16;
        Triplet = 0;
        for (var i = 0; i < StepCount; i++)
            Steps[i] = new BassStep();
    }

    public static BassPattern Load(string path) => FromPrm(PrmDocument.Load(path));
    public void Save(string path) => ToPrm().Save(path);
}
