using System.Text.RegularExpressions;

namespace Cumber;

internal sealed class OptionDefinition
{
    public string? Short { get; init; }
    public string? Long { get; init; }
    public string? Type { get; init; }
    public int Minimum { get; init; }
    public int? Maximum { get; init; } = 1;
    public int Line { get; init; }
    public int Indent { get; init; }
    public string Description { get; set; } = string.Empty;
    public string DisplayName => Short is null ? "--" + Long : Long is null ? "-" + Short : "-" + Short + ", --" + Long;
    public string DisplayDeclaration => DisplayName + (Type is null ? string.Empty : " " + Type.ToUpperInvariant());
}

internal abstract record HelpPart;
internal sealed record TextPart(string Text) : HelpPart;
internal sealed record OptionPart(OptionDefinition Option) : HelpPart;
internal sealed record ChildrenPart(int Indent) : HelpPart;

internal sealed class CommandDefinition
{
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public int Line { get; init; }
    public CommandDefinition? Parent { get; set; }
    public List<CommandDefinition> Children { get; } = [];
    public List<HelpPart> Parts { get; } = [];
    public List<OptionDefinition> LocalOptions { get; } = [];
    public List<OptionDefinition> EffectiveOptions { get; } = [];
    public bool? RunnableOverride { get; init; }
    public bool NoInherit { get; init; }
    public bool CustomHelp => Parent?.Parent is null && Parent is not null && Name == "help";
    public bool Runnable => RunnableOverride ?? !Children.Any(c => !c.CustomHelp);
}

internal static partial class DefinitionParser
{
    [GeneratedRegex(@"^[A-Za-z0-9]+(?:[-_][A-Za-z0-9]+)*$")]
    private static partial Regex NamePattern();
    [GeneratedRegex(@"^(?<short>-[A-Za-z0-9])(?:,\s*(?<long>--[A-Za-z0-9][A-Za-z0-9_-]*))?(?=\s|$)|^(?<long>--[A-Za-z0-9][A-Za-z0-9_-]*)(?=\s|$)")]
    private static partial Regex OptionPattern();
    [GeneratedRegex(@"^=+\s+")]
    private static partial Regex HeadingPattern();

    internal static bool ValidName(string name) => NamePattern().IsMatch(name);
    private static OptionsSpecificationException Fail(string message, int line) => new(message, line);

