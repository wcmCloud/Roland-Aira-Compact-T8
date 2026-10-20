# Roland Aira Compact T-8 Editor

A cross-platform (.NET 10) editor for the Roland AIRA Compact T-8: load the patterns from a T-8 backup, edit them, and write them back.

## Projects

| Project | What it does |
|---|---|
| `src/T8.Core` | Reads and writes the T-8's `.PRM` pattern files ([format](docs/PRM-FORMAT.md)) |
| `src/T8.Midi` | USB MIDI with [DryWetMIDI](https://github.com/melanchall/drywetmidi): pattern select, preview through the T-8's sounds, MIDI clock, `.mid` export ([notes](docs/MIDI.md)) |
| `src/T8.Editor` | The desktop editor (Avalonia): rhythm grid with a step inspector, bass lane, bank/pattern picker, copy/paste, preview and audition on the T-8 |
| `src/T8.Cli` | Command-line tool (`t8`) for the MIDI side |
| `tests/*.Tests` | One test project per project above, including headless UI tests of the editor |

## Run the editor

```sh
dotnet run --project src/T8.Editor
```

Open the `BACKUP` folder (or the T-8 drive itself), edit, and save. Pick the T-8 under **MIDI out** to preview patterns and hear steps as you click them.

### Connecting the T-8

1. Connect the T-8 by USB. It appears as a MIDI port named T-8; press **Refresh** if it isn't listed.
2. Open **MIDI settings…** and make the channels match the T-8's menu (hold SHIFT, press OCT+ (MENU)):
   bass `bACh` (default 2), rhythm `rhCh` (default 10), pattern select `Pc.Ch` (default 16).
   Or press **Detect from T-8**, then press Start on the T-8: the editor reads the bass and rhythm channels
   from the notes the T-8 sends (set pattern select by hand). Drum note numbers can be changed there too.
3. Press **Test connection**. You should hear kick, snare, closed and open hat and a bass note, and the editor
   checks that the T-8 answers on its MIDI input.
4. Choose what **Play** does: play the edited pattern through the T-8's sounds, or run the T-8's own
   sequencer with the editor as clock master (set the T-8's `SYnC` to AUTO or USB).
   Turn the T-8's `rXPc` on so **Select on T-8** and "switch the T-8 to the pattern I pick" work.

Settings are saved in your user profile (`T8Editor/settings.json` in the app data folder).

### Editing bass lines

- Click a step's note to select it, then click a key on the keyboard below: the step gets that note and the
  selection moves on, so a line can be played in key by key. Right-click a step for every note, Off/Note/Tie,
  Accent and Slide.
- **Generate a bass line** writes a new line into the pattern (up to its last step) from a key, a scale
  (major, minor, harmonic and melodic minor, pentatonics, blues, the modes, Phrygian dominant), an optional
  chord progression, the octave range and the chances of notes, chord tones, ties, slides, accents and
  octave jumps. **Generate** keeps the seed, so changing a setting and generating again keeps the feel;
  **Randomize** picks a new seed; **Undo** puts back the previous line.
- **Generate a rhythm** (Rhythm tab) writes a drum pattern in a style: techno, house, electro, breakbeat,
  hip-hop, trap, drum & bass, minimal/acid or random. Each style has hits that always play (like a
  four-on-the-floor kick) and hits that play by chance; **Density** thins or fills those, **Variation** adds
  and drops hits off the style. Pick which instruments to use, and the chances of accents, ghost notes and
  rolls. **Live variation** gives some hits a T-8 step probability below 100%, so the T-8 itself varies the
  pattern on every loop. **Build-up at the end** adds snare/tom hits in the last four steps.
- **Fills.** On the T-8 a fill is another pattern: SHIFT + STEP 8 (FILL PTN) picks it, SHIFT + STEP 7 (FILL TRG)
  plays it once. **Make fill** writes a fill for the current pattern into the **Fill pattern** of the same bank
  (16 by default): the groove for the first half, then snare and tom hits that get denser, louder and rolled,
  landing on kick, open hat and accent. The **Fill** button next to Stop plays it once after the current loop
  and returns to the pattern, so you can hear the transition before writing to the T-8. The T-8 has no MIDI
  messages for its fill, so pick the fill pattern on the T-8 itself.
- With **Show playing step** on, the step being played and the bass note sounding light up in yellow.

### Getting patterns on and off the T-8

Switch the T-8 on while holding **PLAY**; it mounts as a drive named `T-8`.

- **Load from T-8** opens the patterns in the drive's `BACKUP` folder.
- **Write to T-8** writes all 64 patterns to the drive's `RESTORE` folder. Then eject the drive and press
  **ACCENT** on the T-8; wait for `dOnE` before switching it off.

## Build packages

```sh
scripts/publish.sh               # osx-arm64, osx-x64, win-x64, linux-x64 into artifacts/
scripts/publish.sh osx-arm64     # just one
```

Packages are self-contained, so no .NET install is needed to run them. macOS gets a `Roland Aira Compact T-8 Editor.app`; it isn't
signed, so the first time right-click it and choose **Open**. On Windows you can also run `scripts/publish.ps1`.
GitHub Actions (`.github/workflows/ci.yml`) runs the tests on all three OSes and builds the packages; pushing a
`v*` tag attaches them to a release.

## Command line

```sh
dotnet test
dotnet run --project src/T8.Cli -- ports
dotnet run --project src/T8.Cli -- detect
dotnet run --project src/T8.Cli -- test --rhythm-ch 10 --bass-ch 2
dotnet run --project src/T8.Cli -- select 2 5
dotnet run --project src/T8.Cli -- preview BACKUP 1 1 --bpm 125
dotnet run --project src/T8.Cli -- clock --bpm 125
dotnet run --project src/T8.Cli -- export BACKUP 1 1 pattern.mid
```
