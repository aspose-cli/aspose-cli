using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>The menu lines of a product definition: its commands, in help order.</summary>
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

    public IEnumerable<ProductOperationCommand> Operations() =>
        Definition() is { Operations: { } describe } definition ? [describe(definition.Name)] : [];

    private CommandDefinition<TRequest, TResult> Definition() =>
        _create() ?? throw new InvalidOperationException("A command definition factory returned null.");
}
