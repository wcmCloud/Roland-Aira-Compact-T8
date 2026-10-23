## What's new in 2.0.0

**Play on the computer**
- **Output** on the Playback card is Auto, T-8, or Offline. Auto plays on the computer when no MIDI output is available or the T-8 is in drive mode. Offline always plays on the computer, including while the T-8 is plugged in. T-8 plays only on the instrument.
- Six drum kits (Classic Analog, Deep Electro, Tight Machine, Bright Acid, Warm Tape, Gritty Club), a volume, and **Sound settings**: audio output, a bass voice (wave, cutoff, resonance, decay, slide), and a mix for kick, snare, tom, clap, hats and bass.
- For writing and trying ideas. Save and Write to T-8 still send the pattern data. The T-8 plays that pattern with its own sounds.
- A drum keeps ringing past the end of the bar. The next hit of that drum starts it again, so long kicks don't pile up.

**Follow clock**
- Pattern Play can wait for an external MIDI clock instead of the BPM knob. Use a DAW clock on IAC or loopMIDI, not the T-8's own output. The same Output is used, on the computer or on the T-8.

**Undo**
- One undo history for the open patterns. Cmd/Ctrl+Z takes back a cell edit, a paste, or a generator run. Shift+Cmd/Ctrl+Z puts it back. Switching pattern does not clear it.

**Songs in a DAW**
- **Export song as MIDI…** writes the arrangement as notes, or as pattern changes the T-8 can follow.
- **Follow DAW MIDI clock** runs the song from Start, Stop, Continue and clock. On the computer it plays the notes here. In sequencer mode it forwards the clock to the T-8.
- On macOS, **Save song** can open Documents, Downloads and the sidebar.

**Detect from T-8**
- Detect listens to the T-8 even when MIDI out is another port. If MIDI out is still that other port, the result says so. Set MIDI out to the T-8 for Play and Test.

Self-contained builds: nothing else to install. Download the one for your computer:

| Computer | File |
|---|---|
| Mac with Apple Silicon (M1 and later) | `T8Editor-<version>-osx-arm64.zip` |
| Mac with Intel | `T8Editor-<version>-osx-x64.zip` |
| Windows PC (x64, most PCs) | `T8Editor-<version>-win-x64.zip` |
| Windows on ARM | `T8Editor-<version>-win-arm64.zip` |
| Linux x64 | `T8Editor-<version>-linux-x64.tar.gz` |
| Linux ARM64 (e.g. Raspberry Pi 4/5 with a 64-bit OS) | `T8Editor-<version>-linux-arm64.tar.gz` |

- **macOS:** unzip and move *Roland Aira Compact T-8 Editor* to Applications, then open it. The app is signed with
  a Developer ID and notarized by Apple, so it opens without warnings.
- **Windows:** unzip and run `T8Editor.exe`. If SmartScreen warns, choose **More info → Run anyway**.
- **Linux:** `tar -xzf T8Editor-<version>-linux-x64.tar.gz` and run `./T8Editor` in the folder. Your user needs access
  to the MIDI devices (usually the `audio` group).

See the [user guide](https://github.com/wcmCloud/Roland-Aira-Compact-T8#readme) for how to use the editor.
