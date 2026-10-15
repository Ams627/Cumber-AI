namespace Cumber;

public interface IOptionsProcessor
{
    IReadOnlyList<string> CommandPath { get; }
    IReadOnlyList<string> Errors { get; }
    bool HelpRequested { get; }
    bool IsValid { get; }
    IReadOnlyList<string> OriginalArgs { get; }
    IReadOnlyList<string> PositionalArguments { get; }
    string ProgramName { get; }
    int GetOccurrenceCount(string name);

    int GetOccurrenceCount(char name);

    T GetValue<T>(string name);

    T GetValue<T>(char name);

    T GetValueOrDefault<T>(string name, T defaultValue);

    T GetValueOrDefault<T>(char name, T defaultValue);

    IReadOnlyList<T> GetValues<T>(string name);

    IReadOnlyList<T> GetValues<T>(char name);

    bool IsOptionPresent(string name);
    bool IsOptionPresent(char name);
    void PrintHelp();
}
