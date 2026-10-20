# T-8 MIDI

From the MIDI Implementation Chart (manual p.31). The T-8 has **no Control Change and no SysEx**, so knobs can't be set over MIDI and patterns can only be transferred through the USB drive (see `PRM-FORMAT.md`).

| | Default channel | Notes |
|---|---|---|
| Bass | 2 | Notes 12–96 |
| Rhythm | 10 | BD 36, SD 38, Tom 47, Clap 50, CH 42, OH 46 |
| Program change | 16 | 0–63 selects a pattern |

Also recognized: Clock, Start, Continue, Stop, All Notes Off, Active Sensing.

## T-8 settings needed

- **rXPc** (RX Program Change) **On**: for `select`.
- **SYnC** (MIDI Clock Sync) **AUTO** or **USB**: for `clock`.
- For `preview`, stop the T-8's own sequencer so it doesn't play on top.

## Platform support

DryWetMIDI's device API runs on Windows and macOS. On Linux, `T8.Midi` writes to ALSA raw MIDI devices (`/dev/snd/midiC*D*`) itself; your user needs access to them (usually the `audio` group). Linux output only for now.

## Approximations and open questions

- Preview timing of shuffle and flam, and the velocity curve, are approximations of the hardware.
- Rhythm file tracks `LT` and `HT` are sent as Tom and Clap (inferred). `CY` has no T-8 sound and is not sent.
- The program change order (0 = bank 1 pattern 1 … 63 = bank 4 pattern 16) is inferred.
- Bass slide is sent as overlapping (legato) notes, as on a TB-303. Whether the T-8 glides on received legato notes needs checking.
