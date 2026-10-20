using System.Text;

namespace T8.Core.Prm;

/// <summary>
/// Low-level view of a T-8 .PRM file: an ordered list of "KEY\t= VALUE" lines.
/// </summary>
public sealed class PrmDocument
{
    public List<KeyValuePair<string, string>> Entries { get; } = new();

    public string this[string key]
    {
        get
        {
            foreach (var entry in Entries)
            {
                if (entry.Key == key)
                    return entry.Value;
            }
            throw new KeyNotFoundException($"PRM key '{key}' not found.");
        }
    }

    public void Add(string key, string value) => Entries.Add(new(key, value));

    public static PrmDocument Parse(string text)
    {
        var doc = new PrmDocument();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        foreach (var raw in lines)
        {
            if (raw.Length == 0)
                continue;

            var separator = raw.IndexOf('=');
            if (separator < 0)
                throw new FormatException($"Malformed PRM line: '{raw}'");

            var key = raw[..separator].TrimEnd('\t', ' ');
            var value = raw[(separator + 1)..].Trim();
            doc.Add(key, value);
        }
        return doc;
    }

    public static PrmDocument Load(string path) => Parse(File.ReadAllText(path, Encoding.ASCII));

    /// <summary>Writes the document exactly as the T-8 does: "KEY\t= VALUE\n" per line, LF endings.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in Entries)
            sb.Append(key).Append("\t= ").Append(value).Append('\n');
        return sb.ToString();
    }

    public void Save(string path) => File.WriteAllText(path, ToString(), Encoding.ASCII);

    /// <summary>Splits a value like "STATE=1 NOTE=46 ACCENT=1" into its fields.</summary>
    public static Dictionary<string, string> ParseFields(string value)
    {
        var fields = new Dictionary<string, string>();
        foreach (var part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator < 0)
                throw new FormatException($"Malformed PRM field: '{part}'");
            fields[part[..separator]] = part[(separator + 1)..];
        }
        return fields;
    }

    public static int ParseInt(string value) =>
        int.TryParse(value, out var result) ? result : throw new FormatException($"Expected a number, got '{value}'.");
}
