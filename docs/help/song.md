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
