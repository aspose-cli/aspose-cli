using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The lines of one command group in a product menu: each pairs a command definition with the
/// static handler that serves it. The order is the order of help and capabilities.
/// </summary>
/// <typeparam name="TSession">The product's engine session.</typeparam>
public sealed class CommandMenu<TSession>
    where TSession : class
{
    private readonly List<IMenuEntry<TSession>> _entries = [];

    internal CommandMenu()
    {
    }

    internal IReadOnlyList<IMenuEntry<TSession>> Entries => _entries;

    /// <summary>Adds one command and the handler that serves it.</summary>
    /// <param name="create">Creates the command definition; called each time a command tree is built.</param>
    /// <param name="run">Turns the request into the result.</param>
    public CommandMenu<TSession> Command<TRequest, TResult>(
        Func<CommandDefinition<TRequest, TResult>> create,
        Func<TSession, TRequest, TResult> run)
        where TResult : ResultEnvelope
    {
        _entries.Add(new MenuCommand<TSession, TRequest, TResult>(create, run));
        return this;
    }

    /// <summary>Adds a named group of commands.</summary>
    /// <param name="name">The group command name.</param>
    /// <param name="description">The group help.</param>
    /// <param name="commands">Adds the group's lines.</param>
    public CommandMenu<TSession> Group(string name, string description, Action<CommandMenu<TSession>> commands)
    {
        _entries.Add(CreateGroup(name, description, commands));
        return this;
    }

    internal static IMenuEntry<TSession> CreateGroup(
        string name,
        string description,
        Action<CommandMenu<TSession>> commands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(commands);
        var group = new CommandMenu<TSession>();
        commands(group);
        return group._entries.Count > 0
            ? new MenuGroup<TSession>(name, description, group._entries.ToArray())
            : throw new ArgumentException($"Command group '{name}' has no commands.", nameof(commands));
    }
}

/// <summary>The menu lines of a product definition: its commands and command groups, in help order.</summary>
public static class ProductMenu
{
    /// <summary>Adds one command and the static handler that serves it.</summary>
    /// <param name="product">The product definition.</param>
    /// <param name="create">Creates the command definition; called each time a command tree is built.</param>
    /// <param name="run">Turns the request into the result, inside the font scope and the product guard.</param>
    public static ProductDefinitionBuilder<TSession> Command<TSession, TRequest, TResult>(
        this ProductDefinitionBuilder<TSession> product,
        Func<CommandDefinition<TRequest, TResult>> create,
        Func<TSession, TRequest, TResult> run)
        where TSession : class
        where TResult : ResultEnvelope
    {
        ArgumentNullException.ThrowIfNull(product);
        return product.AddMenuEntry(new MenuCommand<TSession, TRequest, TResult>(create, run));
    }

    /// <summary>Adds a named group of commands, such as <c>query</c>.</summary>
    /// <param name="product">The product definition.</param>
    /// <param name="name">The group command name.</param>
    /// <param name="description">The group help.</param>
    /// <param name="commands">Adds the group's lines.</param>
    public static ProductDefinitionBuilder<TSession> Group<TSession>(
        this ProductDefinitionBuilder<TSession> product,
        string name,
        string description,
        Action<CommandMenu<TSession>> commands)
        where TSession : class
    {
        ArgumentNullException.ThrowIfNull(product);
        return product.AddMenuEntry(CommandMenu<TSession>.CreateGroup(name, description, commands));
    }
}

/// <summary>One command line of a menu.</summary>
internal sealed class MenuCommand<TSession, TRequest, TResult> : IMenuEntry<TSession>
    where TSession : class
    where TResult : ResultEnvelope
{
    private readonly Func<CommandDefinition<TRequest, TResult>> _create;
    private readonly Func<TSession, TRequest, TResult> _run;

    public MenuCommand(Func<CommandDefinition<TRequest, TResult>> create, Func<TSession, TRequest, TResult> run)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        _run = run ?? throw new ArgumentNullException(nameof(run));
    }

    public Command Create(MenuContext<TSession> context) => Definition().Create(context, _run);

    public IEnumerable<ProductOutputDefinition> Outputs() => Definition().Outputs();

    public IEnumerable<ProductOperationCommand> Operations(string parent) =>
        Definition() is { Operations: { } describe } definition ? [describe(parent + definition.Name)] : [];

    private CommandDefinition<TRequest, TResult> Definition() =>
        _create() ?? throw new InvalidOperationException("A command definition factory returned null.");
}
