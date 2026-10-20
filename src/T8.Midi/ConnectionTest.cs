using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using T8.Core.Patterns;
using T8.Midi.Ports;

namespace T8.Midi;

public sealed record ConnectionTestResult(
    bool OutputOk,
    bool? T8Responded,
    string? InputPortName,
    IReadOnlyList<string> Log)
{
    /// <summary>Output works and, if we could listen, the T-8 answered.</summary>
    public bool Success => OutputOk && T8Responded != false;

    public string Summary =>
        !OutputOk ? "Couldn't send to the MIDI output."
        : T8Responded == true ? "Connected: the T-8 is answering and test sounds were sent."
        : T8Responded == false ? "Sounds were sent, but nothing came back from the T-8."
        : "Test sounds were sent. No MIDI input to listen on, so check that you heard them.";
}

/// <summary>
/// Checks the USB MIDI link: plays kick, snare, hats and a bass note on the configured channels,
/// and listens on the T-8's MIDI input for anything it sends (it transmits Active Sensing
/// continuously), which proves the cable and driver work in both directions.
/// </summary>
public sealed class ConnectionTest(IMidiPortProvider ports, T8MidiMap map)
{
    public TimeSpan ListenTime { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan NoteGap { get; init; } = TimeSpan.FromMilliseconds(220);

    public async Task<ConnectionTestResult> RunAsync(MidiPortInfo output, CancellationToken cancellationToken = default)
    {
        var log = new List<string>();
        var errors = map.Validate();
        if (errors.Count > 0)
        {
            log.AddRange(errors);
            return new ConnectionTestResult(false, null, null, log);
        }

        IInputDevice? input = null;
        MidiPortInfo? inputPort = null;
        var received = 0;
        string? firstReceived = null;

        try
        {
            inputPort = MidiPorts.FindMatchingInput(ports.ListInputs(), output);
            if (inputPort is not null)
            {
                input = ports.OpenInput(inputPort);
                input.EventReceived += (_, e) =>
                {
                    if (Interlocked.Increment(ref received) == 1)
                        firstReceived = Describe(e.Event);
                };
                input.StartEventsListening();
                log.Add($"Listening on input {inputPort.Name}.");
            }
            else
            {
                log.Add("No matching MIDI input found, so the T-8's reply can't be checked.");
            }
        }
        catch (Exception ex)
        {
            log.Add($"Couldn't open the input {inputPort?.Name}: {ex.Message}");
            DisposeInput(ref input);
            inputPort = null;
        }

        var outputOk = false;
        try
        {
            using var t8 = new T8Controller(ports.OpenOutput(output), map);
            log.Add($"Opened output {output.Name}.");

            foreach (var track in new[] { RhythmTrack.BD, RhythmTrack.SD, RhythmTrack.CH, RhythmTrack.OH })
            {
                t8.TriggerDrum(track);
                log.Add($"Sent {track} (note {map.RhythmNotes[track]}) on rhythm channel {map.RhythmChannel}.");
                await Task.Delay(NoteGap, cancellationToken);
            }

            const int testNote = 36; // C2
            t8.BassNoteOn(testNote);
            await Task.Delay(NoteGap * 2, cancellationToken);
            t8.BassNoteOff(testNote);
            log.Add($"Sent bass note C2 on bass channel {map.BassChannel}.");
            outputOk = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.Add($"Couldn't send to {output.Name}: {ex.Message}");
        }

        bool? responded = null;
        if (input is not null)
        {
            var deadline = DateTime.UtcNow + ListenTime;
            while (Volatile.Read(ref received) == 0 && DateTime.UtcNow < deadline)
                await Task.Delay(20, cancellationToken);

            responded = received > 0;
            log.Add(responded == true
                ? $"Received {received} message(s) from the T-8, first: {firstReceived}."
                : $"Nothing received from {inputPort!.Name} in {ListenTime.TotalSeconds:0.#} s.");
            DisposeInput(ref input);
        }

        return new ConnectionTestResult(outputOk, responded, inputPort?.Name, log);
    }

    private static string Describe(MidiEvent midiEvent) => midiEvent switch
    {
        ActiveSensingEvent => "Active Sensing",
        TimingClockEvent => "MIDI clock",
        _ => midiEvent.EventType.ToString(),
    };

    private static void DisposeInput(ref IInputDevice? input)
    {
        if (input is null)
            return;
        try
        {
            input.StopEventsListening();
        }
        catch (Exception)
        {
            // Closing is best effort.
        }
        (input as IDisposable)?.Dispose();
        input = null;
    }
}
