using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cumber;

internal static partial class HelpRenderer
{
    private static readonly Dictionary<string, int> Colours = new(StringComparer.Ordinal)
    {
        ["black"] = 30,
        ["red"] = 31,
        ["green"] = 32,
        ["yellow"] = 33,
        ["blue"] = 34,
        ["magenta"] = 35,
        ["cyan"] = 36,
        ["white"] = 37,
        ["bright-black"] = 90,
        ["bright-red"] = 91,
        ["bright-green"] = 92,
        ["bright-yellow"] = 93,
        ["bright-blue"] = 94,
        ["bright-magenta"] = 95,
        ["bright-cyan"] = 96,
        ["bright-white"] = 97
    };
    [GeneratedRegex(@"\x1b\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex AnsiPattern();
    [GeneratedRegex(@"\S+")]
    private static partial Regex WordPattern();

    public static void Validate(CommandDefinition command)
    {
        Format(command.Description, false, command.Line);
        foreach (var part in command.Parts)
        {
            if (part is TextPart text) Format(text.Text, false, command.Line);
            if (part is OptionPart option) Format(option.Option.Description, false, option.Option.Line);
        }
        foreach (var child in command.Children) Validate(child);
    }

    public static void Print(CommandDefinition command, OptionsProcessorSettings settings)
    {
        int width = settings.TerminalWidth ?? TerminalWidth();
        bool colour = settings.ColourMode == ColourMode.Always ||
            settings.ColourMode == ColourMode.Auto && ReferenceEquals(settings.Output, Console.Out) &&
            !Console.IsOutputRedirected;
        void Write(string line) => settings.Output.WriteLine(line);
        string FormatText(string text, int line) => Format(text, colour, line);
        var options = command.EffectiveOptions;
        int descriptionColumn = options.Count == 0 ? 0 :
            options.Max(o => o.Indent + VisibleWidth(o.DisplayDeclaration)) + 1;
        WriteWrapped(command.Name + (command.Description.Length == 0 ? "" :
            " - " + FormatText(command.Description, command.Line)), "", "", width, Write);
        foreach (var part in command.Parts)
        {
            switch (part)
            {
                case TextPart text:
                    int spaces = text.Text.TakeWhile(c => c == ' ').Count();
                    string prefix = new(' ', spaces);
                    WriteWrapped(FormatText(text.Text[spaces..], command.Line), prefix, prefix, width, Write);
                    break;
                case OptionPart option:
                    PrintOption(option.Option);
                    break;
                case ChildrenPart children:
                    var parent = command.CustomHelp ? command.Parent! : command;
                    var entries = parent.Children.Where(c => !c.CustomHelp).ToList();
                    if (entries.Count == 0) break;
                    int names = entries.Max(c => VisibleWidth(c.Name));
                    foreach (var child in entries)
                    {
                        string start = new string(' ', children.Indent) + child.Name;
                        if (child.Description.Length == 0) { Write(start); continue; }
                        start += new string(' ', names - VisibleWidth(child.Name)) + " - ";
                        WriteWrapped(FormatText(child.Description, child.Line), start,
                            new string(' ', VisibleWidth(start)), width, Write);
                    }
                    break;
            }
        }
        var inherited = options.Where(o => !command.LocalOptions.Contains(o)).ToList();
        if (inherited.Count > 0)
        {
            Write("");
            Write("Inherited options:");
            foreach (var option in inherited) PrintOption(option);
        }
        if (colour) settings.Output.Write("\u001b[0m");

        void PrintOption(OptionDefinition option)
        {
            string declaration = option.DisplayDeclaration;
            string start = new string(' ', option.Indent) +
                (colour ? "\u001b[1m" + declaration + "\u001b[22m" : declaration);
            string description = FormatText(option.Description, option.Line);
            if (description.Length == 0) { Write(start); return; }
            if (descriptionColumn >= width - 1)
            {
                Write(start);
                string smallIndent = new(' ', Math.Min(option.Indent + 2, Math.Max(0, width - 1)));
                WriteWrapped(description, smallIndent, smallIndent, width, Write);
            }
            else
            {
                start += new string(' ', Math.Max(1, descriptionColumn - VisibleWidth(start)));
                WriteWrapped(description, start, new string(' ', descriptionColumn), width, Write);
            }
        }
    }

    private static int TerminalWidth()
    {
        try { return Console.IsOutputRedirected || Console.WindowWidth <= 0 ? 80 : Console.WindowWidth; }
        catch (IOException) { return 80; }
        catch (PlatformNotSupportedException) { return 80; }
    }

    private static void WriteWrapped(string text, string first, string continuation,
        int width, Action<string> write)
    {
        var words = WordPattern().Matches(text);
        if (words.Count == 0) { write(first.TrimEnd()); return; }
        var line = new StringBuilder(first);
        int cells = VisibleWidth(first);
        bool hasWord = false;
        foreach (Match match in words)
        {
            string word = match.Value;
            int wordWidth = VisibleWidth(word);
            if (hasWord && cells + 1 + wordWidth > width)
            {
                write(line.ToString());
                line.Clear().Append(continuation);
                cells = VisibleWidth(continuation);
                hasWord = false;
            }
            if (hasWord) { line.Append(' '); cells++; }
            line.Append(word);
            cells += wordWidth;
            hasWord = true;
        }
        write(line.ToString());
    }

    internal static int VisibleWidth(string text)
    {
        text = AnsiPattern().Replace(text, "");
        int width = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or
                UnicodeCategory.Format or UnicodeCategory.Control) continue;
            int v = rune.Value;
            bool wide = v >= 0x1100 && (v <= 0x115F || v is 0x2329 or 0x232A ||
                v >= 0x2E80 && v <= 0xA4CF && v != 0x303F ||
                v >= 0xAC00 && v <= 0xD7A3 || v >= 0xF900 && v <= 0xFAFF ||
                v >= 0xFE10 && v <= 0xFE19 || v >= 0xFE30 && v <= 0xFE6F ||
                v >= 0xFF00 && v <= 0xFF60 || v >= 0xFFE0 && v <= 0xFFE6 ||
                v >= 0x1F300 && v <= 0x1FAFF || v >= 0x20000 && v <= 0x3FFFD);
            width += wide ? 2 : 1;
        }
        return width;
    }

