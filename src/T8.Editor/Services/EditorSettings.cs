using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary>Send a program change whenever a pattern is picked in the editor.</summary>
    public bool FollowSelection { get; set; }

    public bool AuditionEnabled { get; set; } = true;
    public double Bpm { get; set; } = 120;
    public string? LastFolder { get; set; }

    public EditorSettings Clone() => new()
    {
        OutputPortName = OutputPortName,
        Midi = Midi.Clone(),
        PlayMode = PlayMode,
        FollowSelection = FollowSelection,
        AuditionEnabled = AuditionEnabled,
        Bpm = Bpm,
        LastFolder = LastFolder,
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
