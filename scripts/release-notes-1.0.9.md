## What's new in 1.0.9

**Variations that keep what you lock**
- **Vary** rewrites the movement around the parts you lock. On rhythms, hats, ghost notes and step probability change (the kick starts locked). On bass, the rhythm stays and other notes can jump an octave; roots on the beat stay put while **Lock roots** is on. Press it again for another take. The seed stays put, so **Generate** can still bring the original pattern back.
- **Fill bank** writes eight related patterns into the next empty slots, close to this one first and further away last. A rhythm bank copies the bass line into each slot, and a bass bank copies the drums. Slots that already have notes are skipped.
- **Generate** still replaces the whole pattern. Locks are only for Vary.

**Bass key from the line**
- Opening a pattern sets the **Key** and **Scale** from the notes already in the line.
- **Colours** starts off, so the keyboard is plain. Turn it on and bright keys are in the scale, pink keys are the selected step's chord, and grey keys are outside it. Generate and Vary use this same key.

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
