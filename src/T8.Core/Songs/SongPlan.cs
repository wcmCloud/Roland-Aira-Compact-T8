using T8.Core.Patterns;

namespace T8.Core.Songs;

/// <summary>One pass of one pattern in a song.</summary>
/// <param name="Entry">Index of the song entry this pass belongs to.</param>
/// <param name="Repeat">1-based repeat of the entry; 0 for the entry's fill.</param>
/// <param name="StartStep">First 16th-note step of the pass, counted from the start of the song.</param>
/// <param name="Steps">Length of the pass: the pattern's rhythm length, as the T-8 changes pattern when its rhythm ends.</param>
public sealed record SongPass(int Entry, int Repeat, int Bank, int Pattern, int StartStep, int Steps)
{
    public bool IsFill => Repeat == 0;
    public int EndStep => StartStep + Steps;
}

/// <summary>
/// A song laid out in time: every repeat of every entry, and every fill, as consecutive passes. The
/// pattern lengths come from the library.
/// </summary>
public sealed class SongPlan
{
    private SongPlan(IReadOnlyList<SongPass> passes) => Passes = passes;

    public IReadOnlyList<SongPass> Passes { get; }

    public int TotalSteps => Passes.Count == 0 ? 0 : Passes[^1].EndStep;

    public static SongPlan Create(Song song, PatternLibrary library, int? onlyEntry = null)
    {
        var passes = new List<SongPass>();
        var step = 0;
        for (var i = 0; i < song.Entries.Count; i++)
        {
            if (onlyEntry is { } only && only != i)
                continue;
            var entry = song.Entries[i];
            var steps = Length(library, entry.Bank, entry.Pattern);
            for (var r = 1; r <= Math.Max(1, entry.Repeats); r++)
            {
                passes.Add(new SongPass(i, r, entry.Bank, entry.Pattern, step, steps));
                step += steps;
            }
            if (entry.Fill is { } fill)
            {
                var fillSteps = Length(library, fill.Bank, fill.Pattern);
                passes.Add(new SongPass(i, 0, fill.Bank, fill.Pattern, step, fillSteps));
                step += fillSteps;
            }
        }
        return new SongPlan(passes);
    }

    /// <summary>The steps one pass of a pattern takes: its rhythm length, as the T-8 changes pattern at the end of the rhythm.</summary>
    public static int Length(PatternLibrary library, int bank, int pattern) =>
        Math.Max(1, library.Rhythm[bank - 1, pattern - 1].Length);

    /// <summary>Index of the pass playing at a step (wrapping round the song), or -1 for an empty song.</summary>
    public int PassAt(long step)
    {
        if (Passes.Count == 0)
            return -1;
        var s = step % TotalSteps;
        for (var i = 0; i < Passes.Count; i++)
        {
            if (s < Passes[i].EndStep)
                return i;
        }
        return Passes.Count - 1;
    }

    /// <summary>Index of the first pass of a song entry, or -1 if the entry isn't in the plan.</summary>
    public int FirstPassOf(int entry)
    {
        for (var i = 0; i < Passes.Count; i++)
        {
            if (Passes[i].Entry == entry)
                return i;
        }
        return -1;
    }
}
