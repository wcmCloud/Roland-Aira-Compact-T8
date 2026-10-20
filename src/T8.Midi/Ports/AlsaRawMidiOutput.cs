using System.Text.RegularExpressions;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace T8.Midi.Ports;

/// <summary>
/// Linux output through ALSA raw MIDI device files (/dev/snd/midiC*D*). DryWetMIDI's device API
/// only supports Windows and macOS, so on Linux we write the MIDI bytes ourselves.
/// </summary>
public sealed partial class AlsaRawMidiOutput : IOutputDevice, IDisposable
{
    private readonly FileStream _stream;
    private readonly MidiEventToBytesConverter _converter = new() { UseRunningStatus = false };
    private readonly Lock _lock = new();

    public AlsaRawMidiOutput(MidiPortInfo port)
    {
        Port = port;
        _stream = new FileStream(port.Id, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, bufferSize: 0);
    }

    public MidiPortInfo Port { get; }

    public event EventHandler<MidiEventSentEventArgs>? EventSent;

    public void PrepareForEventsSending()
    {
    }

    public void SendEvent(MidiEvent midiEvent)
    {
        var bytes = _converter.Convert(midiEvent);
        lock (_lock)
        {
            _stream.Write(bytes);
            _stream.Flush();
        }
        EventSent?.Invoke(this, new MidiEventSentEventArgs(midiEvent));
    }

    public void Dispose() => _stream.Dispose();

    /// <summary>Lists raw MIDI devices, named from /proc/asound/cards (e.g. "T-8").</summary>
    public static IReadOnlyList<MidiPortInfo> List(string devRoot = "/dev/snd", string cardsFile = "/proc/asound/cards")
    {
        if (!Directory.Exists(devRoot))
            return [];

        var cardNames = File.Exists(cardsFile) ? ParseCardNames(File.ReadAllText(cardsFile)) : new Dictionary<int, string>();
        var ports = new List<MidiPortInfo>();
        foreach (var path in Directory.GetFiles(devRoot, "midiC*D*").Order())
        {
            var match = DeviceFileRegex().Match(Path.GetFileName(path));
            if (!match.Success)
                continue;
            var card = int.Parse(match.Groups[1].Value);
            var device = int.Parse(match.Groups[2].Value);
            var name = cardNames.TryGetValue(card, out var cardName) ? cardName : $"Card {card}";
            ports.Add(new MidiPortInfo(device == 0 ? name : $"{name} ({device})", path));
        }
        return ports;
    }

    /// <summary>Parses lines like " 1 [T8             ]: USB-Audio - T-8".</summary>
    internal static Dictionary<int, string> ParseCardNames(string text)
    {
        var names = new Dictionary<int, string>();
        foreach (Match match in CardLineRegex().Matches(text))
            names[int.Parse(match.Groups[1].Value)] = match.Groups[2].Value.Trim();
        return names;
    }

    [GeneratedRegex(@"^midiC(\d+)D(\d+)$")]
    private static partial Regex DeviceFileRegex();

    [GeneratedRegex(@"^\s*(\d+)\s+\[[^\]]*\]:\s*.+?\s-\s(.+)$", RegexOptions.Multiline)]
    private static partial Regex CardLineRegex();
}
