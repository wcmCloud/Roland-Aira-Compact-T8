# T-8 MIDI

From the MIDI Implementation Chart (manual p.31). The T-8 has **no Control Change and no SysEx**, so knobs can't be set over MIDI and patterns can only be transferred through the USB drive (see `PRM-FORMAT.md`).

| | Default channel | Notes |
|---|---|---|
| Bass | 2 | Notes 12–96 |
| Rhythm | 10 | BD 36, SD 38, Tom 47, Clap 50, CH 42, OH 46 |
| Program change | 16 | 0–63 selects a pattern |

Also recognized: Clock, Start, Continue, Stop, All Notes Off, Active Sensing.

All channels and drum notes can be changed in the editor's **MIDI settings…** window (or with `--bass-ch`,
`--rhythm-ch`, `--pc-ch` on the command line) to match the T-8's `bACh`, `rhCh` and `Pc.Ch` menu settings.

## Connection test

**Test connection** (editor) or `t8 test` (command line) plays kick, snare, closed hat, open hat and a bass note
C2 on the configured channels, and listens on the T-8's MIDI input. The T-8 sends Active Sensing all the time,
so receiving anything proves the link works both ways. If no matching input exists, only sending is checked.

## Detecting the T-8's channels

**Detect from T-8** (MIDI settings window) or `t8 detect` (command line) listens on the T-8's MIDI input for up to
20 seconds. Press Start on the T-8 so its sequencer sends bass and drum notes (it stops 3 seconds after both are
heard). Drum notes are recognized by the drum note numbers, so set those first if you changed them. The T-8 doesn't
send a program change when you change pattern on it, so set the pattern select channel to its `Pc.Ch` by hand.
Channels that weren't seen keep their current value.

## T-8 settings needed

- **rXPc** (RX Program Change) **On**: for `select`.
- **SYnC** (MIDI Clock Sync) **AUTO** or **USB**: for `clock`.
- For `preview`, stop the T-8's own sequencer so it doesn't play on top.

## Platform support

DryWetMIDI's device API runs on Windows and macOS. On Linux, `T8.Midi` reads and writes ALSA raw MIDI devices (`/dev/snd/midiC*D*`) itself; your user needs access to them (usually the `audio` group).

## Approximations and open questions

- Preview timing of shuffle and flam, and the velocity curve, are approximations of the hardware.
- Rhythm file tracks `LT` and `HT` are sent as Tom and Clap (inferred). `CY` has no T-8 sound and is not sent.
- The program change order (0 = bank 1 pattern 1 … 63 = bank 4 pattern 16) is inferred.
- Bass slide is sent as overlapping (legato) notes, as on a TB-303. Whether the T-8 glides on received legato notes needs checking.
