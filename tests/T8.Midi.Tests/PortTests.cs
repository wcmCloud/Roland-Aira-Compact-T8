using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using T8.Midi.Ports;
using T8.Tests.Shared;

namespace T8.Midi.Tests;

public class PortTests
{
    [Theory]
    [InlineData("T-8", true)]
    [InlineData("Roland T-8 MIDI", true)]
    [InlineData("t8", true)]
    [InlineData("IAC Bus 1", false)]
    [InlineData("TR-8S", false)]
    public void Recognises_t8_port_names(string name, bool expected) =>
        Assert.Equal(expected, MidiPorts.LooksLikeT8(name));

    [Fact]
    public void FindT8_picks_the_t8_port()
    {
        var ports = FakePortProvider.WithT8().ListOutputs();
        Assert.Equal("T-8", MidiPorts.FindT8(ports)?.Name);
        Assert.Null(MidiPorts.FindT8([new MidiPortInfo("IAC", "IAC")]));
    }

    [Fact]
    public void Alsa_lists_raw_midi_devices_with_card_names()
    {
        using var temp = TestPaths.CreateTempFolder();
        var dev = Directory.CreateDirectory(Path.Combine(temp.Path, "snd")).FullName;
        File.WriteAllText(Path.Combine(dev, "midiC1D0"), "");
        File.WriteAllText(Path.Combine(dev, "midiC2D1"), "");
        File.WriteAllText(Path.Combine(dev, "pcmC1D0p"), "");
        var cards = Path.Combine(temp.Path, "cards");
        File.WriteAllText(cards, " 1 [T8             ]: USB-Audio - T-8\n                      Roland T-8\n");

        var ports = AlsaRawMidiOutput.List(dev, cards);

        Assert.Equal(["T-8", "Card 2 (1)"], ports.Select(p => p.Name));
        Assert.Equal(Path.Combine(dev, "midiC1D0"), ports[0].Id);
    }

    [Fact]
    public void Alsa_list_is_empty_without_sound_devices() =>
        Assert.Empty(AlsaRawMidiOutput.List("/nonexistent/snd", "/nonexistent/cards"));

    [Fact]
    public void Linux_card_names_are_parsed()
    {
        const string cards = """
             0 [PCH            ]: HDA-Intel - HDA Intel PCH
                                  HDA Intel PCH at 0xf7f10000 irq 31
             1 [T8             ]: USB-Audio - T-8
                                  Roland T-8 at usb-0000:00:14.0-2, high speed
            """;

        var names = AlsaRawMidiOutput.ParseCardNames(cards);

        Assert.Equal("HDA Intel PCH", names[0]);
        Assert.Equal("T-8", names[1]);
    }

    [Fact]
    public void Alsa_output_writes_raw_midi_bytes()
    {
        using var temp = TestPaths.CreateTempFolder();
        var file = Path.Combine(temp.Path, "midiC1D0");
        File.WriteAllBytes(file, []);
        var sent = 0;

        using (var output = new AlsaRawMidiOutput(new MidiPortInfo("T-8", file)))
        {
            output.EventSent += (_, _) => sent++;
            output.PrepareForEventsSending();
            output.SendEvent(new NoteOnEvent((SevenBitNumber)36, (SevenBitNumber)100) { Channel = (FourBitNumber)9 });
            output.SendEvent(new ProgramChangeEvent((SevenBitNumber)5) { Channel = (FourBitNumber)15 });
            output.SendEvent(new TimingClockEvent());
        }

        Assert.Equal(new byte[] { 0x99, 36, 100, 0xCF, 5, 0xF8 }, File.ReadAllBytes(file));
        Assert.Equal(3, sent);
    }
}
