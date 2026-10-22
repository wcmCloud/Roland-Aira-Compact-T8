## What's new in 1.0.6

**Notarized for macOS**
- The Mac apps are now signed with a Developer ID and **notarized by Apple**: unzip, move to Applications and open,
  with no Gatekeeper warnings and no `xattr` step.

**Help**
- A **Help window** (F1, the **?** button in the top bar, or Help in the macOS app menu) with a search box and
  topics for every part of the editor; F1 opens the topic for the tab you're on.
- Small **(?)** icons next to the panels show a short popup about that panel, with a link to the full topic.

**Updates**
- The editor tells you when a newer version is out: an orange **"v… available"** chip in the status bar opens the
  release page. It asks GitHub at most once a day, sends no personal data, and can be turned off in the settings;
  **Check for updates** in the About window asks right away.

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
