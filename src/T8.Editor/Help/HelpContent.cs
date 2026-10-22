using System.Reflection;
using System.Text.RegularExpressions;

namespace T8.Editor.Help;

/// <summary>A Help window topic, from docs/help/&lt;id&gt;.md.</summary>
public sealed record HelpTopic(string Id, string Title, string Markdown);

/// <summary>A (?) popup, from docs/help/tips/&lt;id&gt;.md: a short text and the topic its link opens.</summary>
public sealed record HelpPopup(string Id, string Title, string TopicId, string Markdown);

/// <summary>Topic ids: the file names in docs/help, in the order the Help window lists them.</summary>
public static class HelpTopics
{
    public const string GettingStarted = "getting-started";
    public const string DriveMode = "drive-mode";
    public const string RhythmEditing = "rhythm-editing";
    public const string RhythmGenerator = "rhythm-generator";
    public const string Fills = "fills";
    public const string BassEditing = "bass-editing";
    public const string BassGenerator = "bass-generator";
    public const string Song = "song";
    public const string MidiSettings = "midi-settings";
    public const string Shortcuts = "shortcuts";
    public const string About = "about";

    public static IReadOnlyList<string> Order { get; } =
        [GettingStarted, DriveMode, RhythmEditing, RhythmGenerator, Fills, BassEditing, BassGenerator, Song, MidiSettings, Shortcuts, About];

    /// <summary>The topic F1 opens on each main window tab (Rhythm, Bass, Song).</summary>
    public static string ForTab(int tab) => tab switch
    {
        0 => RhythmEditing,
        1 => BassEditing,
        2 => Song,
        _ => GettingStarted,
    };
}

/// <summary>Popup ids: the file names in docs/help/tips.</summary>
public static class HelpTips
{
    public const string Patterns = "patterns";
    public const string Playback = "playback";
    public const string RhythmGrid = "rhythm-grid";
    public const string PatternKnobs = "pattern-knobs";
    public const string StepPanel = "step-panel";
    public const string RhythmGenerator = "rhythm-generator";
    public const string Fills = "fills";
    public const string BassPattern = "bass-pattern";
    public const string BassStepEditor = "bass-step-editor";
    public const string BassGenerator = "bass-generator";
    public const string SongHeader = "song-header";
    public const string Arrangement = "arrangement";
    public const string SongPlayback = "song-playback";
    public const string CopyToBank = "copy-to-bank";
    public const string Connection = "connection";
    public const string MidiChannels = "midi-channels";
    public const string DrumNotes = "drum-notes";
    public const string PlayButton = "play-button";
    public const string OnTheT8 = "on-the-t8";
    public const string DriveMode = "drive-mode";
}

/// <summary>Loads the help Markdown embedded in the app (one source of truth: the files in docs/help).</summary>
public static partial class HelpContent
{
    private const string TopicPrefix = "help.topic.";
    private const string TipPrefix = "help.tip.";

    private static readonly Lazy<IReadOnlyList<HelpTopic>> TopicList = new(LoadTopics);
    private static readonly Lazy<IReadOnlyDictionary<string, HelpPopup>> TipMap = new(LoadTips);

    /// <summary>Every topic, in <see cref="HelpTopics.Order"/>, then any others by name.</summary>
    public static IReadOnlyList<HelpTopic> Topics => TopicList.Value;

    public static IReadOnlyDictionary<string, HelpPopup> Tips => TipMap.Value;

    public static HelpTopic Topic(string? id) => Topics.FirstOrDefault(t => t.Id == id) ?? Topics[0];

    public static HelpPopup? Tip(string? id) => id is not null && Tips.TryGetValue(id, out var tip) ? tip : null;

    /// <summary>Topics whose title or text contains every word of the query (all topics for an empty query).</summary>
    public static IReadOnlyList<HelpTopic> Search(string? query)
    {
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Topics.Where(t => words.All(w =>
            t.Title.Contains(w, StringComparison.OrdinalIgnoreCase) || t.Markdown.Contains(w, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private static IReadOnlyList<HelpTopic> LoadTopics()
    {
        var topics = Resources(TopicPrefix).Select(r =>
        {
            var (title, _, body) = Parse(r.Text);
            return new HelpTopic(r.Id, title, body);
        }).ToList();
        return topics
            .OrderBy(t => HelpTopics.Order.IndexOf(t.Id) is var i and >= 0 ? i : int.MaxValue)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyDictionary<string, HelpPopup> LoadTips() =>
        Resources(TipPrefix).ToDictionary(r => r.Id, r =>
        {
            var (title, topic, body) = Parse(r.Text);
            return new HelpPopup(r.Id, title, topic ?? HelpTopics.GettingStarted, body);
        });

    private static IEnumerable<(string Id, string Text)> Resources(string prefix)
    {
        var assembly = typeof(HelpContent).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            yield return (name[prefix.Length..^".md".Length], reader.ReadToEnd().Replace("\r\n", "\n"));
        }
    }

    /// <summary>"# Title", an optional "&lt;!-- topic: id --&gt;" line, then the body.</summary>
    private static (string Title, string? Topic, string Body) Parse(string text)
    {
        var lines = text.Split('\n').ToList();
        var title = lines.Count > 0 && lines[0].StartsWith("# ") ? lines[0][2..].Trim() : "";
        if (title.Length > 0)
            lines.RemoveAt(0);
        string? topic = null;
        if (lines.Count > 0 && TopicLine().Match(lines[0]) is { Success: true } match)
        {
            topic = match.Groups[1].Value;
            lines.RemoveAt(0);
        }
        return (title, topic, string.Join('\n', lines).Trim());
    }

    [GeneratedRegex(@"^<!--\s*topic:\s*([\w-]+)\s*-->$")]
    private static partial Regex TopicLine();
}

file static class ListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == value)
                return i;
        return -1;
    }
}
