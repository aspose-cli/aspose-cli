using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Builds a product command from its traits: the document arguments, the product's own
/// arguments and then its options in declaration order, then the common options that
/// <see cref="CommandTraits"/> select. The handler maps the parsed values to the product request.
/// </summary>
public static class StandardCommand
{
    /// <summary>Creates one product command that runs through the host pipeline.</summary>
    /// <param name="host">The product's command host.</param>
    /// <param name="name">The command name.</param>
    /// <param name="description">The command help.</param>
    /// <param name="traits">What the command reads and writes.</param>
    /// <param name="parameters">The product's own arguments and options, each kind in help order.</param>
    /// <param name="handler">Maps the parsed values to the product port call.</param>
    public static Command Create<TPort>(
        IProductCommandHost<TPort> host,
        string name,
        string description,
        CommandTraits traits,
        IReadOnlyList<Symbol> parameters,
        Func<ParseResult, StandardInvocation<TPort>, ResultEnvelope> handler)
        where TPort : class =>
        Create(host, name, description, traits, parameters, standardInputTaken: null, handler);

    /// <summary>
    /// Creates a command one of whose own options can make standard input carry data; the
    /// input password then refuses to read it.
    /// </summary>
    internal static Command Create<TPort>(
        IProductCommandHost<TPort> host,
        string name,
        string description,
        CommandTraits traits,
        IReadOnlyList<Symbol> parameters,
        Func<ParseResult, bool>? standardInputTaken,
        Func<ParseResult, StandardInvocation<TPort>, ResultEnvelope> handler)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(handler);
        var standard = new StandardOptions(traits);
        var command = new Command(name, description);
        standard.AddArguments(command);
        foreach (Symbol parameter in parameters)
        {
            switch (parameter)
            {
                case Argument argument:
                    command.Arguments.Add(argument);
                    break;
                case Option option:
                    command.Options.Add(option);
                    break;
                default:
                    throw new ArgumentException($"'{parameter.Name}' is neither an argument nor an option.", nameof(parameters));
            }
        }

        standard.AddOptions(command);
        command.SetAction(parse => host.Run(parse, context =>
        {
            using var invocation = new StandardInvocation<TPort>(
                standard, parse, context, standardInputTaken?.Invoke(parse) != true);
            return handler(parse, invocation);
        }));
        return command;
    }
}
