using Melanchall.DryWetMidi.Multimedia;

namespace T8.Midi.Ports;

/// <param name="Name">Name shown to the user.</param>
/// <param name="Id">Device name on Windows/macOS, device file path on Linux.</param>
public sealed record MidiPortInfo(string Name, string Id);

/// <summary>Lists and opens MIDI outputs. Swappable so the app and CLI can be tested without hardware.</summary>
public interface IMidiPortProvider
{
    IReadOnlyList<MidiPortInfo> ListOutputs();
    IOutputDevice OpenOutput(MidiPortInfo port);
}

/// <summary>Real MIDI ports: DryWetMIDI on Windows and macOS, ALSA raw MIDI on Linux.</summary>
public sealed class SystemMidiPorts : IMidiPortProvider
{
    public IReadOnlyList<MidiPortInfo> ListOutputs()
    {
        if (OperatingSystem.IsLinux())
            return AlsaRawMidiOutput.List();

        return OutputDevice.GetAll()
            .Select(device =>
            {
                using (device)
                    return new MidiPortInfo(device.Name, device.Name);
            })
            .ToList();
    }

    public IOutputDevice OpenOutput(MidiPortInfo port) =>
        OperatingSystem.IsLinux() ? new AlsaRawMidiOutput(port) : OutputDevice.GetByName(port.Id);
}

public static class MidiPorts
{
    public static IMidiPortProvider System { get; } = new SystemMidiPorts();

    /// <summary>The first port whose name looks like a T-8, or null.</summary>
    public static MidiPortInfo? FindT8(IEnumerable<MidiPortInfo> ports) =>
        ports.FirstOrDefault(p => LooksLikeT8(p.Name));

    public static bool LooksLikeT8(string name) =>
        name.Contains("T-8", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("T8", StringComparison.OrdinalIgnoreCase);
}
