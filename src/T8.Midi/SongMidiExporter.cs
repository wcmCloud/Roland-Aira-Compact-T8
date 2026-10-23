using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using T8.Core.Patterns;
using T8.Core.Songs;

namespace T8.Midi;

public enum SongMidiMode { Programs, Notes }

/// <summary>One complete arrangement, including repeats, fills and its trailing silence.</summary>
public sealed class SongMidiExporter
{
    public T8MidiMap Map { get; init; } = new();
    public int? Seed { get; init; }

    public MidiFile Build(Song song, PatternLibrary library, double bpm, SongMidiMode mode)
    {
        var errors = song.Validate();
        if (errors.Count > 0) throw new ArgumentException(errors[0]);
        if (song.Entries.Count == 0) throw new ArgumentException("Add a song entry first.");
        var plan = SongPlan.Create(song, library);
        var events = new List<TimedEvent>();
        var builder = new PatternMidiBuilder { Map = Map, Seed = Seed };
        for (var i = 0; i < plan.Passes.Count; i++)
        {
            var pass = plan.Passes[i];
            long offset = (long)pass.StartStep * PatternMidiBuilder.TicksPerStep;
            if (pass.Repeat == 1 || pass.IsFill)
                events.Add(new TimedEvent(new MarkerEvent(song.Entries[pass.Entry].Name + (pass.IsFill ? " (fill)" : "")), offset));
            if (mode == SongMidiMode.Notes)
            {
                foreach (var e in builder.Build(library.Bass[pass.Bank - 1, pass.Pattern - 1], library.Rhythm[pass.Bank - 1, pass.Pattern - 1], pass.Steps))
                {
                    e.Time += offset;
                    events.Add(e);
                }
            }
            else if (Map.ProgramChangeChannel != T8MidiMap.Off && (i == 0 || pass.Bank != plan.Passes[i - 1].Bank || pass.Pattern != plan.Passes[i - 1].Pattern))
            {
                // Select the first while stopped; queue later changes just after the previous pass starts.
                var time = i == 0 ? 0 : ((long)plan.Passes[i - 1].StartStep + Math.Min(1, plan.Passes[i - 1].Steps - 1)) * PatternMidiBuilder.TicksPerStep;
                events.Add(new TimedEvent(new ProgramChangeEvent((SevenBitNumber)T8MidiMap.ProgramNumber(pass.Bank, pass.Pattern))
                    { Channel = (FourBitNumber)(Map.ProgramChangeChannel - 1) }, time));
            }
        }
        events.Add(new TimedEvent(new MarkerEvent("Song end"), (long)plan.TotalSteps * PatternMidiBuilder.TicksPerStep));
        var file = new MidiFile(events.OrderBy(e => e.Time).ThenBy(e => e.Event is NoteOnEvent ? 1 : 0).ToTrackChunk())
            { TimeDivision = new TicksPerQuarterNoteTimeDivision(PatternMidiBuilder.TicksPerQuarterNote) };
        file.ReplaceTempoMap(PatternMidiBuilder.CreateTempoMap(bpm));
        return file;
    }
}
