using Cumber;

[Cumber.Command("copy")]
[Cumber.Command("copy2")]
class Copy : ICumberCommand
{
    private readonly IOptionsProcessor _optionsProcessor;

    public Copy(IOptionsProcessor optionsProcessor, ISettings settings)
    {
        Console.WriteLine(settings.Name);
        Console.WriteLine("copy constructor called");
        _optionsProcessor = optionsProcessor;
    }
    public void Execute()
    {
        Console.WriteLine($"Command: {string.Join(" ", _optionsProcessor.CommandPath)}");
        Console.WriteLine($"Dest: {string.Join(" ", _optionsProcessor.GetValues<string>('d'))}");
        return;
    }
}

