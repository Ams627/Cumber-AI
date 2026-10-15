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
    public int Execute()
    {
        Console.WriteLine($"Command: {string.Join(" ", _optionsProcessor.CommandPath)}");
        Console.WriteLine($"Dest: {_optionsProcessor.GetValueOrDefault('d', "c:\\temp")}");
        return 0;
    }
}

