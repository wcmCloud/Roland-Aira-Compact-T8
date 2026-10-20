# Song mode in a DAW

**Status:** planned · **Goal:** use the editor's songs from a DAW (Ableton, Logic, Reaper, Cubase, Bitwig…)

## Options compared

| | Effort | Works in | Notes |
|---|---|---|---|
| A. Export song as MIDI | ~1 hour + tests | Every DAW | Recommended first step |
| B. Editor follows the DAW's clock | ~half a day | Every DAW that sends MIDI clock | No plugin; the Song tab plays in sync |
| C. VST3/AU plugin (JUCE, C++) | Several days | VST3/AU hosts | Separate project; can't reuse the .NET/Avalonia UI |

## A. Export song as MIDI (recommended first)

A **Export song as MIDI…** button on the Song tab writes a `.mid` file to drag onto a DAW track routed to the T-8.
Two kinds:

1. **Pattern changes only.** A program change on Pc.Ch for each pattern, timed so the T-8 switches at the right
   moment: sent one pass ahead, as the sync driver does (the T-8 queues a program change and switches when the
   current pattern ends), fills included. The T-8 plays its own stored patterns, following the DAW's clock
   (`SYnC` USB, `rXPc` on). Keeps the T-8's own sequencer features (step probability, live variation).
2. **Full notes.** The whole song as drum notes on rhythm and bass notes on bass (respecting OFF channels), like
   the preview. The DAW plays the T-8's sounds and every note is editable in the DAW.

How:
- [ ] `SongMidiExporter` in `T8.Midi`: build from `SongPlan` (passes and steps) at the song's BPM, 96 PPQ like
      `PatternMidiBuilder`. Full notes reuses `PatternMidiBuilder.Build` per pass (as `T8Controller.PlaySong` does).
      Pattern changes: for pass *k* put the program change for pass *k* at the start of pass *k−1* plus one step
      (the first one at time 0), skipping repeats of the same pattern.
- [ ] Optional markers (meta events) at each entry with its name, so the DAW shows the arrangement.
- [ ] Song tab: **Export as MIDI…** with a choice (pattern changes / full notes), file picker (`.mid`).
- [ ] CLI: `t8 export-song <folder> <song.t8song> <file.mid> [--mode programs|notes]`.
- [ ] Tests: event times and program numbers for a song with repeats and fills; notes mode matches the preview;
      OFF parts left out; tempo in the file.
- [ ] README: "Using a song in a DAW" with the T-8 settings (SYnC USB, rXPc on) and routing tips per DAW.

## B. Editor follows the DAW's clock (no plugin)

The DAW sends MIDI clock/start/stop to a virtual port (IAC Driver on macOS, loopMIDI on Windows); the editor listens
and plays the Song tab in sync (preview notes, or program changes to the T-8).

- [ ] "Clock source: internal / external (MIDI input …)" in MIDI settings.
- [ ] Reuse `T8InputListener`'s clock handling for any input port: Start resets the position, Continue resumes,
      Stop stops, 6 clocks per step; follow tempo changes.
- [ ] Drive the song position from the received clocks (the sync driver already works per step), for both play
      modes; Song Position Pointer support would let the DAW start mid-song.
- [ ] Tests with a fake input sending clock.

## C. VST3/AU plugin (separate project)

Plugins are native code loaded by the DAW; .NET/Avalonia can't be used for a cross-platform plugin with a UI
(VST.NET only does VST2, which Steinberg no longer licenses for new plugins; AudioPlugSharp is Windows-only).

- Framework: **JUCE** (C++), one codebase for VST3, AU and standalone. Check the licence (AGPLv3 or JUCE's free
  tier with its revenue cap).
- A **MIDI effect** plugin: reads a `.t8song` (JSON, documented in `docs/SONG-FORMAT.md`) plus the pattern
  lengths (from a T-8 backup folder, or entered by hand), follows the host's transport (PPQ position, tempo,
  play/stop) and outputs program changes (and optionally notes) at the right times.
- UI: song list with repeats/fills and a timeline, rebuilt in JUCE.
- Port: `SongPlan` (small), the `.t8song` reader, the program change timing from option A.
- Host caveats: Ableton, Reaper, Bitwig and Cubase route plugin MIDI output to hardware; Logic needs the External
  Instrument / IAC workaround for MIDI FX output.
- Distribution: sign and notarize (macOS), installer for VST3/AU folders, CI builds per platform.

## Order

1. A (export) — quick, works everywhere, and its timing code is reused by B and C.
2. B (clock follow) — if playing the Song tab live alongside a DAW is wanted.
3. C (plugin) — only if A and B aren't enough; a separate repository.
