using Cumber;
using Microsoft.Extensions.DependencyInjection;

const string commandText = """
    = FileUtil - a file utility
    Please specify one of the following commands:

      $(subcommands)

    == print - print a file
    This command prints a file on [cyan]#the console#.

    Options:
        -n, --lines  Number of lines | type=uint
        -c, --colour  Output colour | type=string

    == copy - copy a file
    _Copy_ a *file* to a given destination:

        -d destination | type=string minOccurs=0 maxOccurs=*
    """;

IOptionsProcessor optionsProcessor = OptionsProcessor.FromText(commandText, "FileUtil", args);

if (!optionsProcessor.IsValid)
    return -1;

if (optionsProcessor.HelpRequested)
    return 0;

var services = new ServiceCollection();
ISettings settings = new Settings();
services.AddSingleton(settings);
services.AddSingleton(optionsProcessor);
using var serviceProvider = services.BuildServiceProvider();

var router = CommandRouter.FromAssembly(typeof(Program).Assembly, serviceProvider);
router.Execute();
return 0;

internal interface ISettings
{
    string Name { get; set; }
}
internal class Settings : ISettings
{
    public string Name { get; set; } = "Harry";
}


