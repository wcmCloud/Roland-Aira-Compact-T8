# Offline Mode

**Status:** In progress in the working tree on `master` (not committed). The choices below are already in those files. Do not start a second design.  
**Goal:** Compose and audition patterns and songs on a laptop without the T-8, including while it is in USB drive mode. Keep the T-8 as the reference sound and final hardware destination.

## User experience

Add a global **Output: Auto / T-8 / Offline** selector to the Playback card, shared by Rhythm, Bass and Song. Keep the existing hardware Play mode (preview notes / run sequencer) as a separate setting, applicable only when the effective output is T-8.

| Selection | Behaviour |
|---|---|
| Auto (default) | Use the selected usable MIDI output; use Offline when none is available or the T-8 is in drive mode. Show the effective output prominently. |
| T-8 | Require the selected MIDI output. Explain unavailable output; never silently substitute local audio. |
| Offline | Always use computer audio, even with a T-8 connected. Remember the choice. |

Preserve existing selection of non-T-8 MIDI outputs; do not detect availability from device name alone or require a MIDI reply from devices that do not provide one. A successful port open does not prove audible hardware output: provide manual Offline selection for that case.

- Offline controls: **Preset**, **Volume**, **Audio output**, and a compact Bass panel with square/saw, cutoff, resonance, decay and slide time. Start with a conservative master level.
- Step clicks, the bass keyboard, Play/Pause/Stop, fills, playhead, generators and Song playback work locally. Respect Audition and Show playing step preferences.
- Show **Offline · Classic Analog** and a brief explanation that the sounds are a writing preview. No DAW or network connection required.
- Stop and silence the active player before changing output. On MIDI loss during playback, stop, retain edits, resolve Auto to Offline, and let the user press Play again. Do not silently resume or jump outputs mid-song.
- Reconnection must not interrupt local playback. In Auto, use hardware on the next explicit Play after stopping; explicit Offline stays local.
- Offline mode suppresses outbound notes, clocks, program changes and selection-follow messages, plus T-8 input-follow behaviour. Clean up the previous hardware session before activating it. MIDI settings and connection testing remain explicitly hardware operations.
- Audio device loss stops playback with a useful status and a device-selection/retry action. Failure to open local audio must never unexpectedly start hardware playback.

## Six bundled sound presets

Interpret six variations as **six complete drum-and-bass presets**, not six individual drum sounds. Each includes kick, snare, tom, clap, closed hat and open hat, with distinct sample choices and bass settings. They are starting sound designs, subject to listening tests.

| Preset | Drum character | Bass starting point |
|---|---|---|
| Classic Analog (default) | Balanced, dry 808-style hits | Saw, moderate filter, short slide |
| Deep Electro | Longer, deeper kick; softer hats | Filtered square, rounded envelope |
| Tight Machine | Short kick/tom and crisp snare/clap | Square, short decay, quick slide |
| Bright Acid | Sharp hats and snare, firm kick | Saw, resonant filter, audible slide |
| Warm Tape | Darker hits with gentle saturation | Dark square, soft attack |
| Gritty Club | Punchy hits with controlled drive | Saw, brighter envelope, stronger accent |

The drums are Michael Fischer's TR-808 samples, hosted by [TidalCycles](https://github.com/tidalcycles/sounds-tr808-fischer) under CC0-1.0. The working tree pins revision `85fbecf1bec32553395625ea659e2a56dfd7c0e1`. Filenames, source paths and SHA-256 hashes are in `src/T8.Audio/Samples/manifest.json`. The original readme and the CC0 text ship beside the app. These are six curated kits, not six different machines and not a T-8 emulation.

Every kit uses the same clap (`CP.WAV`) and closed hat (`CH.WAV`). The other four voices differ:

| Preset | Kick | Snare | Tom | Open hat | Bass |
|---|---|---|---|---|---|
| Classic Analog | BD5050 | SD5050 | LT50 | OH50 | Saw, cutoff 1100, decay 0.25, slide 0.075 |
| Deep Electro | BD0010 | SD0025 | LT00 | OH10 | Square, cutoff 500, longer decay and slide |
| Tight Machine | BD7500 | SD7500 | LT75 | OH00 | Square, short decay, quick slide |
| Bright Acid | BD7550 | SD7575 | LT25 | OH75 | Saw, resonant filter, audible slide |
| Warm Tape | BD2525 | SD2525 | LT50 | OH25 | Dark square, softer tone |
| Gritty Club | BD1010 | SD1010 | LT10 | OH50 | Saw, more drive |

Knob names follow Fischer's readme: for a two-knob sound the first pair is tone or tuning and the second is decay or snappy (`BD5050` is both knobs at the middle). A trailing `10` is the maximum, not 1.0. A listening pass is still required before release. The samples are bundled; nothing is downloaded on first run.

Use the existing project-licensed code for an original simple bass synthesizer. A real-time oscillator supports smooth slides and arbitrary notes without a bass sample library. Bundle all selected audio; no first-run download, plugin installation or SoundFont is needed. Aim for less than 15 MB of added sound assets, validated after curation.

