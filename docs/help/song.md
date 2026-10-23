# Song tab

The Song tab chains patterns from the banks into a song.

## Arrangement

- With the Song tab open, **click patterns in the pattern grid** to add them in order. **Add selected pattern** adds the open one.
- **Fill bank**, in the rhythm or bass generator, writes eight variations of the open pattern into the next empty slots. Add those from the grid when a song needs somewhere to go.
- For each entry set the **bank**, **pattern**, how many times it **repeats**, a **name** and a **colour** (click the swatch). **Steps** shows how long one pass is.
- **FILL** plays a fill pattern once after the repeats, as a bridge into the next entry (same bank; 16 by default).
- The row buttons play the song from that entry, move it up or down, duplicate it and remove it.

## Playing

- **Play song** plays it; the playing entry is outlined in green and the timeline fills up. The bottom row shows now, next, the step and the time.
- **Previous** and **Next** jump between entries. **Click a block on the timeline** to play that part next, as soon as the current pattern ends.
- **LOOP:** Play once, Loop whole song (saved with the song), or keep repeating one entry.
- On the T-8's own sequencer (see *MIDI settings*), **Write to T-8** first and turn `rXPc` on with `SYnC` on AUTO or USB.

## Copy to bank

**COPY TO BANK** copies the song's patterns into one bank in song order: entry 1 to pattern 1, and so on, then the fill patterns. At most 16 entries and fills fit; repeats don't use slots. The editor copies the patterns before writing, so a song made from that same bank still copies correctly. Then **Save** and **Write to T-8**.

## Files

**Save song** and **Open song…** use `.t8song` files: a small JSON file with the bank and pattern numbers, repeats, names, colours, fills, the loop and the tempo. A song plays whatever patterns are open.

## Use a song in a DAW

Only the Song tab offers **Export song as MIDI…**. Choose **Programs** for queued T-8 pattern changes or
**Notes** for the full drum and bass arrangement. Exports contain one complete pass through the song,
including repeats and fills, the current tempo, entry markers and the full duration. The Loop menu does
not repeat the exported file. Notes use the MIDI settings' channels and drum mapping; OFF parts are omitted.
Probability is rendered when exporting, and shuffle, flam and slide use the same approximations as preview.

For Programs, load the same patterns into the T-8, enable `rXPc`, and set `SYnC` to USB (or AUTO).
Route the imported MIDI track to the T-8 on its Pc.Ch channel and send MIDI clock from the DAW.
The first selection is at time zero; later selections are queued one pass ahead, one step after that
pass begins. Start from the beginning so the T-8 receives the first selection while stopped; if your
DAW starts the hardware before processing that event, preselect the first pattern on the T-8.
For Notes, route rhythm and bass to the desired instruments or T-8 channels and keep the T-8 sequencer
stopped to avoid doubled notes.

To run the arrangement live, enable **Follow DAW MIDI clock**, choose its input and press **Play Song**
to arm. The DAW's Start resets the arrangement, Stop silences it, and Continue resumes its position.
Tempo follows incoming clock directly. No DAW selector is needed. In sequencer mode the editor forwards
transport and clock to the T-8 and queues pattern changes; in notes mode it sends notes without starting
the T-8 sequencer. Use a dedicated input from the DAW, rather than the T-8's returning clock, and avoid
also routing the DAW clock directly to the T-8. Leaving the Song tab disarms external playback.
The DAW owns navigation; entry jumps and entry-only loops are unavailable while following it.
Live notes are scheduled on MIDI clock boundaries (four MIDI-file ticks at 96 PPQ).
For hardware sequencer playback, start at the beginning: the T-8 cannot locate inside a pattern from
a DAW song-position message. In notes mode Song Position Pointer locates in sixteenth-note steps.

On macOS use an enabled IAC Driver bus; on Windows use a virtual MIDI port such as loopMIDI.
In Ableton Live enable Sync on that MIDI output; in Logic enable MIDI clock output to it; in Reaper
configure the output to send clock and transport. Select that same port as the clock input here.
