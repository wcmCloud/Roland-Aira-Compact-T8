using Melanchall.DryWetMidi.Multimedia;

namespace T8.Midi.Ports;

/// <param name="Name">Name shown to the user.</param>
/// <param name="Id">Device name on Windows/macOS, device file path on Linux.</param>
public sealed record MidiPortInfo(string Name, string Id);

/// <summary>Finds and opens MIDI output ports on Windows, macOS (DryWetMIDI) and Linux (ALSA raw MIDI).</summary>
public static class MidiPorts
{
    public static IReadOnlyList<MidiPortInfo> ListOutputs()
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

    /// <summary>The first port whose name looks like a T-8, or null.</summary>
    public static MidiPortInfo? FindT8(IEnumerable<MidiPortInfo> ports) =>
        ports.FirstOrDefault(p => LooksLikeT8(p.Name));

    public static bool LooksLikeT8(string name) =>
        name.Contains("T-8", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("T8", StringComparison.OrdinalIgnoreCase);

    public static IOutputDevice OpenOutput(MidiPortInfo port) =>
        OperatingSystem.IsLinux() ? new AlsaRawMidiOutput(port) : OutputDevice.GetByName(port.Id);
}
