using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using T8.Midi.Ports;

namespace T8.Tests.Shared;

/// <summary>Records every event sent, standing in for a T-8 on the USB port.</summary>
internal sealed class FakeOutputDevice : IOutputDevice, IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<MidiEvent> _events = new();

    public event EventHandler<MidiEventSentEventArgs>? EventSent;

    public bool IsDisposed { get; private set; }

    public IReadOnlyList<MidiEvent> Events
    {
        get
        {
            lock (_lock)
                return _events.ToList();
        }
    }

    public void PrepareForEventsSending()
    {
    }

    public void SendEvent(MidiEvent midiEvent)
    {
        lock (_lock)
            _events.Add(midiEvent.Clone());
        EventSent?.Invoke(this, new MidiEventSentEventArgs(midiEvent));
    }

    public void Clear()
    {
        lock (_lock)
            _events.Clear();
    }

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakePortProvider(params MidiPortInfo[] ports) : IMidiPortProvider
{
    public List<FakeOutputDevice> Opened { get; } = new();

    public FakeOutputDevice? LastOpened => Opened.LastOrDefault();

    public IReadOnlyList<MidiPortInfo> ListOutputs() => ports;

    public IOutputDevice OpenOutput(MidiPortInfo port)
    {
        var device = new FakeOutputDevice();
        Opened.Add(device);
        return device;
    }

    public static FakePortProvider WithT8() =>
        new(new MidiPortInfo("IAC Bus 1", "IAC Bus 1"), new MidiPortInfo("T-8", "T-8"));
}
