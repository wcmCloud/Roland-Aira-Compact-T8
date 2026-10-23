# Getting started

Connect the T-8 with a USB cable and pick it under **OUT** in the top bar ("T-8 MIDI IN" on macOS). Press the refresh button if it isn't listed.

## First steps

- Open the MIDI settings (gear button) and press **Detect from T-8**, or set the channels by hand to match the T-8's menu.
- Press **Test** in the top bar: you should hear kick, snare, hats and a bass note.
- The editor opens with the default presets already loaded. The orange status line says you can find those files in the `original presets` folder, and **Open…** starts there. Load the T-8's own patterns from its drive (see *Loading and sending patterns*), or open a saved folder.
- Edit on the **Rhythm**, **Bass** and **Song** tabs, **Save** on the computer, then **Write to T-8**. The generators can replace a pattern (**Generate**), keep what you lock and rewrite the rest (**Vary**), or write eight related patterns into the next empty slots (**Fill bank**).

## The window

- **Top bar:** New, Open…, Save, Save as…; Load from T-8, Write to T-8, Check; Copy, Paste and Clear for the current pattern (bass and rhythm together); the MIDI output, Test, settings, Help and About.
- **Patterns:** 4 banks of 16 patterns, as on the T-8. Click one to edit it. The current pattern is outlined in orange, empty ones are dimmed, the fill pattern is outlined in blue. The folder name and an orange dot for unsaved changes show next to the title.
- **Playback:** the BPM knob, Play, Stop, Pause, Fill, Select on T-8 (switch the T-8 to this pattern), Audition (hear steps as you edit) and Show playing step. **Output** is Auto, T-8, or Offline. See *On the computer* below. **Follow clock** waits for an external MIDI clock.
- **Status bar:** what just happened, the MIDI channels in use, and an orange chip when a newer version is out (click it for the release page).

## Knobs

Drag a knob up or down (hold Shift for fine steps), use the scroll wheel or the arrow keys. Double-click puts it back to its default.

## On the computer

**Output** on the Playback card is **Auto**, **T-8**, or **Offline**.

- **Auto** plays on the T-8 when one is connected, and on this computer when there is no MIDI output or the T-8 is in drive mode.
- **Offline** always plays on this computer, including while the T-8 is plugged in.
- **T-8** plays only on the instrument. If it isn't available, Play says so.

Pick a drum kit and a volume. **Sound settings** chooses the audio output, the bass voice (saw or square, cutoff, resonance, decay and slide) and a mix for kick, snare, tom, clap, closed hat, open hat and bass. 0% mutes that part. The kits are Classic Analog, Deep Electro, Tight Machine, Bright Acid, Warm Tape and Gritty Club. They are a writing preview: a drum keeps ringing past the bar, and the next hit of that drum starts it again. **Save** and **Write to T-8** still send the pattern data, and the T-8 plays that pattern with its own sounds.

**Follow clock** waits for an external MIDI clock (IAC on Mac, loopMIDI on Windows), not the T-8's own output. Press Play to arm, then start that clock. Songs use the same output. **Follow DAW MIDI clock** on the Song tab runs a song from a DAW; **Export song as MIDI…** writes the arrangement. See *Song tab*.

## Undo

**Cmd/Ctrl+Z** takes back the last change to the open patterns: a cell, a paste, or a generator run. **Shift+Cmd/Ctrl+Z** puts it back. One history covers every pattern. Switching pattern does not clear it. Text fields keep their own undo.

## Factory patterns

The editor opens with the T-8's factory patterns already loaded, from the `original presets` folder shipped with the app. **Open…** starts in that folder. Or do a factory reset on the T-8 (it erases every pattern on it) and load them from its drive.
