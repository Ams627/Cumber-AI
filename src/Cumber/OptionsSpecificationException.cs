namespace Cumber;

public sealed class OptionsSpecificationException : ArgumentException
{
    public OptionsSpecificationException(string message, int lineNumber)
        : base($"Line {lineNumber}: {message}", "commandText") => LineNumber = lineNumber;

    public int LineNumber { get; }
}
