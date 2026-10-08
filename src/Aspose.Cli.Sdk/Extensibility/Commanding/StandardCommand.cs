using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

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
        ArgumentNullException.ThrowIfNull(handler);
        var standard = new StandardOptions(traits);
        Command command = standard.CreateCommand(name, description, parameters);
        command.SetAction(parse => host.Run(parse, context =>
        {
            using var invocation = new StandardInvocation<TPort>(
                standard, parse, context, standardInputTaken?.Invoke(parse) != true);
            try
            {
                return handler(parse, invocation);
            }
            catch (CliException error) when (invocation.ForPairedInput(error) is var restated && restated != error)
            {
                throw restated;
            }
        }));
        return command;
    }
}
