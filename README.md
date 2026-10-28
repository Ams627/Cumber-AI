# Help-text-driven command-line parser specification

Revision: 2026-10-14

This is a plain-text/Markdown specification, despite the historical .adoc filename. The command-definition language described below is its own small language, with selected AsciiDoc-style formatting. This document specifies behaviour and the proposed public API; it contains no parser implementation.

## 1. Purpose and construction

Implement a C# command-line parser whose command structure, option definitions, and help output come from one text string. The design is inspired by docopt but does not parse docopt usage expressions.

Creation uses the static factory method:

    IOptionsProcessor processor =
        OptionsProcessor.FromText(commandText, programName, args);

`args` is the already-tokenised argument array supplied to C# Main. The parser does not split a shell command string or process shell quoting. `programName` is supplied explicitly by the caller, normally without directory or extension. The parser does not derive it from the running executable; tests may select any defined root.

Invalid definition text throws a specification exception. Invalid user arguments return an invalid processor and print diagnostics before returning. The parser never terminates the process.

## 2. Command headings and roots

Headings start at column zero. One or more equals signs followed by whitespace specify the level:

    = FileUtil - a file utility
    == Print - print a file | runnable=true
    === Preview - preview a file

The first heading must be level 1. Increasing depth by more than one level is invalid. Returning to any existing shallower level is valid. Each heading belongs to the nearest preceding heading one level shallower.

A heading contains a command name, an optional short description introduced by ` - `, and optional directives introduced by ` | `. Neither the description nor directives form part of the command name.

Multiple level-one sections define independent program roots. Select the root using the supplied `programName` before resolving commands, help, or options. No root match is an error in the factory configuration and throws. Options never inherit between roots. Group definitions are shared across roots but activate only when included.

Default: root and command names are case-sensitive and use the same ASCII name-segment rules as long options, but may contain one character. Duplicate roots and duplicate sibling command names are specification errors. Identical names under different parents are valid.

## 3. Command selection and execution

The entire command path must precede options and positional arguments. Starting at the selected root, consume consecutive arguments matching immediate child commands.

Command matching ends permanently upon encountering an option, the option terminator, or the first positional argument. Later words cannot select subcommands.

A leaf command is runnable by default. A command with children is a container by default: invoking it alone displays its help and does not request execution. A heading directive `runnable=true` permits a parent to execute without selecting a child. Default: `runnable=false` is also accepted to make any command a container.

For a container with children, an unrecognised ordinary word while selecting its child is an invalid-command error. For a runnable parent, a word not matching a child begins its positional arguments. Consequently a matching child name selects that child; use `--` to pass such a word positionally to a runnable parent.

Options between command levels are not supported. For example, `FileUtil Print -n10 file.txt --colour=red` selects Print. A later subcommand-like word is positional, not a continuation of the command path.

If a container is invoked with options but no child, validate supplied options before showing help; bypass minimum-occurrence checks because no command will execute. Unknown supplied options remain errors.

## 4. Option names and aliases

Short options use a single dash and exactly one letter or digit. Long options use two dashes and a name of at least two characters.

Names are case-sensitive. Default: letters and digits mean ASCII A–Z, a–z, and 0–9. Long names may contain dash and underscore as separators. Each separator must have a letter or digit immediately on both sides: no leading, trailing, consecutive, or mixed consecutive separators. Short names cannot contain separators.

Examples: `-a`, `-1`, `--all`, `--maxCount`, `--max-count`, and `--max_count` are valid. `--a`, `--_all`, `--all-`, `--max--count`, and `--max_-count` are invalid.

A short and long name on the same definition line are aliases of one logical option. Presence, values, occurrence counts, and validation are identical through either alias. `-a --all` counts as two occurrences when they are aliases.

## 5. Option-definition lines

An unescaped line whose first non-whitespace content is a valid option declaration defines an option. Leading indentation controls help display and does not prevent recognition.

Supported declaration forms:

    -a  Include all items
    --all  Include all items
    -a, --all  Include all items
    -n, --lines  Number of lines | type=int

The comma immediately follows the short name. Whitespace after it is optional. Aliases must appear short first, long second. At least one whitespace character separates the final name from a description; an empty description is permitted. Parameter placeholders are not part of declaration syntax: `type` determines whether a value is required.

An indented line following an option definition continues its description, regardless of indentation amount, unless it starts another recognised definition, directive, comment, or heading. Blank lines end continuation. Default: consecutive description lines join with a space before wrapping.

