using T8.Core.Patterns;

namespace T8.Editor.ViewModels;

/// <summary>Why <see cref="PatternHistory.Changed"/> fired.</summary>
public enum PatternHistoryChange
{
    Edit,
    Undo,
    Redo,
    Cleared,
    Saved,
}

/// <summary>
/// Undo and redo for the open pattern library. One step is one edit: a cell, a cleared row, a
/// paste, a transpose, or a generator run, including ones that write other patterns. Nested edits
/// join the outermost step, so a transpose is one step rather than one per note.
/// </summary>
public sealed class PatternHistory
{
    /// <summary>How many steps are kept. Older ones drop off the back.</summary>
    public const int Limit = 100;

    private readonly Func<PatternLibrary> _library;
    private readonly LinkedList<Entry> _undo = new();
    private readonly LinkedList<Entry> _redo = new();
    private int _depth;
    private object _token = new();
    private object _clean;

    public PatternHistory(Func<PatternLibrary> library)
    {
        _library = library;
        _clean = _token;
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>True when the library differs from the last save, open, or new.</summary>
    public bool IsDirty => !ReferenceEquals(_token, _clean);

    public event Action<PatternHistoryChange>? Changed;

    /// <summary>Runs <paramref name="change"/> on its own when there is no history (tests).</summary>
    public static void Apply(PatternHistory? history, Action change)
    {
        if (history is null)
            change();
        else
            history.Edit(change);
    }

    /// <summary>Runs a change to the library. Nested calls join the step that is already open.</summary>
    public void Edit(Action change)
    {
        if (_depth > 0)
        {
            change();
            return;
        }

        var before = Capture();
        _depth++;
        try
        {
            change();
        }
        finally
        {
            _depth--;
            if (!Same(before, _library()))
            {
                Push(_undo, new Entry(_token, before));
                _token = new object();
                _redo.Clear();
                Changed?.Invoke(PatternHistoryChange.Edit);
            }
        }
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;
        var entry = Pop(_undo);
        Push(_redo, new Entry(_token, Capture()));
        entry.Snapshot.Restore(_library());
        _token = entry.Token;
        Changed?.Invoke(PatternHistoryChange.Undo);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;
        var entry = Pop(_redo);
        Push(_undo, new Entry(_token, Capture()));
        entry.Snapshot.Restore(_library());
        _token = entry.Token;
        Changed?.Invoke(PatternHistoryChange.Redo);
    }

    /// <summary>Drops the stacks. The library as it is now is the clean one (new or open).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _token = new object();
        _clean = _token;
        Changed?.Invoke(PatternHistoryChange.Cleared);
    }

    /// <summary>The library as it is now matches what was saved.</summary>
    public void MarkSaved()
    {
        _clean = _token;
        Changed?.Invoke(PatternHistoryChange.Saved);
    }

    private LibrarySnapshot Capture() => LibrarySnapshot.Capture(_library());

    private static void Push(LinkedList<Entry> stack, Entry entry)
    {
        stack.AddLast(entry);
        while (stack.Count > Limit)
            stack.RemoveFirst();
    }

    private static Entry Pop(LinkedList<Entry> stack)
    {
        var entry = stack.Last!.Value;
        stack.RemoveLast();
        return entry;
    }

    private static bool Same(LibrarySnapshot snapshot, PatternLibrary library)
    {
        for (var b = 0; b < PatternLibrary.BankCount; b++)
        {
            for (var p = 0; p < PatternLibrary.PatternsPerBank; p++)
            {
                if (!BassSame(snapshot.Bass[b, p], library.Bass[b, p]))
                    return false;
                if (!RhythmSame(snapshot.Rhythm[b, p], library.Rhythm[b, p]))
                    return false;
            }
        }
        return true;
    }

    private static bool BassSame(BassPattern saved, BassPattern live)
    {
        if (saved.Length != live.Length || saved.Triplet != live.Triplet)
            return false;
        for (var i = 0; i < BassPattern.StepCount; i++)
        {
            var a = saved.Steps[i];
            var b = live.Steps[i];
            if (a.State != b.State || a.Note != b.Note || a.Accent != b.Accent || a.Slide != b.Slide)
                return false;
        }
        return true;
    }

    private static bool RhythmSame(RhythmPattern saved, RhythmPattern live)
    {
        if (saved.Length != live.Length || saved.Scale != live.Scale || saved.Shuffle != live.Shuffle || saved.Flam != live.Flam)
            return false;
        for (var s = 0; s < RhythmPattern.StepCount; s++)
        {
            for (var t = 0; t < RhythmPattern.TrackCount; t++)
            {
                var a = saved.Steps[s].Cells[t];
                var b = live.Steps[s].Cells[t];
                if (a.On != b.On || a.Velocity != b.Velocity || a.SubStep != b.SubStep
                    || a.Probability != b.Probability || a.SubStepProbability != b.SubStepProbability)
                    return false;
            }
        }
        return true;
    }

    private sealed record Entry(object Token, LibrarySnapshot Snapshot);

    private sealed class LibrarySnapshot
    {
        private readonly BassPattern[,] _bass = new BassPattern[PatternLibrary.BankCount, PatternLibrary.PatternsPerBank];
        private readonly RhythmPattern[,] _rhythm = new RhythmPattern[PatternLibrary.BankCount, PatternLibrary.PatternsPerBank];

        public BassPattern[,] Bass => _bass;
        public RhythmPattern[,] Rhythm => _rhythm;

        public static LibrarySnapshot Capture(PatternLibrary library)
        {
            var snapshot = new LibrarySnapshot();
            for (var b = 0; b < PatternLibrary.BankCount; b++)
            {
                for (var p = 0; p < PatternLibrary.PatternsPerBank; p++)
                {
                    var bass = new BassPattern();
                    bass.CopyFrom(library.Bass[b, p]);
                    snapshot._bass[b, p] = bass;
                    var rhythm = new RhythmPattern();
                    rhythm.CopyFrom(library.Rhythm[b, p]);
                    snapshot._rhythm[b, p] = rhythm;
                }
            }
            return snapshot;
        }

        public void Restore(PatternLibrary library)
        {
            for (var b = 0; b < PatternLibrary.BankCount; b++)
            {
                for (var p = 0; p < PatternLibrary.PatternsPerBank; p++)
                {
                    library.Bass[b, p].CopyFrom(_bass[b, p]);
                    library.Rhythm[b, p].CopyFrom(_rhythm[b, p]);
                }
            }
        }
    }
}
