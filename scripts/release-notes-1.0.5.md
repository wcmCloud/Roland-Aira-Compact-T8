## What's new in 1.0.5

**MIDI settings**
- Restyled to match the rest: MIDI OUT, BASS, RHYTHM and PATTERN in outlined boxes; drum notes as outlined
  number boxes with the drum icons; chips with icons for Test connection, Detect from T-8, Reset and Cancel;
  Save as the orange pill. The Play button options no longer touch the panel border.

**Rhythm**
- The single-step inspector is now the same compact panel as the multi-select one (orange for one step, blue for
  several), with an ON chip, and both sit in the same place so the layout doesn't jump.

**Bass**
- Click a step's **number** to select it, like clicking its note.

**Window**
- The title bar shows just the app name and version. The open folder and an orange "unsaved changes" dot now
  show on the Patterns card.

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
