using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Cumber;

public sealed class CommandRouter
{
    private readonly Dictionary<string, Type> _commandMap;
    private readonly IServiceProvider _serviceProvider;

    private CommandRouter(Dictionary<string, Type> commandMap, IServiceProvider serviceProvider)
    {
        _commandMap = commandMap;
        _serviceProvider = serviceProvider;
    }

    public static CommandRouter FromAssembly(Assembly assembly, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var commandMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (Type type in assembly.GetTypes())
        {
            var commandAttributes = type.GetCustomAttributes<CommandAttribute>().ToArray();
            if (commandAttributes.Length == 0) continue;

            if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters ||
                !typeof(ICumberCommand).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"{type.FullName} must be a concrete, non-generic class implementing {nameof(ICumberCommand)}.");
            }

            foreach (var attribute in commandAttributes)
            {
                // Register attribute's command name against this type:

                if (string.IsNullOrWhiteSpace(attribute.Name))
                {
                    throw new InvalidOperationException($"{type.FullName} has an empty command name.");
                }

                if (!commandMap.TryAdd(attribute.Name, type))
                {
                    throw new InvalidOperationException(
                        $"Command '{attribute.Name}' is declared by both {commandMap[attribute.Name].DeclaringType!.FullName} and {type.FullName}.");
                }
            }
        }

        return new CommandRouter(commandMap, serviceProvider);
    }

    public void Execute()
    {
        var commandName = (_serviceProvider.GetService<IOptionsProcessor>()?.CommandPath?[0]) ??
                throw new InvalidOperationException("null command: a command was not properly specified.");
        if (!_commandMap.TryGetValue(commandName!, out var type))
            throw new ArgumentException($"No handler is registered for command '{commandName}'.", nameof(commandName));

        ICumberCommand command;

        try
        {
            command = (ICumberCommand)ActivatorUtilities.CreateInstance(_serviceProvider, type);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Preserve the actual constructor exception and its stack trace.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        command.Execute();
    }
}