    public static Dictionary<string, CommandDefinition> Parse(string text)
    {
        var roots = new Dictionary<string, CommandDefinition>(StringComparer.Ordinal);
        var groups = new Dictionary<string, List<OptionDefinition>>(StringComparer.Ordinal);
        var stack = new List<CommandDefinition>();
        List<OptionDefinition>? group = null;
        OptionDefinition? continuation = null;
        CommandDefinition? current = null;
        bool preamble = true;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string line = ExpandTabs(lines[index]);
            string trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;
            if (trimmed.StartsWith('#')) continue;
            if (line.StartsWith('='))
            {
                group = null;
                continuation = null;
                if (!HeadingPattern().IsMatch(line)) throw Fail("Malformed command heading.", lineNumber);
                int level = line.TakeWhile(c => c == '=').Count();
                if (level > stack.Count + 1) throw Fail("Heading skips a command level.", lineNumber);
                var (heading, directives) = SplitDirectives(line[level..].Trim(), lineNumber);
                int descriptionAt = heading.IndexOf(" - ", StringComparison.Ordinal);
                string name = (descriptionAt < 0 ? heading : heading[..descriptionAt]).Trim();
                string description = descriptionAt < 0 ? "" : heading[(descriptionAt + 3)..].Trim();
                if (!ValidName(name)) throw Fail("Invalid command name.", lineNumber);
                ValidateDirectives(directives, ["runnable", "noinherit"], lineNumber);
                bool? runnable = null;
                if (directives.TryGetValue("runnable", out var value))
                {
                    if (value is not ("true" or "false")) throw Fail("runnable must be true or false.", lineNumber);
                    runnable = value == "true";
                }
                if (directives.TryGetValue("noinherit", out value) && value is not null)
                    throw Fail("noinherit is a flag.", lineNumber);
                var command = new CommandDefinition
                {
                    Name = name,
                    Description = description,
                    Line = lineNumber,
                    RunnableOverride = runnable,
                    NoInherit = directives.ContainsKey("noinherit")
                };
                if (level == 1)
                {
                    if (!roots.TryAdd(name, command)) throw Fail($"Duplicate program root '{name}'.", lineNumber);
                }
                else
                {
                    command.Parent = stack[level - 2];
                    if (command.Parent.Children.Any(c => c.Name == name))
                        throw Fail($"Duplicate sibling command '{name}'.", lineNumber);
                    command.Parent.Children.Add(command);
                }
                if (stack.Count >= level) stack.RemoveRange(level - 1, stack.Count - level + 1);
                stack.Add(command);
                current = command;
                preamble = false;
                if (command.CustomHelp && directives.Count != 0)
                    throw Fail("The custom help section cannot have directives.", lineNumber);
                if (command.Parent?.CustomHelp == true)
                    throw Fail("The custom help section cannot have children.", lineNumber);
                continue;
            }

            if (trimmed.StartsWith("@group", StringComparison.Ordinal))
            {
                if (!preamble) throw Fail("Groups must be defined before the first root.", lineNumber);
                string name = ReadInstruction(trimmed, "@group", lineNumber);
                if (!groups.TryAdd(name, group = [])) throw Fail($"Duplicate group '{name}'.", lineNumber);
                continuation = null;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                group = null;
                continuation = null;
                if (!preamble) current!.Parts.Add(new TextPart(""));
                continue;
            }

            if (preamble && group is null) continue;

            if (trimmed.StartsWith("@usegroup", StringComparison.Ordinal))
            {
                if (preamble || current!.CustomHelp) throw Fail("Group inclusion is not allowed here.", lineNumber);
                string name = ReadInstruction(trimmed, "@usegroup", lineNumber);
                if (!groups.TryGetValue(name, out var options)) throw Fail($"Unknown group '{name}'.", lineNumber);
                foreach (var original in options)
                {
                    var option = new OptionDefinition
                    {
                        Short = original.Short,
                        Long = original.Long,
                        Type = original.Type,
                        Minimum = original.Minimum,
                        Maximum = original.Maximum,
                        Line = lineNumber,
                        Indent = indent + original.Indent,
                        Description = original.Description
                    };
                    current.LocalOptions.Add(option);
                    current.Parts.Add(new OptionPart(option));
                }
                continuation = null;
                continue;
            }

            if (trimmed.StartsWith('-'))
            {
                var option = ParseOption(trimmed, indent, lineNumber);
                if (preamble) group!.Add(option);
                else
                {
                    if (current!.CustomHelp) throw Fail("The custom help section cannot define options.", lineNumber);
                    current.LocalOptions.Add(option);
                    current.Parts.Add(new OptionPart(option));
                }
                continuation = option;
                continue;
            }

            if (trimmed.StartsWith("$(", StringComparison.Ordinal))
            {
                if (trimmed != "$(subcommands)" || preamble)
                    throw Fail("Unknown macro or macro not alone on its line.", lineNumber);
                current!.Parts.Add(new ChildrenPart(indent));
                continuation = null;
                continue;
            }

            if (trimmed.Contains("$(subcommands)", StringComparison.Ordinal))
                throw Fail("The subcommands macro must be alone on its line.", lineNumber);
            if (trimmed.StartsWith("\\-", StringComparison.Ordinal))
            {
                if (preamble) throw Fail("Groups may contain only options and their continuations.", lineNumber);
                current!.Parts.Add(new TextPart(new string(' ', indent) + trimmed[1..]));
                continuation = null;
                continue;
            }

            if (indent > 0 && continuation is not null)
            {
                continuation.Description += (continuation.Description.Length == 0 ? "" : " ") + trimmed;
                continue;
            }

            continuation = null;
            if (preamble) throw Fail("Groups may contain only options and their continuations.", lineNumber);
            if (trimmed.StartsWith('@')) throw Fail("Unknown instruction.", lineNumber);
            current!.Parts.Add(new TextPart(line));
        }

        if (roots.Count == 0) throw Fail("No program roots are defined.", 1);

