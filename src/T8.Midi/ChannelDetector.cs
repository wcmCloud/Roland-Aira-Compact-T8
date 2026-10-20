using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using T8.Midi.Ports;

namespace T8.Midi;

/// <summary>
/// What <see cref="ChannelDetector"/> worked out from the T-8's output. A channel is null when the T-8
/// sent nothing that shows it, e.g. no program change because the pattern wasn't changed.
/// </summary>
public sealed record ChannelDetectionResult(
    int? BassChannel,
    int? RhythmChannel,
    int? ProgramChangeChannel,
    string? InputPortName,
    int EventsHeard)
{
    public bool FoundAny => BassChannel is not null || RhythmChannel is not null || ProgramChangeChannel is not null;

    public string Summary
    {
        get
        {
            if (InputPortName is null)
                return "No T-8 MIDI input found, so the channels can't be detected. Check the USB cable.";
            if (!FoundAny)
                return EventsHeard == 0
                    ? $"Nothing came from {InputPortName}. Check the USB cable."
                    : "The T-8 sent no notes. Press Start on the T-8 while detecting, with a pattern that has drums and bass.";

            var found = new List<string>();
            if (BassChannel is { } bass)
                found.Add($"bass {bass}");
            if (RhythmChannel is { } rhythm)
                found.Add($"rhythm {rhythm}");
            if (ProgramChangeChannel is { } pc)
                found.Add($"pattern select {pc}");
            var missing = new List<string>();
            if (BassChannel is null)
                missing.Add("bass (play a pattern with bass notes)");
            if (RhythmChannel is null)
                missing.Add("rhythm (play a pattern with drums)");
            if (ProgramChangeChannel is null)
                missing.Add("pattern select (the T-8 doesn't always send it; set it to the T-8's Pc.Ch by hand)");

            var text = $"Detected {string.Join(", ", found)}.";
            return missing.Count == 0 ? text : $"{text} Not seen: {string.Join(", ", missing)}.";
        }
    }

    /// <summary>Copies the detected channels onto a map, keeping the others as they are.</summary>
    public T8MidiMap ApplyTo(T8MidiMap map)
    {
        var result = map.Clone();
        result.BassChannel = BassChannel ?? result.BassChannel;
        result.RhythmChannel = RhythmChannel ?? result.RhythmChannel;
        result.ProgramChangeChannel = ProgramChangeChannel ?? result.ProgramChangeChannel;
        return result;
    }
}

/// <summary>
/// Finds the T-8's bACh and rhCh by listening to what it sends while the user plays it: its sequencer
/// transmits bass and drum notes on those channels (checked on a T-8). Drum notes are told apart from
/// bass notes by the map's drum note numbers. Pc.Ch is picked up too if the T-8 sends a program
/// change, but a T-8 changing pattern by hand was seen not to.
/// </summary>
public sealed class ChannelDetector(IMidiPortProvider ports, T8MidiMap? map = null)
{
    private readonly T8MidiMap _map = map ?? new T8MidiMap();

    /// <summary>How long to keep listening for a program change once bass and rhythm are known.</summary>
    public TimeSpan ExtraTime { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Listens on the T-8's input for <paramref name="duration"/>, or until bass and rhythm have been
    /// seen and <see cref="ExtraTime"/> has passed (or a program change has been seen too).
    /// </summary>
    public async Task<ChannelDetectionResult> RunAsync(
        MidiPortInfo output, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var inputPort = MidiPorts.FindMatchingInput(ports.ListInputs(), output);
        if (inputPort is null)
            return new ChannelDetectionResult(null, null, null, null, 0);

        var events = new List<MidiEvent>();
        var gate = new Lock();
        var input = ports.OpenInput(inputPort);
        try
        {
            input.EventReceived += (_, e) =>
            {
                lock (gate)
                    events.Add(e.Event);
            };
            input.StartEventsListening();

            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, cancellationToken);
                lock (gate)
                {
                    var partial = Analyze(events, _map, inputPort.Name);
                    if (partial.BassChannel is null || partial.RhythmChannel is null)
                        continue;
                    if (partial.ProgramChangeChannel is not null)
                        break;
                    var stopAt = DateTime.UtcNow + ExtraTime;
                    if (stopAt < deadline)
                        deadline = stopAt;
                }
            }
        }
        finally
        {
            try
            {
                input.StopEventsListening();
            }
            catch (Exception)
            {
                // Closing is best effort.
            }
            (input as IDisposable)?.Dispose();
        }

        lock (gate)
            return Analyze(events, _map, inputPort.Name);
    }

    /// <summary>Works out the channels from received events: the channel used most for each kind wins.</summary>
    public static ChannelDetectionResult Analyze(IEnumerable<MidiEvent> events, T8MidiMap map, string? inputPortName)
    {
        var drumNotes = map.RhythmNotes.Values.ToHashSet();
        var drumVotes = new int[16];
        var bassVotes = new int[16];
        var programVotes = new int[16];
        var count = 0;

        foreach (var midiEvent in events)
        {
            count++;
            switch (midiEvent)
            {
                case NoteOnEvent note when note.Velocity > 0:
                    if (drumNotes.Contains(note.NoteNumber))
                        drumVotes[note.Channel]++;
                    else
                        bassVotes[note.Channel]++;
                    break;
                case ProgramChangeEvent program:
                    programVotes[program.Channel]++;
                    break;
            }
        }

        // A channel with any non-drum note is the bass channel, whose notes can also hit drum note numbers.
        var bass = Best(bassVotes);
        if (bass is { } b)
            drumVotes[b - 1] = 0;
        return new ChannelDetectionResult(bass, Best(drumVotes), Best(programVotes), inputPortName, count);
    }

    private static int? Best(int[] votes)
    {
        var max = votes.Max();
        return max == 0 ? null : Array.IndexOf(votes, max) + 1;
    }
}
