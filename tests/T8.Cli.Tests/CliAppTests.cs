using Melanchall.DryWetMidi.Core;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Cli.Tests;

public class CliAppTests
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    private int Run(FakePortProvider ports, params string[] args) =>
        new CliApp(ports, _out, _err, new StringReader("\n")).Run(args);

    private int Run(params string[] args) => Run(FakePortProvider.WithT8(), args);

    [Fact]
    public void No_arguments_prints_usage()
    {
        Assert.Equal(1, Run());
        Assert.Contains("Usage: t8", _out.ToString());
    }

    [Fact]
    public void Unknown_command_fails_with_usage()
    {
        Assert.Equal(1, Run("dance"));
        Assert.Contains("Unknown command 'dance'", _err.ToString());
    }

    [Fact]
    public void Ports_lists_outputs_and_inputs_and_marks_the_t8()
    {
        Assert.Equal(0, Run("ports"));
        var text = _out.ToString();
        Assert.Contains("Outputs:", text);
        Assert.Contains("Inputs:", text);
        Assert.Contains("  IAC Bus 1", text);
        Assert.Contains("* T-8", text);
    }

    [Fact]
    public void Ports_says_when_there_are_none()
    {
        Assert.Equal(0, Run(new FakePortProvider(), "ports"));
        Assert.Contains("No MIDI outputs found.", _out.ToString());
        Assert.Contains("No MIDI inputs found.", _out.ToString());
    }

    [Fact]
    public void Test_reports_a_working_connection()
    {
        Assert.Equal(0, Run("test"));
        Assert.Contains("the T-8 is answering", _out.ToString());
    }

    [Fact]
    public void Detect_prints_the_channels_to_use()
    {
        var ports = FakePortProvider.WithT8();
        ports.InputEvents.Add(new NoteOnEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)36, (Melanchall.DryWetMidi.Common.SevenBitNumber)100)
        {
            Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)4,
        });

        Assert.Equal(0, Run(ports, "detect", "--seconds", "0.1"));
        Assert.Contains("Detected rhythm 5.", _out.ToString());
        Assert.Contains("Use: --bass-ch 2 --rhythm-ch 5 --pc-ch 16", _out.ToString());
    }

    [Fact]
    public void Detect_fails_when_the_t8_plays_nothing()
    {
        Assert.Equal(1, Run("detect", "--seconds", "0.1"));
        Assert.Contains("Press Start on the T-8", _out.ToString());
    }

    [Fact]
    public void Test_fails_when_the_t8_is_silent()
    {
        var ports = FakePortProvider.WithT8();
        ports.InputsRespond = false;
        Assert.Equal(1, Run(ports, "test"));
        Assert.Contains("nothing came back", _out.ToString());
    }

    [Fact]
    public void Channel_options_change_where_notes_go()
    {
        var ports = FakePortProvider.WithT8();
        Assert.Equal(0, Run(ports, "select", "1", "2", "--pc-ch", "1"));
        Assert.Equal(0, (int)((ProgramChangeEvent)ports.LastOpened!.Events[0]).Channel);
    }

    [Fact]
    public void Invalid_channel_options_fail()
    {
        Assert.Equal(1, Run("select", "1", "1", "--bass-ch", "10"));
        Assert.Contains("Bass and rhythm need different channels.", _err.ToString());
    }

    [Fact]
    public void Select_sends_program_change()
    {
        var ports = FakePortProvider.WithT8();
        Assert.Equal(0, Run(ports, "select", "2", "5"));

        var pc = Assert.IsType<ProgramChangeEvent>(Assert.Single(ports.LastOpened!.Events));
        Assert.Equal(20, (int)pc.ProgramNumber);
        Assert.True(ports.LastOpened.IsDisposed);
    }

    [Fact]
    public void Select_rejects_bad_numbers()
    {
        Assert.Equal(1, Run("select", "x", "1"));
        Assert.Contains("Expected a number", _err.ToString());
        Assert.Equal(1, Run("select", "9", "1"));
        Assert.Contains("Bank must be 1–4", _err.ToString());
    }

    [Fact]
    public void Fails_without_a_t8()
    {
        Assert.Equal(1, Run(new FakePortProvider(new MidiPortInfo("IAC", "IAC")), "select", "1", "1"));
        Assert.Contains("No T-8 found", _err.ToString());
    }

    [Fact]
    public void Port_option_picks_a_named_port()
    {
        var ports = FakePortProvider.WithT8();
        Assert.Equal(0, Run(ports, "select", "1", "1", "--port", "iac bus 1"));
        Assert.Contains("Using IAC Bus 1", _out.ToString());
    }

    [Fact]
    public void Port_option_with_unknown_name_fails()
    {
        Assert.Equal(1, Run("select", "1", "1", "--port", "Nope"));
        Assert.Contains("No MIDI output named 'Nope'", _err.ToString());
    }

    [Fact]
    public void Preview_plays_until_enter_then_stops()
    {
        var ports = FakePortProvider.WithT8();
        Assert.Equal(0, Run(ports, "preview", TestPaths.Backup, "1", "1", "--bpm", "140"));

        Assert.Contains("Playing at 140 BPM", _out.ToString());
        Assert.Contains(ports.LastOpened!.Events, e => e is ControlChangeEvent c && c.ControlNumber == 123);
    }

    [Fact]
    public void Preview_with_missing_folder_fails()
    {
        Assert.Equal(1, Run("preview", "/nonexistent", "1", "1"));
        Assert.NotEmpty(_err.ToString());
    }

    [Fact]
    public void Clock_starts_and_stops_the_t8()
    {
        var ports = FakePortProvider.WithT8();
        Assert.Equal(0, Run(ports, "clock", "--bpm", "98"));

        var events = ports.LastOpened!.Events;
        Assert.IsType<StartEvent>(events[0]);
        Assert.IsType<StopEvent>(events[^1]);
        Assert.Contains("Clock running at 98 BPM", _out.ToString());
    }

    [Theory]
    [InlineData("both")]
    [InlineData("bass")]
    [InlineData("rhythm")]
    public void Export_writes_a_midi_file(string part)
    {
        using var temp = TestPaths.CreateTempFolder();
        var path = Path.Combine(temp.Path, "p.mid");

        Assert.Equal(0, Run("export", TestPaths.Backup, "1", "1", path, "--part", part));

        Assert.NotEmpty(MidiFile.Read(path).GetTrackChunks());
        Assert.Contains($"Wrote {path}.", _out.ToString());
    }

    [Fact]
    public void Export_needs_an_output_path()
    {
        Assert.Equal(1, Run("export", TestPaths.Backup, "1", "1"));
        Assert.Contains("Missing output .mid path", _err.ToString());
    }

    [Fact]
    public void Export_rejects_unknown_part()
    {
        Assert.Equal(1, Run("export", TestPaths.Backup, "1", "1", "x.mid", "--part", "drums"));
        Assert.Contains("--part must be", _err.ToString());
    }
}