Placing a backslash immediately before an option-looking declaration suppresses its interpretation as an option; the backslash is removed in displayed help. A line whose first non-whitespace character is `#` is a comment and is not displayed. Inline `#` is ordinary text except where used by supported formatting markup.

Malformed option-looking declarations are specification errors rather than silently ignored. Ordinary prose may be displayed literally using the escape convention.

## 6. Directives

The delimiter is a pipe surrounded by whitespace (` | `), outside formatting markup. The delimiter and directives are not displayed. A literal pipe can be escaped as `\|`. Directives are separated by whitespace, are case-sensitive, and may not be duplicated. Unknown directives and invalid values are specification errors.

Option directives:

| Directive | Meaning | Default |
| --- | --- | --- |
| minOccurs=N | Minimum logical-option occurrences | 0 |
| maxOccurs=N | Maximum logical-option occurrences | 1 |
| maxOccurs=* | Unlimited occurrences | — |
| type=int | Required Int32 value | — |
| type=uint | Required UInt32 value | — |
| type=string | Required string value | — |
| type=float | Required Single value | — |

Without `type`, an option is parameterless. The notation `type=[int, uint, string, float]` in the original draft described alternatives; literal bracket syntax is not used.

Occurrence limits must be nonnegative integers, and a finite maximum cannot be less than the minimum. Repeated values are retained in command-line order. Parameterless repetitions have a count but no values.

Heading directives are `runnable=true`, `runnable=false`, and the flag `noinherit`.

## 7. Command-line option syntax

Long-valued options accept either form:

    --lines=10
    --lines 10

Short-valued options accept a separate or attached value:

    -n 10
    -n10

Equals signs are not allowed as the short-option value delimiter: `-n=10` is invalid. A parameterless option cannot receive an equals value.

Scan short groups from left to right. Parameterless letters or digits each identify an option. On reaching a value-taking option, all remaining characters are its value; if none remain, consume the next argument. No further short options are recognised within that argument.

Examples, assuming b and c are parameterless and n takes an integer:

    -bc           means -b -c
    -bcn10        means -b -c -n 10
    -bcn 10       means -b -c -n 10

If a takes a string, `-abc` means `-a` with value `bc`. This supersedes the original restriction that only the final character of a group may require a value.

Attempt normal short-option interpretation first. If it fails structurally and the entire single-dash name matches a long option, report that two dashes are required. A structurally valid short parse wins even when the same long name exists. Type-conversion failure does not reinterpret a structurally valid parse as a long option.

Default value-boundary rules: numeric options may consume negative numbers and report conversion/range errors for invalid values. A separate string value beginning with a dash is treated as a missing value; supply it with an attached form, such as `--name=--all`. Empty strings supplied as `--name=` or as an empty argument are valid. Numeric empty values are invalid. A missing next argument is an error. `--` is never consumed as a separate option value.

Default numeric conversion is invariant-culture. Integers accept an optional sign and decimal digits, subject to their type range. Float accepts a decimal point and scientific notation; NaN and infinity are rejected. Thousands separators are rejected. No implicit cross-type conversion occurs when retrieving values.

## 8. Positional arguments and terminator

After command selection, any argument that is neither an option nor an option value is positional. Options may occur before, between, and after positional arguments.

    FileUtil Print -n10 file.txt --colour=red

This selects Print, sets lines to 10 and colour to red, and retains file.txt as positional.

`--` ends command matching and option parsing. All subsequent arguments are positional, including words resembling commands, options, or another terminator. The terminator itself is not retained. A bare `-` is positional by default. Positional arguments preserve their original values and order; their number and meaning are validated by the calling application, not this specification language.

## 9. Inheritance

Options inherit from ancestors by default. They can be used after the complete selected command path and appear in the selected command's help.

`noinherit` blocks all ancestor options at that heading. Its own options can still inherit into descendants. The effective option set is the nearest parent's effective set, unless blocked, plus locally defined and included options.

Duplicate names or aliases within an effective option set are specification errors; there is no silent override. Including the same group twice, or including a group already inherited, causes an error if it duplicates options.

Default help placement: show inherited options in an automatically generated “Inherited options:” block after the local help text, retaining ancestor order. Local and inherited descriptions use a common alignment calculation.

## 10. Reusable option groups

Groups are defined before the first level-one heading:

    @group common
    -v, --verbose  Verbose output
    -c, --colour  Output colour | type=string

    = FileUtil - a file utility

One blank line ends a group: two consecutive newline sequences after its final content line. LF and CRLF are both supported; whitespace-only lines count as blank. Another group declaration or the first root heading also closes the preceding group.

