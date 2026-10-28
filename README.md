# Cumber

A C# library that derives command-line options and help from one readable definition.
Targets .NET 8 (and can be used by later .NET applications). The library has no external
runtime dependencies. Parsing, help formatting, and definition validation are separate
internal components; the public entry point is a static factory.

## Example

```csharp
using HelpTextOptions;

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

The included xUnit project covers aliases, repeated input values, short groups,
multi-root selection, inheritance barriers, command boundaries, help, validation,
and formatting. Add further tests to `tests/HelpTextOptions.Tests`.

## Build and test

```sh
dotnet build HelpTextOptions.sln -c Release
dotnet test HelpTextOptions.sln -c Release
dotnet pack src/HelpTextOptions/HelpTextOptions.csproj -c Release -o artifacts
```

## NuGet publishing

The project enables packing, includes this README and XML API documentation, and
produces symbol packages. Before publishing, choose a globally unique PackageId,
replace YOUR_NAME, set a release Version, and add your chosen license and repository
metadata to the project. No license is asserted on your behalf.

```sh
dotnet nuget push artifacts/HelpTextOptions.0.1.0.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY
```

Packing is supported; publishing is intentionally a caller action. Do not commit API keys.

## Specification and practical limits

See `specification.md` for the consolidated behavioural specification. The specification
contains a few implementation-chosen defaults, which are identified in its final section.

Terminal-cell width uses a practical Unicode range approximation. Combining marks
and ANSI style sequences do not count toward width; common CJK and emoji code points
count as two cells. Complex emoji grapheme clusters and terminal-specific font behaviour
may differ. Styling does not nest and does not cross source lines. Ordinary source
newlines are preserved; option-description continuations join with spaces.
