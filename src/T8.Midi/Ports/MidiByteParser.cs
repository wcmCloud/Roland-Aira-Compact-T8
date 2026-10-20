using Melanchall.DryWetMidi.Core;

namespace T8.Midi.Ports;

/// <summary>
/// Turns a raw MIDI byte stream into events: real-time bytes, channel messages with running status,
/// and skips SysEx. Used for Linux raw MIDI input, where DryWetMIDI's device API isn't available.
/// </summary>
public sealed class MidiByteParser
{
    private readonly List<byte> _message = new();
    private byte _runningStatus;
    private bool _inSysEx;

    public IEnumerable<MidiEvent> Feed(ReadOnlySpan<byte> bytes)
    {
        var events = new List<MidiEvent>();
        foreach (var b in bytes)
        {
            if (b >= 0xF8)
            {
                if (RealTime(b) is { } realTime)
                    events.Add(realTime);
                continue;
            }

            if (b == 0xF0)
            {
                _inSysEx = true;
                continue;
            }
            if (_inSysEx)
            {
                if (b < 0x80)
                    continue; // SysEx data
                _inSysEx = false;
                if (b == 0xF7)
                    continue; // end of SysEx; any other status byte also ends it and is handled below
            }

            if (b >= 0x80)
            {
                _message.Clear();
                _runningStatus = b < 0xF0 ? b : (byte)0; // system common cancels running status
                if (b >= 0xF0)
                    continue; // system common messages aren't needed here
                _message.Add(b);
                continue;
            }

            if (_runningStatus == 0)
                continue; // data byte with no status: ignore
            if (_message.Count == 0)
                _message.Add(_runningStatus);
            _message.Add(b);

            if (_message.Count == 1 + DataLength(_runningStatus))
            {
                events.Add(ToEvent(_message.ToArray()));
                _message.Clear();
            }
        }
        return events;
    }

    private static int DataLength(byte status) => (status & 0xF0) is 0xC0 or 0xD0 ? 1 : 2;

    private static MidiEvent ToEvent(byte[] message)
    {
        using var converter = new BytesToMidiEventConverter();
        return converter.Convert(message);
    }

    private static MidiEvent? RealTime(byte b) => b switch
    {
        0xF8 => new TimingClockEvent(),
        0xFA => new StartEvent(),
        0xFB => new ContinueEvent(),
        0xFC => new StopEvent(),
        0xFE => new ActiveSensingEvent(),
        0xFF => new ResetEvent(),
        _ => null,
    };
}
