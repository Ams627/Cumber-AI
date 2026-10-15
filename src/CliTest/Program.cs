using Cumber;
using Microsoft.Extensions.DependencyInjection;

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

    == copy2 - copy a file using type 2 copying
    Copy a file to a given destination:
    
        -d destination | type=string    
    """;

IOptionsProcessor optionsProcessor = OptionsProcessor.FromText(commandText, "FileUtil", args);

if (!optionsProcessor.IsValid)
    return 2;

if (optionsProcessor.HelpRequested)
    return 0;


var services = new ServiceCollection();
ISettings settings = new Settings();
services.AddSingleton<ISettings>(settings);
services.AddSingleton<IOptionsProcessor>(optionsProcessor);
using var serviceProvider = services.BuildServiceProvider();

var router = CommandRouter.FromAssembly(typeof(Program).Assembly, serviceProvider);
return router.Execute();

interface ISettings
{
    string Name { get; set; }
}
class Settings : ISettings
{
    public string Name { get; set; } = "Harry";
}


