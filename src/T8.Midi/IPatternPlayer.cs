using Melanchall.DryWetMidi.Core;
using T8.Core.Patterns;
using T8.Core.Songs;
namespace T8.Midi;

/// <summary>Common transport for hardware MIDI and the local audio preview.</summary>
public interface IPatternPlayer : IDisposable
{
    bool IsOffline { get; }
    T8MidiMap Map { get; }
    MidiClockSender Clock { get; }
    long? CurrentStep { get; }
    bool PreviewFinished { get; }
    bool IsSongJumpPending { get; }
    void PlayPreview(BassPattern? bass, RhythmPattern? rhythm, double bpm, bool loop = true, int? seed = null, RhythmPattern? fill = null);
    void PlaySong(SongPlan plan, PatternLibrary library, double bpm, bool loop, int fromPass = 0);
    void QueueSongJump(long atStep, long toStep);
    bool QueueFill();
    void PausePreview();
    void ResumePreview();
    void StopPreview();
    void AllNotesOff();
    void TriggerDrum(RhythmTrack track, int velocity = 100);
    void BassNoteOn(int note, int velocity = 100);
    void BassNoteOff(int note);
    void SelectPattern(int bank, int pattern);
    void SendExternalEvent(MidiEvent midiEvent);
}