## Model compatibility and sound scope

- Hardware mapping is authoritative: `BD` kick, `SD` snare, `LT` tom, `HT` clap, `CH` closed hat, `OH` open hat. `AC` controls accent and is not a voice. Preserve `CY` data but do not invent a new audible instrument: it is absent from the default hardware mapping.
- Offline instrument identity must be independent of customized MIDI note numbers. Retain existing Bass/Rhythm OFF semantics, clearly shown as muted parts locally.
- Implement velocity, accent, probability, substeps, substep probability, flam, shuffle and independent bass/rhythm lengths using the established preview rules. Their hardware timing remains approximate. Do not guess new semantics for unconfirmed Scale/Triplet values.
- Bass: band-limited square/saw oscillator, amplitude/filter envelopes, stable resonant low-pass filter, accent, rests, ties and explicit legato glide. Define and test ties/slides at pattern wrap, song pass transitions and repeated same pitches. A delayed note-off must not cut off a newer note.
- Drum one-shots retain their decay beyond a short MIDI gate. Closed hats choke open hats with a short fade; bound polyphony and use click-free voice stealing.
- Preview sounds and controls never modify PRM data or become hardware patch settings. Save the chosen preset and tweaks in editor settings for v1, globally rather than per pattern. No `.t8song` format change.
- The existing Save and Write to T-8 workflow transfers pattern data through USB drive mode. Sounds stay on the laptop; the hardware plays its own voices. MIDI export remains musical data, not rendered audio.

## Already in the working tree

Uncommitted on `master`. `follow-external-clock` is a separate worktree and is not part of this work.

- `src/T8.Audio`: sample bank, six presets, bass synth and drum mixer in `AudioRenderer`, and a miniaudio device (`Hexa.NET.MiniAudio` 1.0.1) behind `IAudioDevice`. Samples are embedded. Licence files are copied to the output.
- `IPatternPlayer` is the shared transport. `T8Controller` stays the hardware player. `OfflinePlayer` is the laptop player. It still builds events with `PatternMidiBuilder`, then maps the canonical drum notes (36, 38, 47, 50, 42, 46) onto BD, SD, LT, HT, CH and OH. A user's MIDI remap does not change those identities. Bass is a square or saw oscillator, not a sample.
- Playback card: **Auto / T-8 / Offline**, then preset, volume, audio output and bass wave, cutoff, resonance, decay and slide. The choice and the tweaks are saved in editor settings. Auto uses Offline when no MIDI output is selected, the port will not open, or the T-8 is in drive mode. Explicit T-8 never substitutes laptop audio.
- Pattern play, audition, pause, fill, playhead and song play go through that player. Offline sends no notes, clock or program changes, and it ignores pattern changes coming back from the T-8. Select on T-8 is disabled. Follow DAW MIDI clock in notes mode plays the song through the laptop and does not forward clock. A missing clock for about two seconds silences held notes.
- Tests: `tests/T8.Audio.Tests` (kits, timing, fills, jumps, bass) and `tests/T8.Editor.Tests/OfflineModeTests.cs` (routing, settings, the song clock, the six presets in the window).

## Still open

1. Listen to all six kits on speakers or headphones and adjust the sample choice, tone, drive or bass settings if two kits are too similar or too loud. The automated tests only check that each kit makes a finite, distinct signal.
2. Confirm the miniaudio native library is present for macOS, Windows and Linux, on x64 and ARM64, in a published build. Record any target that was not run.
3. Write the in-app help and a short README section: Offline is a writing preview, Save and Write to T-8 still move pattern data only, and the T-8 plays its own sounds afterwards.
4. Manual pass: no T-8 connected, then the T-8 in drive mode, then switch back to T-8 output. Include device unplug, sleep and leaving the app. Do not add a second free-running clock beside Follow DAW MIDI clock.
5. Commit this working tree on its own, without the `follow-external-clock` worktree.

Leave the event compiler inside `PatternMidiBuilder` unless a regression shows the offline timing disagreeing with MIDI preview. A separate compiler in `T8.Core` is not required for the first version.

## Implementation architecture

Current integration points: `PatternMidiBuilder`, `T8Controller`, `ExternalSongPlayer`, `SongPlan`, `MainWindowViewModel`, `SongViewModel` and `EditorSettings`. Pattern audition currently requires a selected MIDI port; Song playback also depends on the hardware controller.

