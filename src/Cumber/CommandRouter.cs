using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Cumber;

public sealed class CommandRouter
{
    private readonly Dictionary<string, ConstructorInfo> _constructors;

    private CommandRouter(Dictionary<string, ConstructorInfo> constructors)
    {
        _constructors = constructors;
    }

    public static CommandRouter FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var constructors = new Dictionary<string, ConstructorInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (Type type in assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<CommandAttribute>();

            if (attribute is null) continue;

            if (string.IsNullOrWhiteSpace(attribute.Name))
            {
                throw new InvalidOperationException(
                    $"{type.FullName} has an empty command name.");
            }

            if (!type.IsClass ||
                type.IsAbstract ||
                type.ContainsGenericParameters ||
                !typeof(ICumberCommand).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"{type.FullName} must be a concrete, non-generic " +
                    $"class implementing {nameof(ICumberCommand)}.");
            }

            ConstructorInfo? constructor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: [typeof(IOptionsProcessor)],
                modifiers: null);

            if (constructor is null)
            {
                throw new InvalidOperationException($"{type.FullName} must have a public constructor taking exactly one {nameof(IOptionsProcessor)}.");
            }

            if (!constructors.TryAdd(attribute.Name, constructor))
            {
                throw new InvalidOperationException(
                    $"Command '{attribute.Name}' is declared by both {constructors[attribute.Name].DeclaringType!.FullName} and {type.FullName}.");
            }
        }

        return new CommandRouter(constructors);
    }

    public int Execute(string commandName, IOptionsProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        ArgumentNullException.ThrowIfNull(processor);

        if (!_constructors.TryGetValue(commandName, out var constructor))
            throw new ArgumentException($"No handler is registered for command '{commandName}'.", nameof(commandName));

        ICumberCommand command;

        try
        {
            command = (ICumberCommand)constructor.Invoke([processor]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Preserve the actual constructor exception and its stack trace.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        return command.Execute();
    }
}