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

IOptionsProcessor processor = OptionsProcessor.FromText(commandText, "FileUtil", args);

if (!processor.IsValid)
    return 2;

if (processor.HelpRequested)
    return 0;

var router = CommandRouter.FromAssembly(typeof(Program).Assembly);
return router.Execute(args[0], processor);

[Cumber.Command("copy")]
class Copy : ICumberCommand
{
    private readonly IOptionsProcessor _optionsProcessor;

    public Copy(IOptionsProcessor optionsProcessor)
    {
        Console.WriteLine("copy constructor called");
        _optionsProcessor = optionsProcessor;
    }
    public int Execute()
    {
        Console.WriteLine($"Command: {string.Join(" ", _optionsProcessor.CommandPath)}");
        Console.WriteLine($"Dest: {_optionsProcessor.GetValueOrDefault('d', "c:\\temp")}");
        return 0;
    }
}


[Cumber.Command("print")]
class Print : ICumberCommand
{
    private readonly IOptionsProcessor _optionsProcessor;

    public Print(IOptionsProcessor optionsProcessor)
    {
        Console.WriteLine("print constructor called");
        _optionsProcessor = optionsProcessor;
    }
    public int Execute()
    {
        Console.WriteLine($"Command: {string.Join(" ", _optionsProcessor.CommandPath)}");
        Console.WriteLine($"Lines: {_optionsProcessor.GetValueOrDefault("lines", 10u)}");
        Console.WriteLine($"Colour: {_optionsProcessor.GetValueOrDefault("colour", "white")}");
        return 0;
    }
}

