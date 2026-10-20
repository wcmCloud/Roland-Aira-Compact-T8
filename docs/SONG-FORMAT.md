# Song file format (`.t8song`)

A song is a UTF-8 JSON file with the extension `.t8song`. It names patterns by bank and pattern number, so it
plays whatever is in those slots of the pattern set that is open (a song file doesn't contain the patterns).

```json
{
  "format": "t8song",
  "version": 1,
  "title": "Hardcore",
  "bpm": 160,
  "loop": false,
  "entries": [
    { "bank": 1, "pattern": 1, "repeats": 1, "name": "Intro", "color": "#5B8DEF", "fill": { "bank": 1, "pattern": 16 } },
    { "bank": 1, "pattern": 3, "repeats": 4, "name": "Main",  "color": "#FF7A1A" },
    { "bank": 1, "pattern": 7, "repeats": 1, "name": "Outro", "color": "#4CD38A" }
  ]
}
```

| Field | Type | Meaning |
|---|---|---|
| `format` | string | Always `"t8song"`; files without it are refused |
| `version` | number | Format version, currently `1`. The editor refuses newer versions it doesn't know |
| `title` | string | Song title |
| `bpm` | number, optional | Tempo saved with the song (20–300); opening the song sets the editor's tempo. Left out = keep the current tempo |
| `loop` | boolean | Start again from the first entry after the last |
| `entries` | array | The song, in playing order |

Each entry:

| Field | Type | Meaning |
|---|---|---|
| `bank` | number | Bank 1–4 |
| `pattern` | number | Pattern 1–16 |
| `repeats` | number | How many times the pattern plays in a row (1–99) |
| `name` | string | Name shown in the song |
| `color` | string | Colour in the song and its timeline, `"#RRGGBB"` |
| `fill` | object, optional | `{ "bank": 1–4, "pattern": 1–16 }`: a pattern played once after the repeats, as a bridge into the next entry. Left out = no fill |

## How a song plays

Each repeat is one pass of the pattern, as long as its rhythm part (its last step); the bass loops on its own
length within the pass. A fill is one pass of the fill pattern's rhythm, with the entry's own bass under it in
the preview. So an entry plays `repeats` passes, then its fill if it has one, then the next entry starts.

- **Through the T-8's sounds** (Play mode "play the edited pattern"): the editor plays the whole song as notes.
- **On the T-8's own sequencer** (Play mode "run the T-8's sequencer"): the editor selects the first pattern,
  sends Start and MIDI clock, and as each pass begins sends the program change for the next pattern. The T-8
  queues it and switches when the current pattern ends, like choosing the next pattern by hand. The patterns on
  the T-8 should be the ones in the editor (write them with **Write to T-8**), `rXPc` must be on, and `SYnC` set
  to AUTO or USB.
