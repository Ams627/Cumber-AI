namespace Cumber;

/// <summary>Per-call settings; no global mutable configuration is used.</summary>
public sealed class OptionsProcessorSettings
{
    public ColourMode ColourMode { get; init; } = ColourMode.Auto;
    public TextWriter Error { get; init; } = Console.Error;
    public TextWriter Output { get; init; } = Console.Out;
    public int? TerminalWidth { get; init; }
}
