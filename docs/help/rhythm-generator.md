# Rhythm generator

Open **Rhythm generator** at the bottom of the Rhythm tab. It writes a drum pattern into the current pattern, up to its last step.

- **STYLE:** Techno, House, Electro, Breakbeat, Hip-hop / boom bap, Trap, Drum & bass, Minimal / acid or Random. Each style has hits that always play (like the four-on-the-floor kick in techno) and hits that only play some of the time.
- **Instrument chips** (BD, SD, TOM, CLAP, CH, OH): the lit ones are used.
- **LOCK:** a lit lock is kept by Vary. The kick starts locked. Generate ignores the locks and replaces the whole pattern.
- **Build-up at end:** snare and tom hits in the last four steps.

## Knobs

- **Density:** 50% follows the style; lower thins out the optional hits, higher adds more.
- **Variation:** hits the style wouldn't play, and optional ones dropped. Vary uses it for how far the hats move.
- **Accents:** on the accent track, mostly on the beat.
- **Ghost notes:** quiet extra snare and hat hits.
- **Rolls:** hat rolls (sub steps) and snare flams.
- **Live variation:** some optional hits get a step probability of 50–80%, so the T-8 plays the pattern a little differently every loop. Vary rewrites that probability when this is above zero.

## Actions

- **Generate** replaces the whole pattern. The same settings and seed always give the same pattern.
- **Vary** keeps locked parts and rewrites hats, ghost notes and, when Live variation is up, step probability. Snare, tom and clap hits stay. Press it again for another take; the seed stays put, so Generate can still bring the pattern back.
- **Fill bank** writes eight related patterns into the next empty slots, close to this one first and further away last, and copies the bass line into each so a song has somewhere to go.
- **Randomize** picks a new seed and replaces the pattern: a different one in the same style.
- **Undo** (also Cmd/Ctrl+Z) takes back the last change to the open patterns, including Generate, Vary, Randomize, Fill bank and Make fill. It is greyed out when there's nothing to undo. Shift+Cmd/Ctrl+Z redoes. Switching pattern keeps the history.
- **Seed:** type a number to get a generated pattern back.

The generator never puts the closed and open hat on the same step: the T-8's open hat cuts off the closed one.