Only option definitions, description continuations, and comments may occur inside a group. Groups cannot include other groups. All other text before the first root heading is ignored and may contain notes or comments. Group declarations are only valid in this preamble.

Include a group in a command section:

    Options:
      @usegroup common

`@usegroup name` must be alone on its line apart from leading whitespace. It inserts option definitions for parsing and help at that position. Each inserted option line begins at the visible column formerly occupied by `@`; definition indentation is relative to that base. Groups do not activate automatically. Include a group at a root to expose it through normal inheritance.

Default: group names follow command-name rules. Duplicate groups, unknown groups, malformed inclusion lines, and resulting option collisions are specification errors.

## 11. Help routing

Each program root has an implicit `help` command:

    FileUtil help
    FileUtil help Print

Help resolves an exact child-command path within the selected root. An unknown target prints an error and produces an invalid result. Help arguments are target names, not execution options. Required-option checks are bypassed for help.

An immediate root child `== help` customises the output of the implicit help command with no target. Without it, root help is printed. A target such as `help remote add` always prints that target section and ignores the custom help section.

The custom help section is help text, not an executable command. Default: it cannot have children, option definitions, group inclusions, or heading directives. It can contain formatting and `$(subcommands)`, which enumerates the root's normal children. The implicit/special help command is omitted from child listings. The name `help` is reserved for this immediate-root role.

`PrintHelp()` takes no parameters and prints help for the target already selected from `args`. After a normal successful parse, this is the selected execution command. After an explicit help request, it is the requested help target. With no target and a custom help section, it prints that section. Container automatic help prints the container's own section.

Help output is printed automatically for help requests and container-only invocations. Calling PrintHelp again prints it again. It does not alter the parse result. After invalid arguments, it prints help for the deepest successfully selected command.

## 12. Subcommand macro

`$(subcommands)` must be alone on its line apart from leading whitespace. The spelling is case-sensitive.

It expands to immediate child commands only, in definition order, with one entry per child:

    Print - print a file
    Copy - copy a file

Every entry starts at the visible column occupied by `$`. A child without a short description prints only its name. Default: descriptions align in a shared column using the widest child name; wrapped description text aligns with its description column. With no children, the macro emits no lines. Unknown standalone macros are specification errors.

## 13. Help rendering

Help includes the selected heading's command name and short description, followed by its body; heading markers and directives are omitted. Descendant section bodies are not included automatically.

Supported formatting:

    *bold*
    _italic_
    [underline]#underlined#
    [green]#coloured text#
    [123,96,45]#RGB colour#

Default colours: black, red, green, yellow, blue, magenta, cyan, white, and their bright variants named bright-black through bright-white. RGB components are integers from 0 to 255; optional spaces after commas are allowed. Default: formatting spans do not nest or cross source lines. Backslash escapes formatting delimiters and backslash itself. Unmatched markup is displayed literally rather than failing the specification. Recognised but invalid RGB values are specification errors.

Option declarations are displayed in bold. Descriptions align across the selected command's effective option set, allowing at least one space after the widest displayed declaration and accounting for definition/group indentation. Wrapped descriptions align with their description start. Default: show value placeholders INT, UINT, STRING, or FLOAT after declarations generated from type.

Use terminal width when available, otherwise 80 columns. Permit an explicit width override for tests. Wrap at word boundaries, never within words. A word wider than available space may overflow. If the normal description column leaves no room, place descriptions below their option declarations with a small indentation.

Formatting markup and ANSI colour/style sequences have zero display width. Width is measured in terminal cells where practical, rather than raw string length. Default: tabs expand to four-column stops. Preserve blank lines and explicit source line breaks in ordinary help prose; wrap each line independently. Option-description continuations are joined as specified earlier.

Default: colour/style output is enabled for interactive terminals and disabled for redirected output; markup is removed in plain output. Provide a factory override for this behaviour. Style resets must prevent formatting from leaking beyond help output.

## 14. Diagnostics and result state

Specification failures throw an OptionsSpecificationException (proposed name), with line number and relevant heading/definition where available. Examples include malformed hierarchy, invalid directives, duplicate options, and unknown groups. Null arguments and invalid factory configuration use appropriate standard argument exceptions.

Command-line failures do not throw solely because the user supplied bad arguments. Print concise diagnostics to the error writer, return IsValid=false, and retain the diagnostics in Errors. Default: stop at the first argument error to avoid misleading cascading errors; occurrence validation may report several independently missing required options.

