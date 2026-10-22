# MIDI settings

Open with the gear button in the top bar. Press **Save** to keep the settings; they are remembered between runs.

## Connection

- **MIDI OUT:** the T-8 shows up as "T-8" ("T-8 MIDI IN" on macOS).
- **Test connection** plays kick, snare, hats and a bass note on the set channels and checks that the T-8 answers.

## MIDI channels

- **BASS** = `bACh` (T-8 default 2), **RHYTHM** = `rhCh` (default 10), **PATTERN** = `Pc.Ch`, pattern select (default 16).
- **Detect from T-8:** press it, then press Start on the T-8 and let a pattern with drums and bass play. Pattern select is found too if `tXPc` is on and you change pattern while it listens.
- **OFF** (Bass and Rhythm) sends nothing for that part, like OFF on the T-8.

## Drum notes

The MIDI note for each drum (0–127). The T-8's defaults: bass drum 36, snare 38, tom 47, hand clap 50, closed hat 42, open hat 46. **Reset to T-8 defaults** puts back the channels and notes.

## Play button

- **Play through the T-8's sounds:** the editor sends the pattern as notes; you hear edits straight away and Fill works. Stop the T-8's own sequencer first.
- **Run the T-8's own sequencer:** the editor selects the pattern and sends MIDI clock; the T-8 plays its stored pattern at the editor's tempo. Set `SYnC` to AUTO or USB and turn `rXPc` on.
- **Switch the T-8 to the pattern I pick in the editor** (on by default) needs `rXPc` on.
- **Follow the T-8:** the editor follows pattern changes made on the T-8 (needs `tXPc` on) and shows its playing step and tempo.

## Updates

**Check for updates on startup** (at the bottom of the window, on by default) lets the editor ask GitHub once a day whether there is a newer version; see *About and licences*.

## On the T-8

Hold **SHIFT** and press **OCT+** (MENU) on the T-8, then set what the card lists: `bACh`, `rhCh`, `Pc.Ch`, `rXPc` (receive pattern changes), `tXPc` (send them) and `SYnC`. Hover over a setting to see what it's for.
