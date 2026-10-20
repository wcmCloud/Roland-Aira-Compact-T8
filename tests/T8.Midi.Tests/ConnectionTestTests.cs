using Melanchall.DryWetMidi.Core;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class ConnectionTestTests
{
    private static readonly MidiPortInfo T8 = new("T-8", "T-8");

    private static ConnectionTest Create(FakePortProvider ports, T8MidiMap? map = null) =>
        new(ports, map ?? new T8MidiMap())
        {
            ListenTime = TimeSpan.FromMilliseconds(200),
            NoteGap = TimeSpan.FromMilliseconds(1),
        };

    [Fact]
    public async Task Passes_when_sounds_go_out_and_the_t8_answers()
    {
        var ports = FakePortProvider.WithT8();
        var result = await Create(ports).RunAsync(T8);

        Assert.True(result.Success);
        Assert.True(result.T8Responded);
        Assert.Equal("T-8", result.InputPortName);
        Assert.Contains("the T-8 is answering", result.Summary);
        Assert.Contains(result.Log, l => l.Contains("Active Sensing"));
        Assert.True(ports.OpenedInputs.Single().IsDisposed);
    }

    [Fact]
    public async Task Sends_drums_and_a_bass_note_on_the_configured_channels()
    {
        var ports = FakePortProvider.WithT8();
        var map = new T8MidiMap { BassChannel = 3, RhythmChannel = 11 };
        await Create(ports, map).RunAsync(T8);

        var notes = ports.LastOpened!.Events.OfType<NoteOnEvent>().Select(n => ((int)n.NoteNumber, (int)n.Channel + 1)).ToList();
        Assert.Equal([(36, 11), (38, 11), (42, 11), (46, 11), (36, 3)], notes);
        Assert.True(ports.LastOpened.IsDisposed);
    }

    [Fact]
    public async Task Skips_parts_that_are_off()
    {
        var ports = FakePortProvider.WithT8();
        var map = new T8MidiMap { BassChannel = T8MidiMap.Off };
        var result = await Create(ports, map).RunAsync(T8);

        Assert.True(result.Success);
        Assert.Contains(result.Log, l => l.Contains("Bass is OFF"));
        Assert.All(ports.LastOpened!.Events.OfType<NoteOnEvent>(), n => Assert.Equal(9, (int)n.Channel));
    }

    [Fact]
    public async Task Fails_when_the_t8_is_silent()
    {
        var ports = FakePortProvider.WithT8();
        ports.InputsRespond = false;

        var result = await Create(ports).RunAsync(T8);

        Assert.False(result.Success);
        Assert.False(result.T8Responded);
        Assert.Contains("nothing came back", result.Summary);
    }

    [Fact]
    public async Task Without_an_input_it_only_checks_sending()
    {
        var ports = FakePortProvider.WithT8();
        ports.Inputs = [new MidiPortInfo("Other", "Other")];

        var result = await Create(ports).RunAsync(T8);

        Assert.True(result.Success);
        Assert.Null(result.T8Responded);
        Assert.Contains("check that you heard them", result.Summary);
    }

    [Fact]
    public async Task Fails_when_the_output_cannot_open()
    {
        var ports = FakePortProvider.WithT8();
        ports.FailOutputs = true;

        var result = await Create(ports).RunAsync(T8);

        Assert.False(result.OutputOk);
        Assert.False(result.Success);
        Assert.Contains(result.Log, l => l.Contains("Port is busy"));
    }

    [Fact]
    public async Task Invalid_settings_stop_the_test_before_sending()
    {
        var ports = FakePortProvider.WithT8();
        var result = await Create(ports, new T8MidiMap { BassChannel = 10 }).RunAsync(T8);

        Assert.False(result.Success);
        Assert.Empty(ports.Opened);
        Assert.Contains("Bass and rhythm need different channels.", result.Log);
    }

    [Fact]
    public async Task Can_be_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ConnectionTest(FakePortProvider.WithT8(), new T8MidiMap()).RunAsync(T8, cts.Token));
    }
}
