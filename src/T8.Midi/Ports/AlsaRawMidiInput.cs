using Melanchall.DryWetMidi.Multimedia;

namespace T8.Midi.Ports;

/// <summary>Linux input from an ALSA raw MIDI device file, read on a background thread.</summary>
public sealed class AlsaRawMidiInput : IInputDevice, IDisposable
{
    private readonly MidiPortInfo _port;
    private readonly MidiByteParser _parser = new();
    private FileStream? _stream;
    private Thread? _thread;
    private volatile bool _listening;

    public AlsaRawMidiInput(MidiPortInfo port) => _port = port;

    public event EventHandler<MidiEventReceivedEventArgs>? EventReceived;

    public bool IsListeningForEvents => _listening;

    public void StartEventsListening()
    {
        if (_listening)
            return;
        _stream = new FileStream(_port.Id, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 0);
        _listening = true;
        _thread = new Thread(Read) { IsBackground = true, Name = "T-8 MIDI input" };
        _thread.Start();
    }

    public void StopEventsListening()
    {
        _listening = false;
        _stream?.Dispose();
        _stream = null;
    }

    private void Read()
    {
        var buffer = new byte[256];
        var stream = _stream;
        try
        {
            while (_listening && stream is not null)
            {
                var count = stream.Read(buffer, 0, buffer.Length);
                if (count == 0)
                {
                    Thread.Sleep(5); // regular files (tests) report end of file; device files block
                    continue;
                }
                foreach (var midiEvent in _parser.Feed(buffer.AsSpan(0, count)))
                    EventReceived?.Invoke(this, new MidiEventReceivedEventArgs(midiEvent));
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Port closed or unplugged.
        }
    }

    public void Dispose() => StopEventsListening();
}
