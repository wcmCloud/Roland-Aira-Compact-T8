namespace T8.Editor.Help;

/// <summary>One page of the in-app help: a title and its text. Paragraphs are separated by blank lines and
/// lines starting with "• " are list items.</summary>
public sealed record HelpTopic(string Id, string Title, string Body)
{
    /// <summary>The body split into paragraphs, for the help views.</summary>
    public IReadOnlyList<string> Paragraphs => Body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// The in-app help, condensed from the README's user guide. The help window lists every topic; the small ?
/// buttons in the window show one topic each (see <see cref="Views.HelpTip"/>).
/// </summary>
public static class HelpTopics
{
    public const string GettingStarted = "getting-started";
    public const string Connection = "connection";
    public const string MidiChannels = "midi-channels";
    public const string DrumNotes = "drum-notes";
    public const string PlayModes = "play-modes";
    public const string OnTheT8 = "on-the-t8";
    public const string Files = "files";
    public const string T8Drive = "t8-drive";
    public const string WriteToT8 = "write-to-t8";
    public const string Patterns = "patterns";
    public const string Playback = "playback";
    public const string Rhythm = "rhythm";
    public const string MultiSelect = "multi-select";
    public const string RhythmGenerator = "rhythm-generator";
    public const string Fills = "fills";
    public const string Bass = "bass";
    public const string StepEditor = "step-editor";
    public const string BassGenerator = "bass-generator";
    public const string Song = "song";
    public const string SongPlayback = "song-playback";
    public const string Shortcuts = "shortcuts";

