using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using T8.Core.Patterns;
using T8.Midi.Ports;

namespace T8.Midi;

/// <summary>
/// Live control of a connected T-8 over USB MIDI. The T-8 has no CC or SysEx support
/// (MIDI Implementation Chart), so live control is notes, program change and clock.
/// </summary>
public sealed class T8Controller : IDisposable
{
    private readonly IOutputDevice _output;
    private Playback? _preview;

    public T8Controller(IOutputDevice output, T8MidiMap? map = null)
    {
        _output = output;
        Map = map ?? new T8MidiMap();
        Clock = new MidiClockSender(output);
    }

    public T8MidiMap Map { get; }
    public MidiClockSender Clock { get; }

    /// <summary>Opens the first port that looks like a T-8.</summary>
    public static T8Controller? Connect(IMidiPortProvider? ports = null, T8MidiMap? map = null)
    {
        ports ??= MidiPorts.System;
        var port = MidiPorts.FindT8(ports.ListOutputs());
        return port is null ? null : new T8Controller(ports.OpenOutput(port), map);
    }

    /// <summary>
    /// Switches the T-8 to a pattern. Needs RX Program Change (rXPc) on.
    /// The order 0–63 = bank 1 pattern 1 … bank 4 pattern 16 is inferred.
    /// </summary>
    public void SelectPattern(int bank, int pattern)
    {
        var program = T8MidiMap.ProgramNumber(bank, pattern);
        Send(new ProgramChangeEvent((SevenBitNumber)program), Map.ProgramChangeChannel);
    }

    public void TriggerDrum(RhythmTrack track, int velocity = 100)
    {
        if (!Map.RhythmNotes.TryGetValue(track, out var note))
            throw new ArgumentException($"The T-8 has no sound for track {track}.", nameof(track));
        var number = (SevenBitNumber)note;
        Send(new NoteOnEvent(number, (SevenBitNumber)Math.Clamp(velocity, 1, 127)), Map.RhythmChannel);
        Send(new NoteOffEvent(number, SevenBitNumber.MinValue), Map.RhythmChannel);
    }

    public void BassNoteOn(int note, int velocity = 100) =>
        Send(new NoteOnEvent((SevenBitNumber)note, (SevenBitNumber)Math.Clamp(velocity, 1, 127)), Map.BassChannel);

    public void BassNoteOff(int note) =>
        Send(new NoteOffEvent((SevenBitNumber)note, SevenBitNumber.MinValue), Map.BassChannel);

    /// <summary>
    /// Plays patterns from the computer through the T-8's sounds. Stop the T-8's own sequencer first.
    /// </summary>
    public bool IsPreviewPlaying => _preview?.IsRunning == true;

    public Playback PlayPreview(BassPattern? bass, RhythmPattern? rhythm, double bpm, bool loop = true, int? seed = null)
    {
        StopPreview();

        var builder = new PatternMidiBuilder { Map = Map, Seed = seed };
        var events = builder.Build(bass, rhythm);
        var length = builder.LengthInTicks(bass, rhythm);
        // Playback ends at its last event, not at PlaybackEnd, so without this the loop would restart
        // right after the last note instead of at the end of the last step. Meta events aren't sent to the T-8.
        events.Add(new TimedEvent(new MarkerEvent("Loop end"), length));
        var tempoMap = PatternMidiBuilder.CreateTempoMap(bpm);
        var settings = new PlaybackSettings
        {
            // HighPrecisionTickGenerator (the default) is Windows/macOS only.
            ClockSettings = OperatingSystem.IsLinux()
                ? new MidiClockSettings { CreateTickGeneratorCallback = () => new RegularPrecisionTickGenerator() }
                : null,
        };

        _preview = new Playback(events, tempoMap, _output, settings)
        {
            Loop = loop,
            InterruptNotesOnStop = true,
            PlaybackEnd = new MidiTimeSpan(length),
        };
        _preview.Start();
        return _preview;
    }

    public void StopPreview()
    {
        if (_preview is null)
            return;
        _preview.Stop();
        _preview.Dispose();
        _preview = null;
        AllNotesOff();
    }

    public void AllNotesOff()
    {
        // CC 123 All Notes Off (recognized by the T-8).
        Send(new ControlChangeEvent((SevenBitNumber)123, SevenBitNumber.MinValue), Map.BassChannel);
        Send(new ControlChangeEvent((SevenBitNumber)123, SevenBitNumber.MinValue), Map.RhythmChannel);
    }

    private void Send(ChannelEvent midiEvent, int channel)
    {
        midiEvent.Channel = (FourBitNumber)(channel - 1);
        _output.SendEvent(midiEvent);
    }

    public void Dispose()
    {
        StopPreview();
        Clock.Dispose();
        (_output as IDisposable)?.Dispose();
    }
}
