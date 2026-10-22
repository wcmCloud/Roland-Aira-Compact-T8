using System.Text.Json;
using System.Text.Json.Serialization;
using T8.Core.Patterns;
using T8.Midi;

namespace T8.Editor.Services;

public enum PlayMode
{
    /// <summary>The computer plays the edited pattern as notes through the T-8's sounds.</summary>
    PreviewNotes,

    /// <summary>The computer is the clock master: selects the pattern on the T-8 and runs its own sequencer.</summary>
    SyncT8Sequencer,
}

/// <summary>Everything the editor remembers between runs.</summary>
public sealed class EditorSettings
{
    public string? OutputPortName { get; set; }
    public T8MidiMap Midi { get; set; } = new();
    public PlayMode PlayMode { get; set; } = PlayMode.PreviewNotes;

    /// <summary>Send a program change whenever a pattern is picked in the editor (on by default; needs rXPc on the T-8).</summary>
    public bool FollowSelection { get; set; } = true;

    /// <summary>
    /// Listen to the T-8: switch to the pattern picked on it (it sends a program change when tXPc is
    /// on) and show the playing step when its own sequencer runs.
    /// </summary>
    public bool FollowT8 { get; set; } = true;

    public bool AuditionEnabled { get; set; } = true;

    /// <summary>Highlight the step being played, and the bass note on the keyboard.</summary>
    public bool ShowPlayhead { get; set; } = true;

    /// <summary>Last settings used in the bass line generator.</summary>
    public BassGeneratorOptions BassGenerator { get; set; } = new();

    /// <summary>Last settings used in the rhythm generator.</summary>
    public RhythmGeneratorOptions RhythmGenerator { get; set; } = new();
    public double Bpm { get; set; } = 120;
    public string? LastFolder { get; set; }

    /// <summary>On the Bass tab, enter the notes played on the T-8's own keyboard into the selected step.</summary>
    public bool NotesFromT8 { get; set; } = true;

    /// <summary>Ask GitHub for a newer release when the editor starts (at most once a day).</summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>The last answer from the update check.</summary>
    public UpdateCheckCache UpdateCheck { get; set; } = new();

    public EditorSettings Clone() => new()
    {
        OutputPortName = OutputPortName,
        Midi = Midi.Clone(),
        PlayMode = PlayMode,
        FollowSelection = FollowSelection,
        FollowT8 = FollowT8,
        AuditionEnabled = AuditionEnabled,
        ShowPlayhead = ShowPlayhead,
        BassGenerator = BassGenerator.Clone(),
        RhythmGenerator = RhythmGenerator.Clone(),
        Bpm = Bpm,
        LastFolder = LastFolder,
        CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
        NotesFromT8 = NotesFromT8,
        UpdateCheck = UpdateCheck.Clone(),
    };
}

public interface ISettingsStore
{
    EditorSettings Load();
    void Save(EditorSettings settings);
}

/// <summary>Settings as JSON, by default in the user's app data folder (T8Editor/settings.json).</summary>
public sealed class JsonSettingsStore(string? path = null) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "T8Editor", "settings.json");

    /// <summary>Loads settings; a missing, unreadable or invalid file gives the defaults.</summary>
    public EditorSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
                return new EditorSettings();
            var settings = JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(Path), Options) ?? new EditorSettings();
            if (settings.Midi.Validate().Count > 0)
                settings.Midi = new T8MidiMap();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new EditorSettings();
        }
    }

    public void Save(EditorSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(settings, Options));
    }
}
