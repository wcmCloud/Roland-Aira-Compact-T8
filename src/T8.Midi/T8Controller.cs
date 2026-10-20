using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using T8.Core.Patterns;
using T8.Core.Songs;
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

    private long _loopTicks;
    private long _fillTicks;
    private double _msPerTick;
    private readonly System.Diagnostics.Stopwatch _sinceFillQueued = new();

    /// <summary>Raised (on the playback thread) when a queued fill has played and the main loop is back.</summary>
    public event EventHandler? FillFinished;

    /// <summary>True while a fill set up by <see cref="PlayPreview"/> is queued or playing.</summary>
    public bool IsFillPending => _preview is { } p && p.PlaybackEnd is MidiTimeSpan end && end.TimeSpan > _loopTicks;

    /// <param name="fill">
    /// A rhythm to play once in place of the main rhythm when <see cref="QueueFill"/> is called, like
    /// the T-8's FILL TRG. The bass keeps playing under it.
    /// </param>
    public Playback PlayPreview(BassPattern? bass, RhythmPattern? rhythm, double bpm, bool loop = true, int? seed = null, RhythmPattern? fill = null)
    {
        StopPreview();

        // Parts switched OFF aren't played at all.
        if (!Map.BassOn)
            bass = null;
        if (!Map.RhythmOn)
        {
            rhythm = null;
            fill = null;
        }

        var builder = new PatternMidiBuilder { Map = Map, Seed = seed };
        var events = builder.Build(bass, rhythm);
        var length = builder.LengthInTicks(bass, rhythm);
        _loopTicks = length;
        _fillTicks = 0;
        _msPerTick = 60_000.0 / (bpm * PatternMidiBuilder.TicksPerQuarterNote);
        _sinceFillQueued.Reset();
        if (fill is not null)
        {
            // The fill sits after the main loop; PlaybackEnd keeps it out until it is queued.
            var fillEvents = builder.Build(bass, fill, fill.Length);
            foreach (var e in fillEvents)
                e.Time += length;
            events.AddRange(fillEvents);
            _fillTicks = (long)fill.Length * PatternMidiBuilder.TicksPerStep;
        }
        return StartPlayback(events, length, length + _fillTicks, bpm, loop);
    }

    /// <summary>
    /// Plays a song through the T-8's sounds: every pass of the plan one after another, from pass
    /// <paramref name="fromPass"/>, and round again from the start if <paramref name="loop"/> is set.
    /// <see cref="CurrentStep"/> then counts steps from the start of the song.
    /// </summary>
    public Playback PlaySong(SongPlan plan, PatternLibrary library, double bpm, bool loop, int fromPass = 0)
    {
        StopPreview();

        var builder = new PatternMidiBuilder { Map = Map };
        var events = new List<TimedEvent>();
        foreach (var pass in plan.Passes)
        {
            var bass = Map.BassOn ? library.Bass[pass.Bank - 1, pass.Pattern - 1] : null;
            var rhythm = Map.RhythmOn ? library.Rhythm[pass.Bank - 1, pass.Pattern - 1] : null;
            var offset = (long)pass.StartStep * PatternMidiBuilder.TicksPerStep;
            foreach (var e in builder.Build(bass, rhythm, pass.Steps))
            {
                e.Time += offset;
                events.Add(e);
            }
        }

        var length = (long)plan.TotalSteps * PatternMidiBuilder.TicksPerStep;
        _loopTicks = length;
        _fillTicks = 0;
        _msPerTick = 60_000.0 / (bpm * PatternMidiBuilder.TicksPerQuarterNote);
        var playback = StartPlayback(events, length, length, bpm, loop);
        if (fromPass > 0 && fromPass < plan.Passes.Count)
            playback.MoveToTime(new MidiTimeSpan((long)plan.Passes[fromPass].StartStep * PatternMidiBuilder.TicksPerStep));
        return playback;
    }

    private Thread? _jumpWatcher;
    private volatile bool _jumpPending;

    /// <summary>
    /// When the song preview reaches <paramref name="atStep"/> (the end of the pass playing now),
    /// carries on from <paramref name="toStep"/> instead, so a chosen part starts right after the
    /// current one. A later call replaces an earlier one.
    /// </summary>
    public void QueueSongJump(long atStep, long toStep)
    {
        if (_preview is not { IsRunning: true } preview)
            return;
        var atTicks = atStep * PatternMidiBuilder.TicksPerStep;
        var toTicks = toStep * PatternMidiBuilder.TicksPerStep;
        _jumpPending = false;
        _jumpWatcher?.Join();
        _jumpPending = true;
        _jumpWatcher = new Thread(() =>
        {
            // Jump a moment before the boundary, keeping the distance to it, so no note of the
            // following pass sounds first.
            const long lead = 2;
            var last = -1L;
            while (_jumpPending && preview.IsRunning)
            {
                var now = preview.GetCurrentTime<MidiTimeSpan>().TimeSpan;
                if (now >= atTicks - lead)
                {
                    preview.MoveToTime(new MidiTimeSpan(Math.Max(0, toTicks - (atTicks - now))));
                    break;
                }
                if (now < last)
                {
                    // The song looped round before the boundary was seen: carry on from the target.
                    preview.MoveToTime(new MidiTimeSpan(toTicks + now));
                    break;
                }
                last = now;
                Thread.Sleep(1);
            }
            _jumpPending = false;
        }) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "T-8 song jump" };
        _jumpWatcher.Start();
    }

    /// <summary>True while a jump queued with <see cref="QueueSongJump"/> hasn't happened yet.</summary>
    public bool IsSongJumpPending => _jumpPending;

    /// <summary>True when a preview started by <see cref="PlaySong"/> or <see cref="PlayPreview"/> has played to its end.</summary>
    public bool PreviewFinished => _preview is { IsRunning: false } && !_paused;

    private bool _paused;

    private Playback StartPlayback(List<TimedEvent> events, long playbackEnd, long timelineEnd, double bpm, bool loop)
    {
        _paused = false;
        // Playback ends at its last event, not at PlaybackEnd, so without this the loop would restart
        // right after the last note instead of at the end of the last step. Meta events aren't sent to the T-8.
        events.Add(new TimedEvent(new MarkerEvent("Loop end"), timelineEnd));
        var tempoMap = PatternMidiBuilder.CreateTempoMap(bpm);
        var settings = new PlaybackSettings
        {
            // HighPrecisionTickGenerator (the default) is Windows/macOS only, and the regular one is far
            // too irregular on Linux, so Linux gets a thread-based generator.
            ClockSettings = OperatingSystem.IsLinux()
                ? new MidiClockSettings { CreateTickGeneratorCallback = () => new ThreadTickGenerator() }
                : null,
        };

        _preview = new Playback(events, tempoMap, _output, settings)
        {
            Loop = loop,
            InterruptNotesOnStop = true,
            PlaybackEnd = new MidiTimeSpan(playbackEnd),
        };
        _preview.RepeatStarted += OnRepeatStarted;
        _preview.Start();
        return _preview;
    }

    /// <summary>
    /// Plays the fill given to <see cref="PlayPreview"/> once, starting at the end of the current loop,
    /// then goes back to the main loop. False when there is no fill or no preview playing.
    /// </summary>
    public bool QueueFill()
    {
        if (_preview is not { IsRunning: true } preview || _fillTicks == 0)
            return false;
        preview.PlaybackEnd = new MidiTimeSpan(_loopTicks + _fillTicks);
        _sinceFillQueued.Restart();
        return true;
    }

    private void OnRepeatStarted(object? sender, EventArgs e)
    {
        if (sender is not Playback preview || preview.PlaybackEnd is not MidiTimeSpan end || end.TimeSpan <= _loopTicks)
            return;
        // A wrap this soon after queuing is the main loop wrapping as the fill was queued: the
        // fill hasn't played yet and will follow this pass of the main loop.
        if (_sinceFillQueued.Elapsed.TotalMilliseconds < _fillTicks * _msPerTick / 2)
            return;
        // Wrapping to the start with the end past the main loop means the fill has just played.
        _sinceFillQueued.Reset();
        preview.PlaybackEnd = new MidiTimeSpan(_loopTicks);
        FillFinished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The 16th-note step being played, counted from the start of the loop (preview) or from Start
    /// (clock), or null when nothing is playing. Take it modulo a part's length to get its step.
    /// </summary>
    public long? CurrentStep
    {
        get
        {
            if (_preview is { IsRunning: true } preview)
            {
                var ticks = preview.GetCurrentTime<MidiTimeSpan>().TimeSpan;
                // During a fill the main pattern isn't playing, so there is no step of it to show.
                return ticks >= _loopTicks ? null : ticks / PatternMidiBuilder.TicksPerStep;
            }
            return Clock.IsRunning ? Clock.CurrentStep : null;
        }
    }

    /// <summary>True when a preview was paused with <see cref="PausePreview"/> and can be resumed.</summary>
    public bool IsPreviewPaused => _preview is { IsRunning: false };

    /// <summary>Pauses the preview where it is; <see cref="ResumePreview"/> carries on from there.</summary>
    public void PausePreview()
    {
        if (_preview is not { IsRunning: true } preview)
            return;
        preview.Stop();
        _paused = true;
        AllNotesOff();
    }

    public void ResumePreview()
    {
        if (_preview is { IsRunning: false } preview)
        {
            _paused = false;
            preview.Start();
        }
    }

    public void StopPreview()
    {
        _jumpPending = false;
        if (_preview is null)
            return;
        _preview.Stop();
        _preview.Dispose();
        _preview = null;
        AllNotesOff();
    }

    /// <summary>All Sound Off (CC 120) and All Notes Off (CC 123) on both channels; the T-8 recognizes both.</summary>
    public void AllNotesOff()
    {
        foreach (var channel in new[] { Map.BassChannel, Map.RhythmChannel })
        {
            Send(new ControlChangeEvent((SevenBitNumber)120, SevenBitNumber.MinValue), channel);
            Send(new ControlChangeEvent((SevenBitNumber)123, SevenBitNumber.MinValue), channel);
        }
    }

    private void Send(ChannelEvent midiEvent, int channel)
    {
        // A part switched OFF (like bACh/rhCh OFF on the T-8) gets nothing.
        if (channel == T8MidiMap.Off)
            return;
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
