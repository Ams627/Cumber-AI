using Cumber;

const string commandText = """
    = FileUtil - a file utility
    Please specify one of the following commands:

      $(subcommands)

    == print - print a file
    This command prints a file on the console.

    Options:
        -n, --lines  Number of lines | type=uint
        -c, --colour  Output colour | type=string

    == copy - copy a file
    Copy a file to a given destination:

        -d destination | type=string

    """;

var processor = OptionsProcessor.FromText(commandText, "FileUtil", args);

if (!processor.IsValid)
    return 2;

if (processor.HelpRequested)
    return 0;

Console.WriteLine($"Command: {string.Join(" ", processor.CommandPath)}");
Console.WriteLine($"Lines: {processor.GetValueOrDefault("lines", 10)}");
Console.WriteLine($"Colour: {processor.GetValueOrDefault("colour", "white")}");

foreach (var argument in processor.PositionalArguments)
    Console.WriteLine($"Positional: {argument}");

return 0;