        foreach (var options in groups.Values)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in options)
                if (option.Short is not null && !names.Add("-" + option.Short) ||
                    option.Long is not null && !names.Add("--" + option.Long))
                    throw Fail("Duplicate option in group.", option.Line);
        }
        foreach (var root in roots.Values)
        {
            BuildEffective(root);
        }
        return roots;
    }

    private static string ReadInstruction(string text, string instruction, int line)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[0] != instruction || !ValidName(parts[1]))
            throw Fail($"Expected {instruction} NAME.", line);
        return parts[1];
    }

    private static string ExpandTabs(string text)
    {
        var result = new System.Text.StringBuilder();
        int column = 0;
        foreach (char c in text)
        {
            if (c == '\t')
            {
                int spaces = 4 - column % 4;
                result.Append(' ', spaces);
                column += spaces;
            }
            else { result.Append(c); column++; }
        }
        return result.ToString();
    }

    private static OptionDefinition ParseOption(string text, int indent, int line)
    {
        var (definition, directives) = SplitDirectives(text, line);
        var match = OptionPattern().Match(definition);

        if (!match.Success)
        {
            throw Fail("Malformed option declaration.", line);
        }

        string? shortName = match.Groups["short"].Success ? match.Groups["short"].Value[1..] : null;
        string? longName = match.Groups["long"].Success ? match.Groups["long"].Value[2..] : null;

        if (longName is not null && (longName.Length < 2 || !ValidName(longName)))
            throw Fail("Invalid long option name.", line);

        ValidateDirectives(directives, ["type", "minOccurs", "maxOccurs"], line);

        string? type = null;

        if (directives.TryGetValue("type", out type) && type is not ("int" or "uint" or "float" or "string"))
        {
            throw Fail("type must be int, uint, float, or string.", line);
        }

        int minimum = ReadCount(directives, "minOccurs", 0, line);
        int? maximum = directives.TryGetValue("maxOccurs", out var max) && max == "*" ? null : ReadCount(directives, "maxOccurs", 1, line);
        if (maximum < minimum)
        {
            throw Fail("maxOccurs cannot be less than minOccurs.", line);
        }

        return new OptionDefinition
        {
            Short = shortName,
            Long = longName,
            Type = type,
            Minimum = minimum,
            Maximum = maximum,
            Line = line,
            Indent = indent,
            Description = definition[match.Length..].Trim()
        };
    }

    private static int ReadCount(Dictionary<string, string?> directives, string name, int fallback, int line)
    {
        if (!directives.TryGetValue(name, out var text))
        {
            return fallback;
        }

        if (text is null || text.Length == 0 || text.Any(c => c is < '0' or > '9') ||
            !int.TryParse(text, out int count))
        {
            throw Fail($"{name} must be a nonnegative integer.", line);
        }

        return count;
    }

    private static void ValidateDirectives(Dictionary<string, string?> directives, string[] valid, int line)
    {
        foreach (var name in directives.Keys)
            if (!valid.Contains(name, StringComparer.Ordinal)) throw Fail($"Unknown directive '{name}'.", line);
    }

    internal static (string Text, Dictionary<string, string?> Directives) SplitDirectives(string line, int number)
    {
        // Hash-delimited style spans shield pipes in their contents.
        bool span = false;
        int separator = -1;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\') { i++; continue; }
            if (!span && line[i] is '*' or '_' && (i == 0 || !char.IsLetterOrDigit(line[i - 1])))
            {
                int end = i + 1;
                for (; end < line.Length; end++)
                {
                    if (line[end] == '\\') { end++; continue; }
                    if (line[end] == line[i]) break;
                }
                if (end < line.Length) { i = end; continue; }
            }

            if (line[i] == '#' && (span || i > 0 && line[i - 1] == ']'))
            {
                span = !span; continue;
            }

            if (!span && line[i] == '|' && i > 0 && i + 1 < line.Length &&
                char.IsWhiteSpace(line[i - 1]) && char.IsWhiteSpace(line[i + 1]))
            {
                separator = i; break;
            }
        }

        var directives = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (separator < 0)
        {
            return (line, directives);
        }

        foreach (string token in line[(separator + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            int equal = token.IndexOf('=');
            string name = equal < 0 ? token : token[..equal];
            string? value = equal < 0 ? null : token[(equal + 1)..];
            if (!directives.TryAdd(name, value))
            {
                throw Fail($"Duplicate directive '{name}'.", number);
            }
        }

        if (directives.Count == 0)
        {
            throw Fail("Empty directive list.", number);
        }

        return (line[..separator].TrimEnd(), directives);
    }

    private static void BuildEffective(CommandDefinition command)
    {
        if (!command.NoInherit && !command.CustomHelp && command.Parent is not null)
        {
            command.EffectiveOptions.AddRange(command.Parent.EffectiveOptions);
        }

        command.EffectiveOptions.AddRange(command.LocalOptions);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var option in command.EffectiveOptions)
        {
            if (option.Short is not null && !names.Add("-" + option.Short) ||
                option.Long is not null && !names.Add("--" + option.Long))
                throw Fail($"Duplicate option or alias in command '{command.Name}': {option.DisplayName}.", option.Line);
        }

        foreach (var child in command.Children)
        {
            BuildEffective(child);
        }
    }
}
