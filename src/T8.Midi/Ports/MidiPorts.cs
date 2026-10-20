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

    IReadOnlyList<MidiPortInfo> ListInputs();
    IInputDevice OpenInput(MidiPortInfo port);
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

    public IReadOnlyList<MidiPortInfo> ListInputs()
    {
        // ALSA raw MIDI device files are both input and output.
        if (OperatingSystem.IsLinux())
            return AlsaRawMidiOutput.List();

        return InputDevice.GetAll()
            .Select(device =>
            {
                using (device)
                    return new MidiPortInfo(device.Name, device.Name);
            })
            .ToList();
    }

    public IInputDevice OpenInput(MidiPortInfo port) =>
        OperatingSystem.IsLinux() ? new AlsaRawMidiInput(port) : InputDevice.GetByName(port.Id);
}

public static class MidiPorts
{
    public static IMidiPortProvider System { get; } = new SystemMidiPorts();

    /// <summary>The first port whose name looks like a T-8, or null.</summary>
    public static MidiPortInfo? FindT8(IEnumerable<MidiPortInfo> ports) =>
        ports.FirstOrDefault(p => LooksLikeT8(p.Name));

    /// <summary>The input that belongs to an output: same name first, otherwise any T-8 input.</summary>
    public static MidiPortInfo? FindMatchingInput(IEnumerable<MidiPortInfo> inputs, MidiPortInfo output)
    {
        var list = inputs.ToList();
        return list.FirstOrDefault(p => p.Name == output.Name) ?? FindT8(list);
    }

    public static bool LooksLikeT8(string name) =>
        name.Contains("T-8", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("T8", StringComparison.OrdinalIgnoreCase);
}
