namespace T8.Core.Patterns;

/// <summary>
/// The full set of patterns in a T-8 BACKUP or RESTORE folder:
/// 4 banks × 16 patterns, each with a bass and a rhythm part.
/// </summary>
public sealed class PatternLibrary
{
    public const int BankCount = 4;
    public const int PatternsPerBank = 16;

    public BassPattern[,] Bass { get; } = new BassPattern[BankCount, PatternsPerBank];
    public RhythmPattern[,] Rhythm { get; } = new RhythmPattern[BankCount, PatternsPerBank];

    public static string BassPath(string root, int bank, int pattern) =>
        Path.Combine(root, "BASS", $"PTN_BANK{bank:00}", $"T8_BASS_PTN{bank:00}_{pattern:00}.PRM");

    public static string RhythmPath(string root, int bank, int pattern) =>
        Path.Combine(root, "RHYTHM", $"PTN_BANK{bank:00}", $"T8_RHYTHM_PTN{bank:00}_{pattern:00}.PRM");

    /// <summary>A library of 64 blank patterns.</summary>
    public static PatternLibrary CreateBlank()
    {
        var library = new PatternLibrary();
        for (var b = 0; b < BankCount; b++)
        {
            for (var p = 0; p < PatternsPerBank; p++)
            {
                library.Bass[b, p] = new BassPattern();
                library.Rhythm[b, p] = new RhythmPattern();
            }
        }
        return library;
    }

    /// <summary>
    /// Finds the folder holding BASS and RHYTHM: the folder itself, or its BACKUP subfolder
    /// (so the T-8 drive root can be picked directly). Null if neither has them.
    /// </summary>
    public static string? ResolveRoot(string folder)
    {
        foreach (var candidate in new[] { folder, Path.Combine(folder, "BACKUP") })
        {
            if (Directory.Exists(Path.Combine(candidate, "BASS")) && Directory.Exists(Path.Combine(candidate, "RHYTHM")))
                return candidate;
        }
        return null;
    }

    /// <summary>Loads from a folder containing BASS and RHYTHM subfolders. Bank and pattern numbers are 1-based in file names.</summary>
    public static PatternLibrary Load(string root)
    {
        var library = new PatternLibrary();
        for (var b = 0; b < BankCount; b++)
        {
            for (var p = 0; p < PatternsPerBank; p++)
            {
                library.Bass[b, p] = BassPattern.Load(BassPath(root, b + 1, p + 1));
                library.Rhythm[b, p] = RhythmPattern.Load(RhythmPath(root, b + 1, p + 1));
            }
        }
        return library;
    }

    public void Save(string root)
    {
        for (var b = 0; b < BankCount; b++)
        {
            for (var p = 0; p < PatternsPerBank; p++)
            {
                var bassPath = BassPath(root, b + 1, p + 1);
                var rhythmPath = RhythmPath(root, b + 1, p + 1);
                Directory.CreateDirectory(Path.GetDirectoryName(bassPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(rhythmPath)!);
                Bass[b, p].Save(bassPath);
                Rhythm[b, p].Save(rhythmPath);
            }
        }
    }
}
