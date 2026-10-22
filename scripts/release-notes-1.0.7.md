## What's new in 1.0.7

**Bass: play the notes on the T-8**
- With **T-8 KEYS** on (in the Step editor panel, on by default), a key you play on the T-8's own keyboard goes
  into the selected bass step, just like clicking the on-screen keyboard, including "next step after a note".
- The key you hold on the T-8 lights up **green** on the on-screen keyboard. Notes the T-8's own sequencer plays
  are shown but never recorded. The chip is greyed out when the T-8's MIDI input isn't open.

**Settings**
- **Switch the T-8 to the pattern I pick in the editor** is now on by default, like the T-8's rXPc (settings you
  already saved are kept).

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
