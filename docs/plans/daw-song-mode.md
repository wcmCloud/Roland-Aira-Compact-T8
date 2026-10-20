# Song mode in a DAW

**Status:** parked — **next step** · **Priority: the VST3/AU plugin (option C) first** · **Goal:** use the editor's songs from a
DAW (Ableton, Logic, Reaper, Cubase, Bitwig…)

## Options compared

| | Effort | Works in | Notes |
|---|---|---|---|
| **C. VST3/AU plugin (JUCE, C++)** | **Several days** | **VST3/AU hosts** | **Priority.** Separate project; can't reuse the .NET/Avalonia UI |
| A. Export song as MIDI | ~1 hour + tests | Every DAW | Quick extra; its timing rules are shared with the plugin |
| B. Editor follows the DAW's clock | ~half a day | Every DAW that sends MIDI clock | No plugin; the Song tab plays in sync |

## A. Export song as MIDI

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

## C. VST3/AU plugin (priority)

Plugins are native code loaded by the DAW; .NET/Avalonia can't be used for a cross-platform plugin with a UI
(VST.NET only does VST2, which Steinberg no longer licenses for new plugins; AudioPlugSharp is Windows-only). So
the plugin is a new C++ project, with the song logic ported from this repository.

### What it does

A **MIDI effect** plugin, **Setlist T-8**, on a track routed to the T-8:

- Loads a `.t8song` file (JSON, [docs/SONG-FORMAT.md](../SONG-FORMAT.md)) or edits the song in the plugin.
- Follows the host's transport: play/stop, tempo, and the bar/beat position (PPQ), so the song starts, stops,
  loops and jumps with the DAW's timeline.
- **Pattern-change mode** (main use): outputs the program change for each pattern on Pc.Ch, one pass ahead,
  exactly as `SongSyncDriver` does, so the T-8 plays its own patterns in sync (T-8 `SYnC` USB, `rXPc` on; the
  DAW sends MIDI clock to the T-8).
- **Notes mode** (later): outputs the drum and bass notes of each pass, like the editor's preview, using the
  pattern files from a T-8 backup folder.
- Pattern lengths (steps per pass) come from a T-8 backup folder the user points it at, or are typed per entry.
- State (song, settings) is saved in the DAW project.

### Plan

1. **Project setup**
   - [ ] New repository `Setlist-T8` (under wcmCloud), JUCE 8 with CMake, targets VST3, AU (macOS) and Standalone.
         Plugin name in DAWs: "Setlist T-8"; vendor: wcmCloud.
   - [ ] Licence check: JUCE's free tier vs AGPLv3; pick the plugin's own licence.
   - [ ] GitHub Actions: build macOS (universal), Windows; artifacts per format.
2. **Song core (C++ port)**
   - [ ] `Song`/`SongEntry`/`SongFill` and the `.t8song` reader/writer (JSON via `juce::JSON`), same validation.
   - [ ] `SongPlan` (passes, `PassAt`, `FirstPassOf`) and pattern length lookup.
   - [ ] PRM reader for rhythm `LENGTH` (and the step data for notes mode later).
   - [ ] Unit tests (JUCE's `UnitTest` or Catch2) mirroring `SongTests` and `SongPlaybackTests`.
3. **MIDI output from the host's timeline**
   - [ ] In `processBlock`: read `AudioPlayHead` (isPlaying, ppqPosition, bpm); a 16th-note step is 0.25 PPQ.
   - [ ] For each block, find the pass boundaries inside it and emit program changes sample-accurately
         (one pass ahead, skipping repeats of the same pattern; the first pattern on start).
   - [ ] Handle start mid-song (position → pass), loops and jumps in the DAW (re-sync on discontinuity), stop
         (optionally All Notes Off), tempo changes.
   - [ ] Settings: Pc.Ch, loop song, start offset (song starts at bar N).
4. **UI (JUCE)**
   - [ ] The same look as the editor: dark cards, the song list with repeats/fills/colours, and the colour timeline
         with the playing part and progress.
   - [ ] Load/save `.t8song`, choose the T-8 backup folder, click a timeline block to queue the next part (when the
         DAW is playing).
5. **Hosts and distribution**
   - [ ] Test: Ableton Live, Reaper, Bitwig, Cubase (VST3 MIDI out to hardware); Logic (AU MIDI FX output needs the
         IAC Driver or External Instrument workaround; document it).
   - [ ] macOS: sign and notarize, a `.pkg` installing to `/Library/Audio/Plug-Ins/VST3` and `Components`.
   - [ ] Windows: installer for `Common Files/VST3`.
   - [ ] README with setup per DAW and the T-8 settings.
6. **Later**
   - [ ] Notes mode (play the patterns through the T-8's sounds from the DAW).
   - [ ] Pattern-change export from the editor (option A) can then share the timing rules.

### Open decisions

- ~~Plugin name~~ — decided: **Setlist T-8**. "T-8" is Roland's model name, so in store listings and the
  description present it as compatibility ("Setlist T-8 — song mode for the Roland AIRA Compact T-8, not affiliated
  with Roland"), and use no Roland logos.
- JUCE licence tier.
- Pattern lengths: require a backup folder, or let the song file carry the lengths (a small `.t8song` v2 addition:
  `steps` per entry, written by the editor)?

## Order

1. **C (plugin)** — the priority; a separate repository.
2. A (export) — quick extra for DAWs where the plugin can't send MIDI to hardware easily (Logic).
3. B (clock follow) — if playing the editor's Song tab live alongside a DAW is still wanted.
