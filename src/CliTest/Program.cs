using HelpTextOptions;

const string commandText = """
    = FileUtil - a file utility
      $(subcommands)

    == Print - print a file
    -n, --lines  Number of lines | type=int
    -c, --colour  Output colour | type=string
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

