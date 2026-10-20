# T-8 pattern file format (.PRM)

Decoded from a full T-8 backup (64 bass + 64 rhythm files). Every file round-trips byte for byte through `T8.Core` (see `tests/`).

## Transport

Patterns are not sent over MIDI. Hold **PLAY** while powering on the T-8 and it mounts as a USB drive named `T-8`:

- `BACKUP/` – read the current patterns from here.
- `RESTORE/` – copy `BASS` and `RHYTHM` folders here, eject, press **ACCENT** on the T-8 to load them.

Layout (bank `bb` = 01–04, pattern `nn` = 01–16):

```
BASS/PTN_BANKbb/T8_BASS_PTNbb_nn.PRM
RHYTHM/PTN_BANKbb/T8_RHYTHM_PTNbb_nn.PRM
```

## Common syntax

Plain ASCII, LF line endings, one `KEY<TAB>= VALUE` per line, trailing newline. All 32 steps are always written, even past `LENGTH`.

## Bass file

```
LENGTH	= 12
TRIPLET	= 0
STEP 1	= STATE=1 NOTE=46 ACCENT=1 SLIDE=0
...
STEP 32	= STATE=0 NOTE=36 ACCENT=0 SLIDE=0
```

| Field | Values | Meaning |
|---|---|---|
| LENGTH | 1–32 | Last step |
| TRIPLET | 0 in every factory file | Probably the triplet scale flag (unconfirmed) |
| STATE | 0 / 1 / 2 | Off / note / tie |
| NOTE | 12–72 seen | MIDI note number (C0–C7 on the T-8) |
| ACCENT, SLIDE | 0 / 1 | Flags |

## Rhythm file

```
LENGTH	= 32
SCALE	= 1
SHUFFLE	= 0
FLAM	= 36
STEP 1	= AC=170AA BD=170AA SD=000AA LT=000AA HT=070AA CY=000AA CH=000AA OH=000AA
```

Tracks in order: `AC` (accent, shared by all instruments), `BD`, `SD`, `LT`, `HT`, `CY`, `CH`, `OH`.

Each cell is five characters, each a value 0–10 written `0`–`9` then `A`:

| Pos | Meaning | Values |
|---|---|---|
| 1 | On | 0 / 1 |
| 2 | Velocity | v.0–v.10 (7 is the default) |
| 3 | Sub step | 0 off, 1 = 1_2, 2 = 1_3, 3 = 1_4, 4 = flam |
| 4 | Probability | ×10 % (A = P.100) |
| 5 | Sub step probability | ×10 % (A = P.100) |

Header values seen: `SCALE` always 1, `SHUFFLE` 0–200, `FLAM` 20 or 36.

## Still to confirm on the device

The field meanings for positions 2–5, `TRIPLET`, `SCALE` and the units of `SHUFFLE`/`FLAM` are inferred from the manual (pp. 13–15) and the spread of values in the factory files. They need a check: change one setting on the T-8, back up, and diff.