    public static IReadOnlyList<HelpTopic> All { get; } =
    [
        new(GettingStarted, "Getting started",
            """
            Connect the T-8 with a USB cable and pick it under OUT in the top bar ("T-8 MIDI IN" on macOS); press the refresh button if it isn't listed.

            Open the MIDI settings (gear button) and press Detect from T-8, or set the channels by hand to match the T-8's menu. Press Test in the top bar: you should hear kick, snare, hats and a bass note.

            To edit the T-8's own patterns, load them from the T-8 drive (see "Loading from the T-8"), edit them on the Rhythm and Bass tabs, Save them on the computer, and Write to T-8.

            Want the factory patterns? Use Open… and pick the BACKUP folder that comes with the editor's source, or do a factory reset on the T-8 and load them from its drive.
            """),
        new(Connection, "Connection and test",
            """
            The T-8 shows up as a MIDI port called T-8 ("T-8 MIDI IN" on macOS). Pick it under OUT in the top bar or in the MIDI settings.

            • Test (pulse icon) plays kick, snare, closed hat, open hat and a bass note on the set channels and checks that the T-8 answers. If you hear nothing, the channels don't match the T-8's menu.
            • The refresh button lists the MIDI ports again, e.g. after plugging the T-8 in.

            While the T-8 is in drive mode it isn't a MIDI device: switch it off and on normally to play it again.
            """),
        new(MidiChannels, "MIDI channels",
            """
            The editor and the T-8 must use the same channels. On the T-8: hold SHIFT and press OCT+ (MENU).

            • BASS = bACh (T-8 default 2), RHYTHM = rhCh (default 10), PATTERN = Pc.Ch, pattern select (default 16).
            • Detect from T-8: press it, then press Start on the T-8 and let a pattern with drums and bass play. Pattern select is found too if tXPc is on and you change pattern while it listens.
            • OFF (first choice for Bass and Rhythm) sends nothing for that part, like OFF on the T-8: handy to hear just the drums or just the bass.

            Press Save to keep the settings; they are remembered between runs. Reset to T-8 defaults puts back the T-8's channels and drum notes.
            """),
        new(DrumNotes, "Drum notes",
            """
            The MIDI note number (0–127) the editor sends for each drum. The T-8's defaults are filled in: bass drum 36, snare 38, tom 47, hand clap 50, closed hat 42, open hat 46.

            Only change them if your T-8 (or another device) uses different notes. Reset to T-8 defaults puts them back.
            """),
        new(PlayModes, "What Play does",
            """
            Set in the MIDI settings, under Play button:

            • Play through the T-8's sounds: the editor sends the pattern as notes and the T-8 plays them with its sounds. You hear your edits straight away, and Fill works. Stop the T-8's own sequencer first so it doesn't play on top.
            • Run the T-8's own sequencer: the editor switches the T-8 to the pattern and sends MIDI clock, so the T-8 plays its stored pattern at the editor's tempo. Set SYnC to AUTO or USB and turn rXPc on.

            • Switch the T-8 to the pattern I pick in the editor: sends a pattern change when you pick a pattern (needs rXPc on).
            • Follow the T-8: when you change pattern on the T-8 the editor follows (needs tXPc on), and when you press Play on the T-8 the editor shows its playing step and tempo.
            """),
        new(OnTheT8, "Settings on the T-8",
            """
            Hold SHIFT and press OCT+ (MENU) on the T-8, then set what the card lists: bACh (bass channel), rhCh (rhythm channel), Pc.Ch (pattern select channel), rXPc (receive pattern changes), tXPc (send pattern changes) and SYnC (AUTO or USB to follow the editor's clock).

            Hover over a setting in the card to see what it is for.
            """),
        new(Files, "Opening and saving",
            """
            The T-8 has 64 patterns (4 banks × 16), each with a rhythm and a bass part. The editor works on all 64 at once.

            • New: 64 blank patterns.
            • Open…: a BACKUP or RESTORE folder (with BASS and RHYTHM inside), or the T-8 drive itself.
            • Save / Save as…: write all 64 patterns to a folder on the computer. Keep a copy before you write to the T-8.
            • Copy, Paste and Clear in the top bar work on the current pattern, bass and rhythm together.

            The Patterns card shows the open folder; an orange dot and "unsaved changes" mean there are edits that aren't saved yet.
            """),
        new(T8Drive, "Loading from the T-8",
            """
            The T-8 doesn't send its patterns over MIDI. Instead it shows up on the computer as a USB drive named T-8 with a BACKUP folder.

            • Switch the T-8 off and connect it by USB.
            • Hold PLAY and switch it on; keep holding until the step buttons light up.
            • Wait about a minute, until the T-8 drive appears and the step buttons stop counting.
            • Press Load from T-8 (or Check first, if it's greyed out).

            Load from T-8 and Write to T-8 are only enabled while the drive is connected; the dot next to them turns green. Then Save as… to keep a copy, eject the drive and switch the T-8 off.

            Factory patterns: a factory reset (hold SLIDE (WRITE) while switching on, press ACCENT, wait for dOnE) erases every pattern on the T-8 and puts the factory ones back.
            """),
        new(WriteToT8, "Writing to the T-8",
            """
            Write to T-8 puts all 64 patterns into the T-8 drive's RESTORE folder; the T-8 loads them when you press ACCENT.

            • Save your patterns on the computer first.
            • Put the T-8 in drive mode (hold PLAY while switching on) and wait for the T-8 drive.
            • Press Write to T-8 and confirm.
            • Eject the T-8 drive, then press ACCENT on the T-8.
            • Wait until the display shows dOnE before switching it off.

            This replaces all 64 patterns on the T-8, so load and save its current patterns first if you want to keep them.
            """),
        new(Patterns, "Patterns",
            """
            4 banks of 16 patterns, as on the T-8. Click a cell to edit that pattern (on the Song tab, clicking a pattern adds it to the song instead).

            • The current pattern is outlined in orange; empty patterns are dimmed; the fill pattern is outlined in blue.
            • Select on T-8 (Playback card) switches the T-8 to the current pattern.
            """),
        new(Playback, "Playback",
            """
            • BPM: the tempo for Play. Drag the knob, use the wheel or arrow keys; double-click resets it.
            • Play, Pause, Stop: play the current pattern. Pause keeps the place; Stop goes back to the start.
            • Fill: play the fill pattern once after this loop (see Fills).
            • Select on T-8: switch the T-8 to the current pattern.
            • Audition: play each step on the T-8 as you edit it.
            • Show playing step: outline the playing step (and the bass note on the keyboard) in yellow.

            When you press Play on the T-8 itself, the card shows "T-8 playing" and its tempo. What Play does is set in the MIDI settings (see "What Play does").
            """),
        new(Rhythm, "Editing rhythms",
            """
            Each row is a T-8 track (Accent, Bass drum, Snare, Tom, Clap, Closed hat, Open hat), each column one of 32 steps; the brighter columns are the beats.

            • Click a cell to turn it on or off. The cell you clicked is outlined in white and shown in the orange panel on the right: ON, Velocity (v.0–v.10; the T-8 uses v.7), Probability, Sub step (Half, Third, Quarter: 2, 3 or 4 hits; Flam: a quiet hit just before) and the sub step's probability.
            • Pattern knobs: Last step (1–32; later steps are dimmed), Shuffle (0–200, delays every second 16th) and Flam (the spacing of a flam's two hits).
            • Rows: click a row's name to select it. The icons at the end of a row copy it, paste a copied row over it (also into another pattern) and clear it. With a row selected, Cmd/Ctrl+C and Cmd/Ctrl+V copy and paste.
            """),
        new(MultiSelect, "Editing several steps",
            """
            Ctrl-click (Cmd-click on macOS) or Shift-click active steps to pick them; they get a blue outline. Select all active steps picks every active step.

            The blue panel then sets Velocity, Probability, Sub step and Sub step probability for all of them: Apply writes the values to every picked step and clears the selection; Clear leaves them as they were.

            Inactive steps can't be picked, and switching a picked step off drops it from the selection.
            """),
        new(RhythmGenerator, "Rhythm generator",
            """
            Writes a new drum pattern into the current pattern, up to its last step.

            • STYLE: Techno, House, Electro, Breakbeat, Hip-hop, Trap, Drum & bass, Minimal / acid or Random.
            • Instrument chips (BD, SD, TOM, CLAP, CH, OH): the lit ones are used.
            • Build-up at end: snare and tom hits in the last four steps.
            • Knobs: Density (50% follows the style), Variation, Accents, Ghost notes, Rolls (hat rolls and flams) and Live variation (some hits get a 50–80% step probability, so the T-8 varies the pattern every loop).

            ACTIONS: Generate uses the current seed (same settings and seed, same pattern); Randomize picks a new seed; Undo takes back the last Generate, Randomize or Make fill; type a Seed to get a pattern back.
            """),
        new(Fills, "Fills",
            """
            On the T-8 a fill is just another pattern: SHIFT + STEP 8 (FILL PTN) picks it, SHIFT + STEP 7 (FILL TRG) plays it once.

            • Set FILL (the fill pattern, 16 by default) in the rhythm generator and press Make fill: it writes the current groove followed by a build-up into that pattern. Undo takes it back.
            • Press Fill in the Playback card while a pattern plays: the fill plays once at the end of the loop, then the pattern carries on.

            After Write to T-8, pick the fill on the T-8 itself with SHIFT + STEP 8 (there is no MIDI message for it).
            """),
        new(Bass, "Editing bass lines",
            """
            Each column is one step: its note, its state, semitone up/down, Accent and Slide.

            • Click a step's note or its number to select it; its note lights up orange on the keyboard.
            • The state button cycles Off → Note → Tie. A tie holds the previous note through the step.
            • PATTERN panel: Last step (1–32), Triplet and Transpose (−12, −1, +1, +12 semitones for the whole line).
            • Right-click a step for every note from C0 to C7 by octave, plus Off/Note/Tie, Accent and Slide.
            """),
        new(StepEditor, "Step editor and keyboard",
            """
            Click a key on the keyboard to give the selected step that note. The step is turned on and you hear the note on the T-8.

            • Next step after a note (orange when on): the selection then moves on, so you can play a line in key by key.
            • The chevrons move the selection to the previous or next step.
            """),
        new(BassGenerator, "Bass line generator",
            """
            Writes a new bass line into the current pattern, up to its last step, following a key and a scale.

            • KEY and SCALE: e.g. E minor pentatonic, or modes like Dorian and Phrygian.
            • CHORDS: a progression built from the scale (e.g. i – VI – III – VII) and how many steps each chord lasts.
            • Knobs: From octave and Octaves (the range), Notes, Chord tones, Ties, Slides, Accents and Octave jumps.

            The line starts on the root and each chord change starts on that chord's root. ACTIONS (Generate, Randomize, Undo, Seed) work as in the rhythm generator.
            """),
        new(Song, "Songs",
            """
            The Song tab chains patterns into a song, saved as a .t8song file.

            • With the Song tab open, click patterns in the pattern grid to add them in order (Add selected pattern adds the open one).
            • For each entry set the bank, pattern, repeats, name and colour (click the swatch).
            • FILL plays a fill pattern once after the repeats, as a bridge into the next entry.
            • Row buttons: play from here, move up or down, duplicate, remove.
            • COPY TO BANK copies the song's patterns into one bank in song order (at most 16 entries and fills), so you can chain them by hand on the T-8.
            """),
        new(SongPlayback, "Playing a song",
            """
            Press Play song. The playing entry is outlined in green, the timeline fills up, and the bottom row shows now, next, the step and the time.

            • Previous / Next jump between entries.
            • Click a block on the timeline to play that part next, as soon as the current pattern ends (outlined in white until then).
            • LOOP: Play once, Loop whole song, or keep repeating one entry.

            On the T-8's own sequencer (MIDI settings), the editor tells the T-8 the next pattern as each one starts: Write to T-8 first, and turn rXPc on with SYnC on AUTO or USB.
            """),
        new(Shortcuts, "Shortcuts and tips",
            """
            • F1: this help. The small ? buttons next to each panel show the help for that panel.
            • Cmd/Ctrl+S: save the patterns.
            • Cmd/Ctrl+C / Cmd/Ctrl+V with a rhythm row selected: copy and paste the row.
            • Knobs: drag up or down (Shift for fine steps), scroll, or use the arrow keys; double-click resets.
            • Right-click a bass step for every note.

            The full user guide, with screenshots, is on the project's GitHub page.
            """),
    ];

    public static HelpTopic Get(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}
