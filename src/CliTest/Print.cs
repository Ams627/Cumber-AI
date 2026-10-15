using Cumber;

[Cumber.Command("print")]
class Print : ICumberCommand
{
    private readonly IOptionsProcessor _optionsProcessor;

    public Print(IOptionsProcessor optionsProcessor)
    {
        Console.WriteLine("print constructor called");
        _optionsProcessor = optionsProcessor;
    }
    public int Execute()
    {
        Console.WriteLine($"Command: {string.Join(" ", _optionsProcessor.CommandPath)}");
        Console.WriteLine($"Lines: {_optionsProcessor.GetValueOrDefault("lines", 10u)}");
        Console.WriteLine($"Colour: {_optionsProcessor.GetValueOrDefault("colour", "white")}");
        return 0;
    }
}