Do not automatically print full help after errors. Include a brief suggestion showing the appropriate help command. The caller can invoke PrintHelp explicitly.

Successful explicit or automatic help sets IsValid=true and HelpRequested=true. Normal valid execution sets IsValid=true and HelpRequested=false. An invalid help request sets IsValid=false and HelpRequested=true. An invalid normal invocation sets IsValid=false and HelpRequested=false.

Callers execute their command only when IsValid && !HelpRequested. Retrieval of parsed option values or presence on an invalid result throws InvalidOperationException, preventing reliance on partial parsing. ProgramName, CommandPath, Errors, HelpRequested, IsValid, and PrintHelp remain available.

## 15. Proposed public API

The static factory returns IOptionsProcessor. Suggested interface:

```csharp
public interface IOptionsProcessor
{
    bool IsValid { get; }
    bool HelpRequested { get; }
    string ProgramName { get; }
    IReadOnlyList<string> CommandPath { get; }
    IReadOnlyList<string> PositionalArguments { get; }
    IReadOnlyList<string> Errors { get; }

    bool IsOptionPresent(string name);
    bool IsOptionPresent(char name);
    int GetOccurrenceCount(string name);
    int GetOccurrenceCount(char name);

    T GetValue<T>(string name);
    T GetValue<T>(char name);
    T GetValueOrDefault<T>(string name, T defaultValue);
    T GetValueOrDefault<T>(char name, T defaultValue);
    IReadOnlyList<T> GetValues<T>(string name);
    IReadOnlyList<T> GetValues<T>(char name);

    void PrintHelp();
}
```

CommandPath excludes the program root and records the selected command names. For help, it records the target command path, not the literal help token. PositionalArguments excludes command tokens, option tokens, option values, and the terminator. Returned collections are read-only snapshots.

Lookup names omit dashes. Strings identify long aliases and chars identify short aliases. IsOptionPresent("all") equals IsOptionPresent('a') when those names are aliases.

Unknown lookup names are calling-code errors and throw ArgumentException. GetValue requires exactly one value. GetValueOrDefault returns its supplied default if the option is absent, requires exactly one value otherwise, and rejects multiple values. GetValues returns all values in argument order or an empty list. Value retrieval on a parameterless option throws InvalidOperationException. Requesting a T inconsistent with the declared type throws; no implicit conversion is performed. Use int, uint, string, and float for their respective declared types.

The factory should accept optional configuration for output and error TextWriters, terminal width, and colour mode without complicating its common three-argument use. Defaults are Console.Out, Console.Error, detected width or 80, and automatic colour mode. Exact configuration-object naming is an implementation choice.

## 16. Complete definition example

```text
Notes before the first root are ignored outside groups.

@group common
-v, --verbose  Verbose output
-c, --colour  Output colour | type=string

= FileUtil - a file utility
Common options:
  @usegroup common

Commands:
  $(subcommands)

== help - help overview
Use FileUtil help COMMAND for detailed command help.
Available commands:
  $(subcommands)

== Print - print a file
-n, --lines  Maximum number of lines | type=uint

== Merge - merge files
-i, --input  Input file | type=string minOccurs=1 maxOccurs=*

== Remote - list configured remotes | runnable=true
  $(subcommands)

=== Add - add a remote

=== Remove - remove a remote

== Raw - process raw data | noinherit
-i, --input  Input file | type=string

= Split - split files
-n, --parts  Number of parts | type=uint minOccurs=1
```

Selecting programName FileUtil or Split gives independent behaviour from the same definition text. Print inherits verbose and colour. Raw blocks them. Merge accepts repeated input values. Remote can execute directly as well as select Add or Remove.

## 17. Defaults chosen during consolidation

These details complete areas left to the implementer and are deliberately easy to revise: ASCII identifiers; case-sensitive command/group names; explicit runnable=false; nonnested groups and formatting; invariant numeric conversion; restricted separate dash-leading string values; read-only results; first-error argument diagnostics; generated inherited-option blocks; special help-section restrictions; automatic plain output when redirected; and the exact generic retrieval interface proposed above.

The central user-agreed rules are the heading hierarchy and multiple explicit roots, contiguous command paths, runnable=true, noinherit, case-sensitive option names with nonconsecutive internal separators, attached short values, repeated options with default occurrence limits 0/1, pipe directives, -- termination, positional arguments interleaved with options, implicit/custom help, word-safe width-aware formatting, child-only indented subcommand expansion, and reusable preamble groups.