    private static int Closing(string text, char delimiter, int start)
    {
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == '\\') { i++; continue; }
            if (text[i] == delimiter) return i;
        }
        return -1;
    }

    private static string Literal(string text)
    {
        var result = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && "\\*_#[]|".Contains(text[i + 1])) i++;
            result.Append(text[i]);
        }
        return result.ToString();
    }

    private static string Format(string text, bool colour, int line)
    {
        var output = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length && "\\*_#[]|".Contains(text[i + 1]))
            { output.Append(text[++i]); continue; }
            if (c is '*' or '_')
            {
                int end = Closing(text, c, i + 1);
                if (end > i + 1)
                {
                    string open = c == '*' ? "1" : "3";
                    string close = c == '*' ? "22" : "23";
                    if (colour) output.Append("\u001b[").Append(open).Append('m');
                    output.Append(Literal(text[(i + 1)..end]));
                    if (colour) output.Append("\u001b[").Append(close).Append('m');
                    i = end;
                    continue;
                }
            }
            if (c == '[')
            {
                int bracket = text.IndexOf(']', i + 1);
                if (bracket >= 0 && bracket + 1 < text.Length && text[bracket + 1] == '#')
                {
                    int end = Closing(text, '#', bracket + 2);
                    if (end >= 0)
                    {
                        string style = text[(i + 1)..bracket];
                        string? code = null;
                        string reset = "39";
                        if (style == "underline") { code = "4"; reset = "24"; }
                        else if (Colours.TryGetValue(style, out int named)) code = named.ToString(CultureInfo.InvariantCulture);
                        else if (style.Contains(','))
                        {
                            var components = style.Split(',');
                            if (components.Length != 3 || components.Any(p =>
                                !int.TryParse(p.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ||
                                n is < 0 or > 255))
                                throw new OptionsSpecificationException("RGB components must be integers from 0 to 255.", line);
                            code = "38;2;" + string.Join(";", components.Select(p => p.Trim()));
                        }
                        if (code is not null)
                        {
                            if (colour) output.Append("\u001b[").Append(code).Append('m');
                            output.Append(Literal(text[(bracket + 2)..end]));
                            if (colour) output.Append("\u001b[").Append(reset).Append('m');
                            i = end;
                            continue;
                        }
                    }
                }
            }
            output.Append(c);
        }
        string result = output.ToString();
        return colour ? result : AnsiPattern().Replace(result, "");
    }
}
