# Cumber - an unencumbered command-line options processor

A C# library that derives command-line options and help text from one readable definition.
Targets .NET 8 (and can be used by later .NET applications). The public entry point is a static factory.

The aim is to provide command-line parsing in the style of git where the first word
after the program name is a command (subsequent words may be sub-commands). This
library provides very easy mapping from command specification in help text to a c#
class which implements the command functionality. You only need to write the 
help text - complex specification of commands and options is not required.

Commands each have their own section within the help text: each section is written
with a number of equals signs starting at the first character on the line. Each
command "level" has one more equals sign. Level 1 is the name of the program itself:

Command line options start either with a dash (for single letter options)
or double dash for long-name options. a single letter option can be equivalent
to a long-name option - for example `-a, --all`. "Old style" Microsoft CMD.EXE options
starting with a forward-slash (`dir /w`) are not supported. This is due to the convergence
of path specification conventions where forward-slashes can be used to specify
paths in many tools. 

```
= git

== remote

== push
```

This means the help text can support more than one program - the idea behind this is
the old unix example of grep/egrep etc. where if the same executable has a different
name, it functions differently. 

lines starting with a dash or double-dash specify options which can be retrieved later. 
Cumber automatically checks for and reports invalid options. 

```
    -a,--all - load all files from the folder instead of just the first one
```

Options may take a parameter - to specify this, give the `type` directive
after the option with an intervening pipe character.

```
    -i - specify an input file | type=string
```

Options may also be given more than once on the command line

```
    -i - specify up to five input files | type=string minOccurs=0 maxOccurs=3
```

You can then type a command like `MyTool read -i file1.txt -i file2.txt -i file3.txt`


## Example

```csharp
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
```

Cumber uses `Microsoft.Extensions.DependencyInjection` for dependency injection 
and you should inject the `IOptionsProcessor` itself (as shown in the example above)
to be able to read options in the command implementation. 

The implementation of the print command can then be written as follows:

```csharp
using Cumber;

[Cumber.Command("print")]
internal class Print : ICumberCommand
{
    private readonly IOptionsProcessor _optionsProcessor;

    public Print(IOptionsProcessor optionsProcessor)
    {
        Console.WriteLine("print constructor called");
        _optionsProcessor = optionsProcessor;
    }
    public void Execute()
    {
        Console.WriteLine($"Command: {string.Join(" ", _optionsProcessor.CommandPath)}");
        Console.WriteLine($"Lines: {_optionsProcessor.GetValueOrDefault("lines", 10u)}");
        Console.WriteLine($"Colour: {_optionsProcessor.GetValueOrDefault("colour", "white")}");
        return;
    }
}

```

Example invocation: `FileUtil Print -n10 file.txt --colour=red`.

The caller explicitly supplies the root name. Ordinary parse failures print diagnostics
and return `IsValid=false`; invalid definitions throw `OptionsSpecificationException`.
Help is printed automatically when requested or when a container is invoked alone.
`PrintHelp()` prints help again for the selected target. The library never calls
`Environment.Exit`.

Options are case-sensitive. An option can be looked up in the command implmentation by 
either its long name or short name. Option lookup omitsdashes. Typed getters require the exact 
declared C# type. Repeated option values are ordered.


## Testing and output control

```csharp
using var output = new StringWriter();
using var errors = new StringWriter();
var options = OptionsProcessor.FromText(definitions, "FileUtil",
    new[] { "help", "Print" },
    new OptionsProcessorSettings
    {
        Output = output,
        Error = errors,
        TerminalWidth = 60,
        ColourMode = ColourMode.Never
    });
```

## Help text special formatting

Colour, bold and italic text is supported in help text. Colour is specified as follows:

```
== print - print a file
This command prints a file on [cyan]#the console#.
```
The following colour names are supported: 
    black, red, green, yellow, blue, magenta, cyan, white, bright-black, bright-red, bright-green, bright-yellow, 
    bright-blue, bright-magenta, bright-cyan, bright-white

Bold is indicated with asterisks `copy *the only* file` and italics with underscores `edit _the_ file`.

