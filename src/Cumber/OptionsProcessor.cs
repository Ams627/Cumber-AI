using System.Globalization;

namespace Cumber;

/// <summary>Factory and immutable public result for a single command-line invocation.</summary>
public sealed class OptionsProcessor : IOptionsProcessor
{
    private readonly CommandDefinition selected;
    private readonly CommandDefinition helpTarget;
    private readonly OptionsProcessorSettings settings;
    private readonly Dictionary<OptionDefinition, List<object>> values = [];
    private readonly Dictionary<OptionDefinition, int> counts = [];
    private readonly Dictionary<string, OptionDefinition> longOptions = new(StringComparer.Ordinal);
    private readonly Dictionary<char, OptionDefinition> shortOptions = [];

    public bool IsValid { get; }
    public bool HelpRequested { get; }
    public string ProgramName { get; }
    public IReadOnlyList<string> CommandPath { get; }
    public IReadOnlyList<string> PositionalArguments { get; }
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Parses definitions and arguments, printing help or diagnostics as required.</summary>
    public static IOptionsProcessor FromText(
            string commandText,
            string programName,
            string[] args,
            OptionsProcessorSettings? settings = null
        )
    {
        ArgumentNullException.ThrowIfNull(commandText);
        ArgumentException.ThrowIfNullOrEmpty(programName);
        ArgumentNullException.ThrowIfNull(args);

        if (args.Any(a => a is null))
        {
            throw new ArgumentException("Arguments cannot contain null.", nameof(args));
        }

        settings ??= new OptionsProcessorSettings();

        ArgumentNullException.ThrowIfNull(settings.Output);
        ArgumentNullException.ThrowIfNull(settings.Error);

        if (settings.TerminalWidth is <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Width must be positive.");

        if (!Enum.IsDefined(settings.ColourMode))
            throw new ArgumentOutOfRangeException(nameof(settings));

        var roots = DefinitionParser.Parse(commandText);

        // Validate markup in all roots before any user-facing output.
        foreach (var root in roots.Values)
        {
            HelpRenderer.Validate(root);
        }

        if (!roots.TryGetValue(programName, out var rootCommand))
            throw new ArgumentException($"No program root matches '{programName}'.", nameof(programName));

        return new OptionsProcessor(rootCommand, args.ToArray(), settings);
    }

    private OptionsProcessor(CommandDefinition root, string[] args, OptionsProcessorSettings settings)
    {
        this.settings = settings;
        ProgramName = root.Name;
        var errors = new List<string>();
        var positional = new List<string>();
        var path = new List<string>();
        var command = root;
        bool explicitHelp = args.Length > 0 && args[0] == "help";
        int index = explicitHelp ? 1 : 0;

        if (explicitHelp)
        {
            for (; index < args.Length; index++)
            {
                var child = command.Children.FirstOrDefault(c => !c.CustomHelp && c.Name == args[index]);
                if (child is null)
                {
                    errors.Add($"Unknown help target '{args[index]}'.");
                    break;
                }
                command = child;
                path.Add(child.Name);
            }
        }
        else
        {
            for (; index < args.Length; index++)
            {
                string arg = args[index];
                if (arg.StartsWith('-') && arg != "-") break;
                var child = command.Children.FirstOrDefault(c => !c.CustomHelp && c.Name == arg);
                if (child is not null)
                {
                    command = child;
                    path.Add(child.Name);
                }
                else
                {
                    if (!command.Runnable && command.Children.Any(c => !c.CustomHelp))
                        errors.Add($"Unknown command '{arg}' after '{string.Join(" ", new[] { root.Name }.Concat(path))}'.");
                    break;
                }
            }
        }
        selected = command;
        helpTarget = explicitHelp && path.Count == 0 && errors.Count == 0
            ? root.Children.FirstOrDefault(c => c.CustomHelp) ?? root : command;

        foreach (var option in selected.EffectiveOptions)
        {
            if (option.Short is not null) shortOptions.Add(option.Short[0], option);
            if (option.Long is not null) longOptions.Add(option.Long, option);
            counts.Add(option, 0);
            values.Add(option, []);
        }

        if (!explicitHelp && errors.Count == 0)
            ParseArguments(args, index, positional, errors);

        HelpRequested = explicitHelp || errors.Count == 0 && !selected.Runnable;
        if (errors.Count == 0 && !HelpRequested)
            foreach (var option in selected.EffectiveOptions)
                if (counts[option] < option.Minimum)
                    errors.Add($"Option '{option.DisplayName}' must occur at least {option.Minimum} time(s).");

        IsValid = errors.Count == 0;
        Errors = errors.AsReadOnly();
        CommandPath = path.AsReadOnly();
        PositionalArguments = positional.AsReadOnly();

        if (!IsValid)
        {
            foreach (var error in errors) settings.Error.WriteLine(error);
            settings.Error.WriteLine($"Use '{root.Name} help{(path.Count == 0 ? "" : " " + string.Join(" ", path))}' for help.");
        }
        else if (HelpRequested)
        {
            PrintHelp();
        }
    }

    private void ParseArguments(string[] args, int start, List<string> positional, List<string> errors)
    {
        bool terminated = false;
        for (int i = start; i < args.Length; i++)
        {
            string arg = args[i];
            if (terminated) { positional.Add(arg); continue; }
            if (arg == "--") { terminated = true; continue; }
            if (arg == "-" || !arg.StartsWith('-')) { positional.Add(arg); continue; }
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                int equal = arg.IndexOf('=');
                string name = equal < 0 ? arg[2..] : arg[2..equal];
                if (!longOptions.TryGetValue(name, out var option))
                { errors.Add($"Unknown option '--{name}'."); return; }
                string? attached = equal < 0 ? null : arg[(equal + 1)..];
                if (!Consume(option, attached, args, ref i, errors)) return;
                continue;
            }
            string group = arg[1..];
            // Structural preflight ensures a mistyped long name never partially mutates the result.
            string? structuralError = CheckGroup(group);
            if (structuralError is not null)
            {
                errors.Add(longOptions.ContainsKey(group)
                    ? $"Option '{arg}' requires two dashes; use '--{group}'."
                    : structuralError);
                return;
            }
            for (int j = 0; j < group.Length; j++)
            {
                var option = shortOptions[group[j]];
                string? attached = option.Type is not null && j + 1 < group.Length ? group[(j + 1)..] : null;
                if (!Consume(option, attached, args, ref i, errors)) return;
                if (option.Type is not null) break;
            }
        }
    }

