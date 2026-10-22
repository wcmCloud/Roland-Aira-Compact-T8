# Rhythm generator

Writes a new drum pattern into the current pattern, up to its last step.

- **STYLE:** Techno, House, Electro, Breakbeat, Hip-hop / boom bap, Trap, Drum & bass, Minimal / acid or Random. Each style has hits that always play (like the four-on-the-floor kick in techno) and hits that only play some of the time.
- **Instrument chips** (BD, SD, TOM, CLAP, CH, OH): the lit ones are used.
- **Build-up at end:** snare and tom hits in the last four steps.

## Knobs

- **Density:** 50% follows the style; lower thins out the optional hits, higher adds more.
- **Variation:** hits the style wouldn't play, and optional ones dropped.
- **Accents:** on the accent track, mostly on the beat.
- **Ghost notes:** quiet extra snare and hat hits.
- **Rolls:** hat rolls (sub steps) and snare flams.
- **Live variation:** some optional hits get a step probability of 50–80%, so the T-8 plays the pattern a little differently every loop.

## Actions

- **Generate** uses the current seed: the same settings and seed always give the same pattern.
- **Randomize** picks a new seed: a different pattern in the same style.
- **Undo** takes back the last Generate, Randomize or Make fill (greyed out when there's nothing to undo).
- **Seed:** type a number to get a pattern back.

The generator never puts the closed and open hat on the same step: the T-8's open hat cuts off the closed one.
