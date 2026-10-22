# T-8 MIDI

From the T-8's MIDI Implementation Chart (Owner's Manual p.31, version 1.02, Feb. 2022) and its MIDI menu
settings (p.25–26). The T-8 has **no Control Change (apart from the channel mode messages below) and no SysEx**,
so knobs can't be set over MIDI and patterns can only be transferred through the USB drive (see `PRM-FORMAT.md`).

| | Default channel | Notes |
|---|---|---|
| Bass | 2 (`bACh`) | Notes 12–96 |
| Rhythm | 10 (`rhCh`) | See the drum notes below |
| Program change | 16 (`Pc.Ch`) | 0–63 selects bank 1 pattern 1 … bank 4 pattern 16 |

Each channel can be 1–16 or OFF on the T-8. The editor's bass and rhythm channels can be **OFF** as well (and
`--bass-ch off` / `--rhythm-ch off` on the command line): nothing is sent for that part, not in the preview,
audition, connection test or all-notes-off. A `.mid` export leaves the part out too. Pattern select is always 1–16.

## Implementation chart and what the editor does with it

| Message | T-8 sends | T-8 recognizes | Editor |
|---|:-:|:-:|---|
| Note on/off, bass 12–96 | ✓ | ✓ | Sends: preview, audition, test. Receives: **Detect** finds the bass channel |
| Note on/off, rhythm (drum notes) | ✓ | ✓ | Sends: preview, audition, test. Receives: **Detect** finds the rhythm channel |
| Velocity 0–127 | ✓ | ✓ | Sends step velocity (v.0–v.10) and accents as note velocity |
| Program change 0–63 | ✓ (`tXPc` on) | ✓ (`rXPc` on) | Sends: **Select on T-8**, switching with the editor, sync mode. Receives: **Follow the T-8** switches the editor to the pattern picked on the T-8; **Detect** finds `Pc.Ch` |
| Clock | ✓ | ✓ | Sends in sync mode (24 per quarter note). Receives: shows the T-8's playing step and tempo |
| Start | ✓ | ✓ | Sends on Play in sync mode. Receives: the T-8's sequencer started |
| Continue | — | ✓ | Sends on Play after **Pause** in sync mode, so the T-8 carries on where it stopped |
| Stop | ✓ | ✓ | Sends on Stop/Pause in sync mode. Receives: the T-8's sequencer stopped |
| All Sound Off (CC 120) | ✓ (when MIDI goes offline) | ✓ | Sends on Stop and Pause |
| All Notes Off (CC 123) | — | ✓ | Sends on Stop and Pause |
| Active Sensing | ✓ | ✓ | Receives: **Test connection** checks the T-8 answers |
| Control change, pitch bend, aftertouch, SysEx, song position/select, tune request, system reset | — | — | Not supported by the T-8 |

### Drum notes

| Instrument | T-8 sends | T-8 recognizes | Editor sends (default) |
|---|---|---|---|
| Bass drum | 36 | 35, 36 | 36 |
| Snare drum | 38 | 38, 40 | 38 |
| Hand clap | 50 | 48, 50 | 50 |
| Tom | 47 | 45, 47 | 47 |
| Closed hi-hat | 42 | 42, 44 | 42 |
| Open hi-hat | 46 | 46 | 46 |

The pattern files' `LT` and `HT` tracks are the tom and the hand clap; `CY` has no T-8 sound (the editor hides it).
The drum notes can be changed in **MIDI settings…** if needed.

### T-8 menu settings (SHIFT + OCT+)

| Setting | Values | Used for |
|---|---|---|
| `rhCh`, `bACh` | 1–16, OFF | Rhythm and bass channels (must match the editor; **Detect from T-8** finds them) |
| `Pc.Ch` | 1–16 | Program change channel |
| `rXPc` | OFF, On | On: the T-8 switches pattern when the editor sends a program change |
| `tXPc` | OFF, On | On: the T-8 sends a program change when you change pattern on it, so the editor can follow |
| `SYnC` | AUTO, Int, MIDI, USB | AUTO or USB: the T-8 follows the editor's clock in sync mode |
| `thrv` | OFF, On | MIDI IN → OUT thru on the T-8's jacks (not used over USB) |

## Connection test

**Test connection** (editor) or `t8 test` (command line) plays kick, snare, closed hat, open hat and a bass note
C2 on the configured channels, and listens on the T-8's MIDI input. The T-8 sends Active Sensing all the time,
so receiving anything proves the link works both ways. If no matching input exists, only sending is checked.

## Detecting the T-8's channels

**Detect from T-8** (MIDI settings window) or `t8 detect` (command line) listens on the T-8's MIDI input for up to
20 seconds. Press Start on the T-8 so its sequencer sends bass and drum notes (it stops 3 seconds after both are
heard). Drum notes are recognized by the drum note numbers, so set those first if you changed them. The T-8 only
sends a program change when its `tXPc` setting is on: turn it on and change pattern while detecting, or set the
pattern select channel to its `Pc.Ch` by hand.
Channels that weren't seen keep their current value.

## T-8 settings needed

- **rXPc** (RX Program Change) **On**: for `select` and switching the T-8 from the editor.
- **tXPc** (TX Program Change) **On**: for the editor to follow pattern changes made on the T-8.
- **SYnC** (MIDI Clock Sync) **AUTO** or **USB**: for `clock` and the editor's sync mode.
- For `preview`, stop the T-8's own sequencer so it doesn't play on top.

## Following the T-8

With **Follow the T-8** on (MIDI settings, on by default), the editor listens to the T-8's MIDI output:

- When you change pattern on the T-8 (with `tXPc` on), the editor switches to the same bank and pattern.
- When you press Play on the T-8, its Start and Clock messages light up the playing step in the editor and the
  Playback card shows the T-8's tempo; Stop clears it.

Windows only lets one program open a MIDI input at a time, so the editor lets go of the input while the
connection test or the MIDI settings window uses it.

## Platform support

DryWetMIDI's device API runs on Windows and macOS. Its playback timer is too irregular on Linux, so there the
preview uses the editor's own thread-based timer. On Linux, `T8.Midi` reads and writes ALSA raw MIDI devices (`/dev/snd/midiC*D*`) itself; your user needs access to them (usually the `audio` group).

## Approximations and open questions

- Preview timing of shuffle and flam, and the velocity curve, are approximations of the hardware.
- Rhythm file tracks `LT` and `HT` are sent as Tom and Clap (inferred). `CY` has no T-8 sound and is not sent.
- The program change order (0 = bank 1 pattern 1 … 63 = bank 4 pattern 16) is inferred; the chart only gives the range 0–63.
- Bass slide is sent as overlapping (legato) notes, as on a TB-303. Whether the T-8 glides on received legato notes needs checking.