    private string? CheckGroup(string group)
    {
        for (int j = 0; j < group.Length; j++)
        {
            if (!shortOptions.TryGetValue(group[j], out var option))
                return $"Unknown short option '-{group[j]}' in '-{group}'.";
            if (option.Type is not null)
                return j + 1 < group.Length && group[j + 1] == '='
                    ? $"Short option '-{group[j]}' does not accept an equals delimiter." : null;
        }
        return null;
    }

    private bool Consume(OptionDefinition option, string? attached, string[] args, ref int index, List<string> errors)
    {
        if (option.Type is null && attached is not null)
        { errors.Add($"Option '{option.DisplayName}' does not take a value."); return false; }
        object? value = null;
        if (option.Type is not null)
        {
            string? text = attached;
            if (text is null)
            {
                if (index + 1 >= args.Length || args[index + 1] == "--" ||
                    option.Type == "string" && args[index + 1].StartsWith('-'))
                { errors.Add($"Option '{option.DisplayName}' requires a value."); return false; }
                text = args[++index];
            }
            if (!TryConvert(option.Type, text, out value))
            { errors.Add($"Invalid {option.Type} value '{text}' for option '{option.DisplayName}'."); return false; }
        }
        int count = counts[option] + 1;
        if (option.Maximum is int maximum && count > maximum)
        { errors.Add($"Option '{option.DisplayName}' may occur at most {maximum} time(s)."); return false; }
        counts[option] = count;
        if (option.Type is not null) values[option].Add(value!);
        return true;
    }

    private static bool TryConvert(string type, string text, out object? value)
    {
        value = null;
        if (type == "string") { value = text; return true; }
        if (text.Length == 0 || text != text.Trim()) return false;
        if (type is "int" or "uint")
        {
            int first = text[0] is '+' or '-' ? 1 : 0;
            if (first == text.Length || text[first..].Any(c => c is < '0' or > '9')) return false;
            if (type == "int" && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int i))
            { value = i; return true; }
            if (type == "uint" && uint.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out uint u))
            { value = u; return true; }
            return false;
        }
        if (float.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f))
        { value = f; return true; }
        return false;
    }

    private OptionDefinition Find(string name)
    {
        EnsureValid();
        ArgumentNullException.ThrowIfNull(name);
        return longOptions.TryGetValue(name, out var option) ? option
            : throw new ArgumentException($"Unknown long option '{name}'.", nameof(name));
    }
    private OptionDefinition Find(char name)
    {
        EnsureValid();
        return shortOptions.TryGetValue(name, out var option) ? option
            : throw new ArgumentException($"Unknown short option '{name}'.", nameof(name));
    }
    private void EnsureValid()
    {
        if (!IsValid) throw new InvalidOperationException("Option retrieval requires a valid parse result.");
    }
    private IReadOnlyList<T> GetTyped<T>(OptionDefinition option)
    {
        Type expected = option.Type switch
        {
            "int" => typeof(int),
            "uint" => typeof(uint),
            "float" => typeof(float),
            "string" => typeof(string),
            _ => throw new InvalidOperationException("Parameterless options have no values.")
        };
        if (typeof(T) != expected)
            throw new ArgumentException($"Option '{option.DisplayName}' requires {expected.Name}, not {typeof(T).Name}.");
        return values[option].Cast<T>().ToList().AsReadOnly();
    }
    private T Single<T>(OptionDefinition option, bool allowDefault, T fallback)
    {
        var result = GetTyped<T>(option);
        if (result.Count == 0 && allowDefault) return fallback;
        if (result.Count != 1) throw new InvalidOperationException($"Option '{option.DisplayName}' must have exactly one value.");
        return result[0];
    }
    public bool IsOptionPresent(string name) => counts[Find(name)] != 0;
    public bool IsOptionPresent(char name) => counts[Find(name)] != 0;
    public int GetOccurrenceCount(string name) => counts[Find(name)];
    public int GetOccurrenceCount(char name) => counts[Find(name)];
    public T GetValue<T>(string name) => Single(Find(name), false, default(T)!);
    public T GetValue<T>(char name) => Single(Find(name), false, default(T)!);
    public T GetValueOrDefault<T>(string name, T defaultValue) => Single(Find(name), true, defaultValue);
    public T GetValueOrDefault<T>(char name, T defaultValue) => Single(Find(name), true, defaultValue);
    public IReadOnlyList<T> GetValues<T>(string name) => GetTyped<T>(Find(name));
    public IReadOnlyList<T> GetValues<T>(char name) => GetTyped<T>(Find(name));
    public void PrintHelp() => HelpRenderer.Print(helpTarget, settings);
}
