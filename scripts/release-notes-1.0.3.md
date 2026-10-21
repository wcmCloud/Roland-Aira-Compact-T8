## What's new in 1.0.3

**T-8 drive**
- **Load from T-8** and **Write to T-8** are only enabled while the T-8 is connected in drive mode (hold PLAY
  while switching it on); a dot next to them turns green when the T-8 drive is found.
- The editor looks for the drive when it starts and when you come back to its window; **Check** looks again.
- A **?** button explains how to put the T-8 in drive mode and how to load written patterns (eject, then ACCENT).

**Look**
- A new top bar: buttons grouped in rounded boxes, **icons on every button**, an orange accent line and badge.
- A shorter MIDI Out box with a smaller font.

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
