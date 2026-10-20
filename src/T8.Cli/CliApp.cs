using System.Globalization;
using T8.Core.Patterns;
using T8.Midi;
using T8.Midi.Ports;

namespace T8.Cli;

/// <summary>Command-line tool for trying the T-8 MIDI connection before the editor UI exists.</summary>
public sealed class CliApp(IMidiPortProvider ports, TextWriter output, TextWriter error, TextReader input)
{
    public const string Usage = """
        Usage: t8 <command> [options]

          ports                                   List MIDI outputs
          select <bank> <pattern>                 Switch the T-8 to a pattern (needs rXPc on)
          preview <folder> <bank> <pattern>       Play a .PRM pattern through the T-8's sounds
                  [--bpm 120] [--part both|bass|rhythm]
          clock [--bpm 120]                       Start the T-8's own sequencer at a tempo (needs SYnC AUTO/USB)
          export <folder> <bank> <pattern> <file.mid> [--bpm 120] [--part both|bass|rhythm]
                                                  Save a pattern as a MIDI file

          <folder> is a BACKUP or RESTORE folder with BASS and RHYTHM inside.
          Add --port "<name>" to use a port other than the auto-detected T-8.
        """;

    private string[] _args = [];

    public int Run(string[] args)
    {
        _args = args;
        if (args.Length == 0)
        {
            output.WriteLine(Usage);
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
    }

    private int ListPorts()
    {
        var list = ports.ListOutputs();
        if (list.Count == 0)
            output.WriteLine("No MIDI outputs found.");
        foreach (var port in list)
            output.WriteLine($"{(MidiPorts.LooksLikeT8(port.Name) ? "*" : " ")} {port.Name}");
        return 0;
    }

    private int Select()
    {
        int bank = IntArg(1), pattern = IntArg(2);
        using var t8 = Open();
        t8.SelectPattern(bank, pattern);
        output.WriteLine($"Sent program change for bank {bank} pattern {pattern}.");
        return 0;
    }

    private int Preview()
    {
        var (bass, rhythm) = LoadPatterns();
        using var t8 = Open();
        var bpm = Option("--bpm", 120.0);
        t8.PlayPreview(bass, rhythm, bpm);
        output.WriteLine($"Playing at {bpm.ToString(CultureInfo.InvariantCulture)} BPM. Press Enter to stop.");
        input.ReadLine();
        t8.StopPreview();
        return 0;
    }

    private int RunClock()
    {
        using var t8 = Open();
        t8.Clock.Bpm = Option("--bpm", 120.0);
        t8.Clock.Start();
        output.WriteLine($"Clock running at {t8.Clock.Bpm.ToString(CultureInfo.InvariantCulture)} BPM. Press Enter to stop.");
        input.ReadLine();
        t8.Clock.Stop();
        return 0;
    }

    private int Export()
    {
        var (bass, rhythm) = LoadPatterns();
        var path = _args.Length > 4 && !_args[4].StartsWith("--") ? _args[4] : throw new ArgumentException("Missing output .mid path.");
        var file = new PatternMidiBuilder().BuildMidiFile(bass, rhythm, Option("--bpm", 120.0));
        file.Write(path, overwriteFile: true);
        output.WriteLine($"Wrote {path}.");
        return 0;
    }

    private (BassPattern? Bass, RhythmPattern? Rhythm) LoadPatterns()
    {
        var folder = _args.Length > 1 ? _args[1] : throw new ArgumentException("Missing pattern folder.");
        int bank = IntArg(2), pattern = IntArg(3);
        var part = Option("--part", "both");
        if (part is not ("both" or "bass" or "rhythm"))
            throw new ArgumentException($"--part must be both, bass or rhythm, not '{part}'.");
        var bass = part is "both" or "bass" ? BassPattern.Load(PatternLibrary.BassPath(folder, bank, pattern)) : null;
        var rhythm = part is "both" or "rhythm" ? RhythmPattern.Load(PatternLibrary.RhythmPath(folder, bank, pattern)) : null;
        return (bass, rhythm);
    }

    private T8Controller Open()
    {
        var list = ports.ListOutputs();
        var name = Option<string?>("--port", null);
        var port = name is null
            ? MidiPorts.FindT8(list)
            : list.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (port is null)
            throw new InvalidOperationException(name is null
                ? "No T-8 found. Check the USB cable, or run 't8 ports' and pass --port."
                : $"No MIDI output named '{name}'.");
        output.WriteLine($"Using {port.Name}");
        return new T8Controller(ports.OpenOutput(port));
    }

    private int IntArg(int index) =>
        _args.Length > index && int.TryParse(_args[index], out var value)
            ? value
            : throw new ArgumentException($"Expected a number for argument {index}.\n\n{Usage}");

    private T Option<T>(string name, T fallback)
    {
        var index = Array.IndexOf(_args, name);
        if (index < 0 || index + 1 >= _args.Length)
            return fallback;
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(_args[index + 1], type, CultureInfo.InvariantCulture);
    }

    private int Fail(string message)
    {
        error.WriteLine(message);
        return 1;
    }
}
