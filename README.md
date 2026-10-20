# T-8 Editor

A cross-platform (.NET 10) editor for the Roland AIRA Compact T-8: load the patterns from a T-8 backup, edit them, and write them back.

## Projects

| Project | What it does |
|---|---|
| `src/T8.Core` | Reads and writes the T-8's `.PRM` pattern files ([format](docs/PRM-FORMAT.md)) |
| `src/T8.Midi` | USB MIDI with [DryWetMIDI](https://github.com/melanchall/drywetmidi): pattern select, preview through the T-8's sounds, MIDI clock, `.mid` export ([notes](docs/MIDI.md)) |
| `src/T8.Cli` | Command-line tool for trying the MIDI side before the editor UI exists |
| `tests/T8.Core.Tests` | Round-trip tests on the factory patterns, plus MIDI conversion tests |

## Try it

```sh
dotnet test
dotnet run --project src/T8.Cli -- ports
dotnet run --project src/T8.Cli -- select 2 5
dotnet run --project src/T8.Cli -- preview BACKUP 1 1 --bpm 125
dotnet run --project src/T8.Cli -- clock --bpm 125
dotnet run --project src/T8.Cli -- export BACKUP 1 1 pattern.mid
```

## Getting patterns on and off the T-8

Hold **PLAY** while switching the T-8 on. It mounts as a USB drive named `T-8`.

- Copy `BACKUP/BASS` and `BACKUP/RHYTHM` to read the current patterns.
- To load patterns, copy `BASS` and `RHYTHM` into `RESTORE/`, eject, then press **ACCENT** on the T-8.
