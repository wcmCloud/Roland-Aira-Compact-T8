## What's new in 1.0.1

**Songs**
- A new **Song** tab: chain patterns from the banks with repeats and **fill** bridges, see the song on a colour
  timeline, and play it through the T-8's sounds or on the T-8's own sequencer. Saved as `.t8song` files
  ([format](https://github.com/wcmCloud/Roland-Aira-Compact-T8/blob/master/docs/SONG-FORMAT.md)).
- On the Song tab, clicking a pattern in the grid adds it to the song ("Bank n - Pattern y").
- Click a part on the timeline while playing to play it next, when the current pattern ends.
- Loop menu: Play once, **Loop whole song**, or loop a single entry.
- **Copy to bank**: copy a song's patterns into a bank in song order (safe even when the song uses that bank).

**Editing**
- Rhythm grid **multi-select**: Ctrl/Cmd/Shift-click active steps and set velocity, probability and sub step
  for all of them with **Apply**.

**MIDI**
- **OFF** for the bass and rhythm channels, like bACh/rhCh OFF on the T-8.
- Everything in the T-8's MIDI implementation chart: **Pause** (MIDI Continue), All Sound Off, and
  **Follow the T-8** (switch to the pattern picked on the T-8, show its playing step and tempo).
- Linux: preview playback now keeps time (DryWetMIDI's Linux timer was far too irregular).

**Look**
- The Transport card is now **Playback**; a redesigned MIDI settings window; compact song rows.

Self-contained builds: nothing else to install. Download the one for your computer:

| Computer | File |
|---|---|
| Mac with Apple Silicon (M1 and later) | `T8Editor-<version>-osx-arm64.zip` |
| Mac with Intel | `T8Editor-<version>-osx-x64.zip` |
| Windows PC (x64, most PCs) | `T8Editor-<version>-win-x64.zip` |
| Windows on ARM | `T8Editor-<version>-win-arm64.zip` |
| Linux x64 | `T8Editor-<version>-linux-x64.tar.gz` |
| Linux ARM64 (e.g. Raspberry Pi 4/5 with a 64-bit OS) | `T8Editor-<version>-linux-arm64.tar.gz` |

- **macOS:** unzip and move *Roland Aira Compact T-8 Editor* to Applications. The app isn't notarized by Apple, so the
  first time right-click it and choose **Open**. If macOS says it is damaged, run
  `xattr -dr com.apple.quarantine "/Applications/Roland Aira Compact T-8 Editor.app"` once.
- **Windows:** unzip and run `T8Editor.exe`. If SmartScreen warns, choose **More info → Run anyway**.
- **Linux:** `tar -xzf T8Editor-<version>-linux-x64.tar.gz` and run `./T8Editor` in the folder. Your user needs access
  to the MIDI devices (usually the `audio` group).

See the [user guide](https://github.com/wcmCloud/Roland-Aira-Compact-T8#readme) for how to use the editor.
