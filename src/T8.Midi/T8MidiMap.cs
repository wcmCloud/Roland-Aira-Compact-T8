using T8.Core.Patterns;

namespace T8.Midi;

/// <summary>
/// MIDI channels and note numbers from the T-8 MIDI Implementation Chart (manual p.31).
/// Channels are 1-based, as shown on the T-8 (menu items rhCh, bACh, Pc.Ch).
/// </summary>
public sealed class T8MidiMap
{
    public int BassChannel { get; set; } = 2;
    public int RhythmChannel { get; set; } = 10;
    public int ProgramChangeChannel { get; set; } = 16;

    /// <summary>
    /// Note sent for each rhythm track. The file's track names (LT/HT/CY) are TR-style slots; the
    /// mapping LT = tom and HT = hand clap is inferred, and CY has no matching T-8 instrument.
    /// </summary>
    public Dictionary<RhythmTrack, int> RhythmNotes { get; set; } = DefaultRhythmNotes();

    /// <summary>Tracks the T-8 can play, in the order its instrument buttons appear.</summary>
    public static IReadOnlyList<RhythmTrack> PlayableTracks { get; } =
        [RhythmTrack.BD, RhythmTrack.SD, RhythmTrack.LT, RhythmTrack.HT, RhythmTrack.CH, RhythmTrack.OH];

    public static Dictionary<RhythmTrack, int> DefaultRhythmNotes() => new()
    {
        [RhythmTrack.BD] = 36,
        [RhythmTrack.SD] = 38,
        [RhythmTrack.LT] = 47, // TOM
        [RhythmTrack.HT] = 50, // HAND CLAP
        [RhythmTrack.CH] = 42,
        [RhythmTrack.OH] = 46,
    };

    public T8MidiMap Clone() => new()
    {
        BassChannel = BassChannel,
        RhythmChannel = RhythmChannel,
        ProgramChangeChannel = ProgramChangeChannel,
        RhythmNotes = new Dictionary<RhythmTrack, int>(RhythmNotes),
    };

    /// <summary>Problems that would stop the map from working, e.g. "Bass channel must be 1–16." Empty when valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        CheckChannel(BassChannel, "Bass");
        CheckChannel(RhythmChannel, "Rhythm");
        CheckChannel(ProgramChangeChannel, "Program change");
        foreach (var (track, note) in RhythmNotes)
        {
            if (note is < 0 or > 127)
                errors.Add($"{track} note must be 0–127.");
        }
        if (BassChannel == RhythmChannel)
            errors.Add("Bass and rhythm need different channels.");
        return errors;

        void CheckChannel(int channel, string name)
        {
            if (channel is < 1 or > 16)
                errors.Add($"{name} channel must be 1–16.");
        }
    }

    /// <summary>Program number 0–63 for a 1-based bank (1–4) and pattern (1–16).</summary>
    public static int ProgramNumber(int bank, int pattern)
    {
        if (bank is < 1 or > PatternLibrary.BankCount)
            throw new ArgumentOutOfRangeException(nameof(bank), bank, "Bank must be 1–4.");
        if (pattern is < 1 or > PatternLibrary.PatternsPerBank)
            throw new ArgumentOutOfRangeException(nameof(pattern), pattern, "Pattern must be 1–16.");
        return (bank - 1) * PatternLibrary.PatternsPerBank + (pattern - 1);
    }
}
