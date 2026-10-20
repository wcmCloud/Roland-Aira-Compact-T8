using T8.Core.Patterns;
using T8.Midi;
using T8.Midi.Ports;

// Small command-line tool for trying the T-8 MIDI connection before the editor UI exists.

const string Usage = """
    Usage: t8 <command> [options]

      ports                                   List MIDI outputs
      select <bank> <pattern>                 Switch the T-8 to a pattern (needs rXPc on)
      preview <folder> <bank> <pattern>       Play a .PRM pattern through the T-8's sounds
              [--bpm 120] [--part both|bass|rhythm]
      clock [--bpm 120]                       Start the T-8's own sequencer at a tempo (needs SYnC AUTO/USB)
      export <folder> <bank> <pattern> <file.mid> [--bpm 120]
                                              Save a pattern as a MIDI file

      <folder> is a BACKUP or RESTORE folder with BASS and RHYTHM inside.
      Add --port "<name>" to use a port other than the auto-detected T-8.
    """;

if (args.Length == 0)
{
    Console.WriteLine(Usage);
    return 1;
}

try
{
    return args[0] switch
    {
        "ports" => ListPorts(),
        "select" => Select(),
        "preview" => Preview(),
        "clock" => RunClock(),
        "export" => Export(),
        _ => Fail($"Unknown command '{args[0]}'.\n\n{Usage}"),
    };
}
catch (Exception ex)
{
    return Fail(ex.Message);
}

int ListPorts()
{
    var ports = MidiPorts.ListOutputs();
    if (ports.Count == 0)
        Console.WriteLine("No MIDI outputs found.");
    foreach (var port in ports)
        Console.WriteLine($"{(MidiPorts.LooksLikeT8(port.Name) ? "*" : " ")} {port.Name}");
    return 0;
}

int Select()
{
    using var t8 = Open();
    t8.SelectPattern(IntArg(1), IntArg(2));
    Console.WriteLine($"Sent program change for bank {IntArg(1)} pattern {IntArg(2)}.");
    return 0;
}

int Preview()
{
    var (bass, rhythm) = LoadPatterns();
    using var t8 = Open();
    var bpm = Option("--bpm", 120.0);
    t8.PlayPreview(bass, rhythm, bpm);
    Console.WriteLine($"Playing at {bpm} BPM. Press Enter to stop.");
    Console.ReadLine();
    t8.StopPreview();
    return 0;
}

int RunClock()
{
    using var t8 = Open();
    t8.Clock.Bpm = Option("--bpm", 120.0);
    t8.Clock.Start();
    Console.WriteLine($"Clock running at {t8.Clock.Bpm} BPM. Press Enter to stop.");
    Console.ReadLine();
    t8.Clock.Stop();
    return 0;
}

int Export()
{
    var (bass, rhythm) = LoadPatterns();
    var path = args.Length > 4 ? args[4] : throw new ArgumentException("Missing output .mid path.");
    var file = new PatternMidiBuilder().BuildMidiFile(bass, rhythm, Option("--bpm", 120.0));
    file.Write(path, overwriteFile: true);
    Console.WriteLine($"Wrote {path}.");
    return 0;
}

(BassPattern? Bass, RhythmPattern? Rhythm) LoadPatterns()
{
    var folder = args.Length > 1 ? args[1] : throw new ArgumentException("Missing pattern folder.");
    int bank = IntArg(2), pattern = IntArg(3);
    var part = Option("--part", "both");
    var bass = part is "both" or "bass" ? BassPattern.Load(PatternLibrary.BassPath(folder, bank, pattern)) : null;
    var rhythm = part is "both" or "rhythm" ? RhythmPattern.Load(PatternLibrary.RhythmPath(folder, bank, pattern)) : null;
    return (bass, rhythm);
}

T8Controller Open()
{
    var ports = MidiPorts.ListOutputs();
    var name = Option<string?>("--port", null);
    var port = name is null
        ? MidiPorts.FindT8(ports)
        : ports.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (port is null)
        throw new InvalidOperationException(name is null
            ? "No T-8 found. Check the USB cable, or run 't8 ports' and pass --port."
            : $"No MIDI output named '{name}'.");
    Console.WriteLine($"Using {port.Name}");
    return new T8Controller(MidiPorts.OpenOutput(port));
}

int IntArg(int index) =>
    args.Length > index && int.TryParse(args[index], out var value)
        ? value
        : throw new ArgumentException($"Expected a number for argument {index}.\n\n{Usage}");

T Option<T>(string name, T fallback)
{
    var index = Array.IndexOf(args, name);
    if (index < 0 || index + 1 >= args.Length)
        return fallback;
    var raw = args[index + 1];
    return (T)Convert.ChangeType(raw, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), System.Globalization.CultureInfo.InvariantCulture);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
