using T8.Core.Songs;

namespace T8.Midi;

/// <summary>
/// Plays a song on the T-8's own sequencer with the computer as clock master. Before starting it
/// selects the first pattern; then, as each pass begins, it sends the program change for the pass
/// after it, which the T-8 queues ("up next") and switches to when the current pattern ends.
/// Needs rXPc on and SYnC set to AUTO or USB on the T-8.
/// </summary>
public sealed class SongSyncDriver : IDisposable
{
    private readonly T8Controller _t8;
    private readonly SongPlan _plan;
    private readonly bool _loop;
    private readonly int _offset;
    private int _lastPass = -1;
    private volatile bool _finished;

    /// <param name="fromPass">Pass to start from.</param>
    public SongSyncDriver(T8Controller t8, SongPlan plan, bool loop, int fromPass = 0)
    {
        _t8 = t8;
        _plan = plan;
        _loop = loop;
        _offset = fromPass > 0 && fromPass < plan.Passes.Count ? plan.Passes[fromPass].StartStep : 0;
    }

    /// <summary>The song step being played, from the clocks sent, or null before the first clock.</summary>
    public long? CurrentStep => _t8.Clock.CurrentStep is { } step ? step + _offset : null;

    /// <summary>True once a song without loop has played its last pass.</summary>
    public bool Finished => _finished;

    public void Start(double bpm)
    {
        if (_plan.Passes.Count == 0)
            return;
        var first = _plan.Passes[_plan.PassAt(_offset)];
        // While stopped, the T-8 switches pattern straight away.
        _t8.SelectPattern(first.Bank, first.Pattern);
        _t8.Clock.Bpm = bpm;
        // Each step boundary is handled on the clock thread as the clock reaches it, so no pass is missed.
        _t8.Clock.StepStarted += OnStepStarted;
        _t8.Clock.Start();
    }

    public void Stop()
    {
        _t8.Clock.StepStarted -= OnStepStarted;
        _t8.Clock.Stop();
    }

    private void OnStepStarted(long clockStep) => OnStep(clockStep + _offset);

    /// <summary>At each step: queues the next pattern when a pass starts, and stops at the end of the song.</summary>
    internal void OnStep(long step)
    {
        if (_finished)
            return;
        if (!_loop && step >= _plan.TotalSteps)
        {
            // Report finished only once Stop has been sent.
            Stop();
            _finished = true;
            return;
        }

        var index = _plan.PassAt(step);
        if (index == _lastPass)
            return;
        _lastPass = index;

        var next = index + 1 < _plan.Passes.Count ? index + 1 : _loop ? 0 : -1;
        if (next < 0)
            return;
        var current = _plan.Passes[index];
        var upcoming = _plan.Passes[next];
        if (upcoming.Bank != current.Bank || upcoming.Pattern != current.Pattern)
            _t8.SelectPattern(upcoming.Bank, upcoming.Pattern);
    }

    public void Dispose() => Stop();
}
