namespace Cumber;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class CommandAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
