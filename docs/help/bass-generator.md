# Bass generator

Writes a bass line into the current pattern, up to its last step, following the key set on the step editor.

**KEY**, **SCALE** and **CHORDS** sit above the keyboard. Opening a pattern sets the key and scale from the notes in the line. Changing them changes what Generate and Vary write. **COLOURS** starts off; turn it on and the keyboard lights that scale. Grey keys are outside it.

- **KEY:** C to B.
- **SCALE:** major, the minors, the pentatonics, blues and the modes (Dorian, Phrygian, Lydian, Mixolydian, Locrian, Phrygian dominant).
- **CHORDS:** a progression built from the scale (for example i – VI – III – VII), and how many steps each chord lasts. **TONES** names the root, third and fifth at the selected step.
- **LOCK ROOTS:** on by default. Vary keeps the octave of a chord root that falls on the beat.

## Knobs

- **From octave** and **Octaves:** the range of the line (octave 1 is the classic bass range).
- **Notes:** how often a step plays a new note (beats get a bonus).
- **Chord tones:** how often a note on the beat is the chord's root, third or fifth.
- **Ties, Slides, Accents, Octave jumps:** how often each happens. Vary uses Octave jumps for how often an unlocked note moves up or down an octave.

The line starts on the root and each chord change starts on that chord's root.

## Actions

- **Generate** replaces the whole line. The same settings and seed always give the same line. **Randomize** picks a new seed and replaces it.
- **Vary** keeps the rhythm and every pitch. Other notes can jump an octave. Roots on the beat stay put while LOCK ROOTS is on. Press it again for another take; the seed stays put.
- **Fill bank** writes eight related lines into the next empty slots, close to this one first, and copies the drum pattern into each.
- **Undo** takes back the last Generate, Vary, Randomize or Fill bank.
- **Seed:** type a number to get a generated line back.
