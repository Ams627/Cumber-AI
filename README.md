# Cumber

A C# library that derives command-line options and help from one readable definition.
Targets .NET 8 (and can be used by later .NET applications). The library has no external
runtime dependencies. Parsing, help formatting, and definition validation are separate
internal components; the public entry point is a static factory.

## Example

```csharp
using Cumber;

const string definitions = """
    = FileUtil - a file utility
      $(subcommands)

    == Print - print a file
    -n, --lines  Number of lines | type=int
    -c, --colour  Output colour | type=string
    -a, --all  Include everything
    """;

IOptionsProcessor options = OptionsProcessor.FromText(definitions, "FileUtil", args);
if (!options.IsValid) return 2;
if (options.HelpRequested) return 0;

int lines = options.GetValueOrDefault("lines", 10);
string colour = options.GetValueOrDefault("colour", "white");
bool all = options.IsOptionPresent('a'); // Equivalent to IsOptionPresent("all").
foreach (string file in options.PositionalArguments)
{
    // Application-specific work goes here.
}
return 0;
```

For example: `FileUtil Print -n10 file.txt --colour=red`.

The caller explicitly supplies the root name. Ordinary parse failures print diagnostics
and return `IsValid=false`; invalid definitions throw `OptionsSpecificationException`.
Help is printed automatically when requested or when a container is invoked alone.
`PrintHelp()` prints help again for the selected target. The library never calls
`Environment.Exit`.

Options are case-sensitive. Long-name string lookups and short-name char lookups omit
dashes. Typed getters require the exact declared C# type. Repeated values are ordered.
