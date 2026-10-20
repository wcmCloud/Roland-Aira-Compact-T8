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

/// <summary>An input that can play back events, like a T-8 sending Active Sensing.</summary>
internal sealed class FakeInputDevice : IInputDevice, IDisposable
{
    public event EventHandler<MidiEventReceivedEventArgs>? EventReceived;

    public bool IsListeningForEvents { get; private set; }
    public bool IsDisposed { get; private set; }

    /// <summary>Events sent as soon as listening starts.</summary>
    public List<MidiEvent> SendOnStart { get; } = new();

    public void StartEventsListening()
    {
        IsListeningForEvents = true;
        foreach (var midiEvent in SendOnStart)
            Receive(midiEvent);
    }

    public void StopEventsListening() => IsListeningForEvents = false;

    public void Receive(MidiEvent midiEvent)
    {
        if (IsListeningForEvents)
            EventReceived?.Invoke(this, new MidiEventReceivedEventArgs(midiEvent));
    }

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakePortProvider(params MidiPortInfo[] ports) : IMidiPortProvider
{
    public List<FakeOutputDevice> Opened { get; } = new();
    public List<FakeInputDevice> OpenedInputs { get; } = new();

    public FakeOutputDevice? LastOpened => Opened.LastOrDefault();

    /// <summary>Inputs to list; defaults to the same ports as the outputs.</summary>
    public List<MidiPortInfo>? Inputs { get; set; }

    /// <summary>Whether opened inputs answer like a T-8 (Active Sensing).</summary>
    public bool InputsRespond { get; set; } = true;

    public bool FailOutputs { get; set; }

    public IReadOnlyList<MidiPortInfo> ListOutputs() => ports;

    public IOutputDevice OpenOutput(MidiPortInfo port)
    {
        if (FailOutputs)
            throw new InvalidOperationException("Port is busy.");
        var device = new FakeOutputDevice();
        Opened.Add(device);
        return device;
    }

    public IReadOnlyList<MidiPortInfo> ListInputs() => Inputs ?? ports.ToList();

    public IInputDevice OpenInput(MidiPortInfo port)
    {
        var device = new FakeInputDevice();
        if (InputsRespond)
            device.SendOnStart.Add(new ActiveSensingEvent());
        OpenedInputs.Add(device);
        return device;
    }

    public static FakePortProvider WithT8() =>
        new(new MidiPortInfo("IAC Bus 1", "IAC Bus 1"), new MidiPortInfo("T-8", "T-8"));
}
