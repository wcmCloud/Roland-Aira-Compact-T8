## What's new in 1.0.2

**Rhythm editing**
- Click a row's name (e.g. *Closed hat*) to **select** the row; it is outlined in orange.
- Each row ends with **Copy**, **Paste** and **Clear** icons. Copy takes all the row's steps (on/off, velocity,
  probability, sub step); Paste puts them on another row, also in **another pattern or bank**.
- With a row selected, **Cmd/Ctrl+C** and **Cmd/Ctrl+V** copy and paste it.
- The multi-select panel is more compact, and its sliders line up with the pattern knobs.

**Songs**
- The song rows and the timeline now use the same colour for each part.

**Licence**
- Dual-licensed: **MIT OR GPL-3.0**, at your choice. Both licence files come with the apps.

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
