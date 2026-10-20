using System.Text.Json;
using System.Text.Json.Serialization;
using T8.Core.Patterns;

namespace T8.Core.Songs;

/// <summary>A pattern to play once as a bridge after a song entry, like the T-8's FILL.</summary>
public sealed record SongFill(int Bank, int Pattern);

/// <summary>One line of a song: a bank and pattern played a number of times, then optionally a fill.</summary>
public sealed class SongEntry
{
    public const int MaxRepeats = 99;

    public int Bank { get; set; } = 1;
    public int Pattern { get; set; } = 1;
    public int Repeats { get; set; } = 1;
    public string Name { get; set; } = "";

    /// <summary>Colour for the song timeline, "#RRGGBB".</summary>
    public string Color { get; set; } = "#FF7A1A";

    /// <summary>A pattern played once after the repeats, leading into the next entry; null for none.</summary>
    public SongFill? Fill { get; set; }

    public SongEntry Clone() => (SongEntry)MemberwiseClone();
}

/// <summary>
/// A song: patterns from the bank played in order, each repeated and optionally followed by a fill.
/// Saved as a ".t8song" JSON file (see docs/SONG-FORMAT.md).
/// </summary>
public sealed class Song
{
    public const string FileExtension = ".t8song";
    public const string FormatName = "t8song";
    public const int FormatVersion = 1;

    public string Title { get; set; } = "Untitled song";

    /// <summary>Tempo saved with the song; null keeps the editor's tempo.</summary>
    public double? Bpm { get; set; }

    /// <summary>Start again from the first entry after the last one.</summary>
    public bool Loop { get; set; }

    public List<SongEntry> Entries { get; set; } = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The file as written to disk.</summary>
    private sealed record SongFile(
        string Format,
        int Version,
        string Title,
        double? Bpm,
        bool Loop,
        List<SongEntry> Entries);

    public string ToJson() => JsonSerializer.Serialize(new SongFile(FormatName, FormatVersion, Title, Bpm, Loop, Entries), Json);

    public void Save(string path) => File.WriteAllText(path, ToJson());

    public static Song Load(string path) => FromJson(File.ReadAllText(path));

    /// <summary>Reads a song file; throws <see cref="FormatException"/> with a readable message if it isn't valid.</summary>
    public static Song FromJson(string json)
    {
        SongFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SongFile>(json, Json);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Not a song file: {ex.Message}", ex);
        }
        if (file is null || file.Format != FormatName)
            throw new FormatException("Not a T-8 song file (missing \"format\": \"t8song\").");
        if (file.Version > FormatVersion)
            throw new FormatException($"This song file is version {file.Version}; this editor reads up to version {FormatVersion}.");

        var song = new Song { Title = file.Title ?? "", Bpm = file.Bpm, Loop = file.Loop, Entries = file.Entries ?? new() };
        var errors = song.Validate();
        if (errors.Count > 0)
            throw new FormatException(string.Join(" ", errors));
        return song;
    }

    /// <summary>Problems that would stop the song from playing, e.g. "Entry 2: pattern must be 1–16."</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        for (var i = 0; i < Entries.Count; i++)
        {
            var entry = Entries[i];
            var label = $"Entry {i + 1}:";
            CheckSlot(entry.Bank, entry.Pattern, $"{label}");
            if (entry.Repeats is < 1 or > SongEntry.MaxRepeats)
                errors.Add($"{label} repeats must be 1–{SongEntry.MaxRepeats}.");
            if (entry.Fill is { } fill)
                CheckSlot(fill.Bank, fill.Pattern, $"{label} fill");
            if (entry.Color is not { Length: 7 } color || color[0] != '#' || !int.TryParse(color[1..], System.Globalization.NumberStyles.HexNumber, null, out _))
                errors.Add($"{label} colour must look like #FF7A1A.");
        }
        if (Bpm is { } bpm && bpm is < 20 or > 300)
            errors.Add("BPM must be 20–300.");
        return errors;

        void CheckSlot(int bank, int pattern, string label)
        {
            if (bank is < 1 or > PatternLibrary.BankCount)
                errors.Add($"{label} bank must be 1–{PatternLibrary.BankCount}.");
            if (pattern is < 1 or > PatternLibrary.PatternsPerBank)
                errors.Add($"{label} pattern must be 1–{PatternLibrary.PatternsPerBank}.");
        }
    }
}