1. Add a device-independent event compiler, preferably in `T8.Core`, extracting the musical timing rules from `PatternMidiBuilder`. Emit logical instrument events and explicit bass gate/slide state. Keep a MIDI adapter and retain existing MIDI ordering/timing through regression tests. Avoid decoding customized MIDI numbers into internal instruments.
2. Introduce a playback service consumed by both view models: transport, position, audition, fill queuing, song plan and disposal. Provide hardware and offline implementations; keep the existing T-8 sequencer driver intact behind hardware routing.
3. Add `T8.Audio`: sample bank, drum mixer, mono bass synth, limiter and an audio-device abstraction. Prefer [miniaudio](https://github.com/mackron/miniaudio) via the MIT-licensed [Hexa.NET.MiniAudio](https://github.com/HexaEngine/Hexa.NET.MiniAudio) binding, subject to a packaging spike. Confirm native binaries for all six existing targets (macOS/Windows/Linux, x64/ARM64), .NET 10 publishing, signing and native lifetime behaviour. If a target is absent, build and package the pinned native library; do not assume NuGet covers every RID.
4. Schedule local events against the audio sample counter, using fractional position accumulation to avoid tempo drift. UI timers only display position. Preload/decode samples off the callback, use bounded queues and preallocated voices, and avoid callback locks, allocations, file I/O or logging. Keep managed callback delegates alive throughout native device lifetime.
5. Convert/resample assets at load time for the active device rate. Target 44.1/48 kHz and 256-frame buffers initially; permit a larger buffer for unreliable devices. Aim for wired audition latency below 20 ms on supported test machines, measured rather than guaranteed; Bluetooth adds its own latency.
6. Apply pattern edits as immutable snapshots at the next step boundary; preserve transport phase. Queue fills at the existing loop boundary. Apply preset changes safely at a boundary without disrupting transport; smooth live gain/filter changes.
7. For Song mode reuse `SongPlan`, repeats, fills, entry seeking and loop choices. Support the existing DAW clock input with offline audio as output: translate incoming clock to audio scheduling, handle Start/Stop/Continue/SPP, and disable hardware clock forwarding. This needs a clock adapter rather than an independent free-running song clock. Keep existing restrictions on DAW-controlled seeking and entry loops.
8. Persist output preference, preset ID, volume, bass tweaks and audio-device preference. Migrate old settings with Auto as default and preserve the hardware Play mode. Invalid preset/device selections get documented safe defaults.

## Delivery sequence

1. **Audio and assets spike:** validate six native targets, audition and select samples, record licences/provenance, prove kick plus sliding bass and measure latency. This settles the two main uncertainties before broad refactoring.
2. **Shared events and renderer:** extract timing with regression coverage; implement offline rendering and synthesis with deterministic tests.
3. **Pattern integration:** output routing, audition, transport, fills, edits during playback, six presets and settings migration.
4. **Song integration:** internal transport and DAW clock, repeats/fills/loops/seeking; confirm hardware paths remain unchanged.
5. **Release readiness:** package assets/native libraries on every target, listening and device tests, update README and in-app help with offline behaviour and hardware transfer limits.

The first version is the working tree described above. Audio export, user sample import, per-pattern sound automation, effects racks and an exact T-8 model stay future work.

## Required tests and acceptance criteria

Add `T8.Audio.Tests` and extend Core, MIDI and Editor suites. Use a fake device and deterministic offline render path in CI so most tests need no physical hardware.

- Timing: every rhythm attribute, mismatched lengths, probability endpoints and seeded rolls, tempo changes, long-run drift, buffer-boundary events and fill transitions. Explicitly decide probability reroll boundaries and preserve existing behaviour during extraction.
- Bass: pitch accuracy, rest/gate/release, accent, square/saw distinction, ties, slides across loop boundaries, same-note overlaps and stale note-offs. Check finite/bounded output under extreme filter settings.
- Drums/mixer: all six voices for each of six presets, AC behaviour, hat choke, overlapping hits, decay tails, voice limits, clipping protection, missing/corrupt assets and sample-rate changes. Use numerical invariants/tolerances rather than brittle whole-waveform hashes.
- Routing: Auto with no port and with drive mode; explicit Offline with hardware present; explicit T-8 failure; disconnect/reconnect while playing/paused; no outbound MIDI or unwanted T-8 follow while offline; no duplicate transport owners.
- UI/settings: audition, pause/resume, stop, playhead, fill, generation, pattern selection, preset persistence, old settings migration and invalid device/preset recovery.
- Song: repeats, fills, once/whole-song/entry loops, seeking, tab changes, external Start/Stop/Continue/SPP, clock loss and no simultaneous internal/external clock. Clock-loss handling must silence held notes and expose waiting/stopped state.
- Data/regressions: opening, saving, exporting MIDI and writing PRM preserve existing musical content; preview preferences never enter hardware files. Existing MIDI tests must continue to pass.
- Packaging: every target includes assets, licence manifest and native audio library; start and play with networking disabled. Test real output on each OS and architecture where runners/hardware are available; record any untested targets explicitly.
- Listening/manual acceptance: no T-8 connected → open a set, audition, edit, play a fill and song, save; repeat in drive mode; switch to hardware and compare musical timing. Verify six presets are distinct, balanced, free of clicks and usable through headphones. Confirm device loss, sleep/wake, shutdown and repeated preset/output changes release resources.

Done means all supported workflows pass, the full relevant automated suite is green, and measured audio/device checks are recorded. Tests alone cannot certify sound quality or physical output latency.
