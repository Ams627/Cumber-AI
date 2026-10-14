namespace HelpTextOptions;

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

public enum ColourMode { Auto, Always, Never }

/// <summary>Per-call settings; no global mutable configuration is used.</summary>
public sealed class OptionsProcessorSettings
{
    public TextWriter Output { get; init; } = Console.Out;
    public TextWriter Error { get; init; } = Console.Error;
    public int? TerminalWidth { get; init; }
    public ColourMode ColourMode { get; init; } = ColourMode.Auto;
}

public sealed class OptionsSpecificationException : ArgumentException
{
    public int LineNumber { get; }
    public OptionsSpecificationException(string message, int lineNumber)
        : base($"Line {lineNumber}: {message}", "commandText") => LineNumber = lineNumber;
}
