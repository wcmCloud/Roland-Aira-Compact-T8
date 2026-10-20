using T8.Core.Patterns;

namespace T8.Core.Songs;

/// <summary>
/// Copies the patterns of a song into one bank, in song order: entry 1 to pattern 1, entry 2 to
/// pattern 2 and so on, then each distinct fill pattern, and blank patterns for the rest. All the
/// source patterns are copied first, so a song may use patterns of the very bank it replaces.
/// </summary>
public static class SongBankWriter
{
    /// <summary>How many slots the song needs: one per entry plus one per distinct fill.</summary>
    public static int SlotsNeeded(Song song) =>
        song.Entries.Count + DistinctFills(song).Count;

    /// <summary>Why the song can't be copied to a bank, or null if it can.</summary>
    public static string? Problem(Song song)
    {
        if (song.Entries.Count == 0)
            return "The song has no patterns to copy.";
        var needed = SlotsNeeded(song);
        if (needed > PatternLibrary.PatternsPerBank)
        {
            var fills = DistinctFills(song).Count;
            return fills > 0
                ? $"A bank holds {PatternLibrary.PatternsPerBank} patterns; this song needs {needed} ({song.Entries.Count} entries and {fills} fills)."
                : $"A bank holds {PatternLibrary.PatternsPerBank} patterns; this song has {song.Entries.Count} entries.";
        }
        return null;
    }

    /// <summary>
    /// Writes the song's patterns into <paramref name="bank"/> and points the song's entries and
    /// fills at the copies, so it plays the same as before.
    /// </summary>
    public static void CopyToBank(Song song, PatternLibrary library, int bank)
    {
        if (bank is < 1 or > PatternLibrary.BankCount)
            throw new ArgumentOutOfRangeException(nameof(bank), bank, "Bank must be 1–4.");
        if (Problem(song) is { } problem)
            throw new InvalidOperationException(problem);

        // Take copies of every source pattern before anything in the bank is replaced.
        var entryCopies = song.Entries.Select(e => Copy(library, e.Bank, e.Pattern)).ToList();
        var fills = DistinctFills(song);
        var fillCopies = fills.Select(f => Copy(library, f.Bank, f.Pattern)).ToList();

        var b = bank - 1;
        var slot = 0;
        foreach (var (bass, rhythm) in entryCopies.Concat(fillCopies))
        {
            library.Bass[b, slot] = bass;
            library.Rhythm[b, slot] = rhythm;
            slot++;
        }
        for (; slot < PatternLibrary.PatternsPerBank; slot++)
        {
            library.Bass[b, slot] = new BassPattern();
            library.Rhythm[b, slot] = new RhythmPattern();
        }

        // Point the song at the copies.
        for (var i = 0; i < song.Entries.Count; i++)
        {
            var entry = song.Entries[i];
            if (entry.Fill is { } fill)
                entry.Fill = new SongFill(bank, song.Entries.Count + fills.IndexOf(fill) + 1);
            entry.Bank = bank;
            entry.Pattern = i + 1;
        }
    }

    private static List<SongFill> DistinctFills(Song song) =>
        song.Entries.Where(e => e.Fill is not null).Select(e => e.Fill!).Distinct().ToList();

    private static (BassPattern Bass, RhythmPattern Rhythm) Copy(PatternLibrary library, int bank, int pattern) =>
        (library.Bass[bank - 1, pattern - 1].Clone(), library.Rhythm[bank - 1, pattern - 1].Clone());
}
