using T8.Cli;
using T8.Midi.Ports;

return new CliApp(MidiPorts.System, Console.Out, Console.Error, Console.In).Run(args);
