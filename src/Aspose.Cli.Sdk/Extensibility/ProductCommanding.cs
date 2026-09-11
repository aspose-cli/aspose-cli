using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>Resolved host values exposed to a product command invocation.</summary>
public sealed record ProductCommandGlobals
{
    /// <summary>Invocation-scoped lookup for explicitly referenced environment secrets.</summary>
    public required Func<string, string?> ReadEnvironment { get; init; }

    /// <summary>Whether diagnostics and notices are suppressed.</summary>
    public required bool Quiet { get; init; }

    /// <summary>The invocation's shared monotonic deadline and cancellation token.</summary>
    public OperationDeadline Deadline { get; init; } = OperationDeadline.Start(null);

    /// <summary>Invocation-scoped budget ledger; child operations cannot reset it.</summary>
    public required ResourceBudgetLedger ResourceBudgets { get; init; }
}
/// <summary>
/// Strongly typed execution scope supplied to commands from exactly one
/// product. It contains no catalog and cannot resolve sibling runtimes.
/// </summary>
/// <typeparam name="TPort">The product's typed port.</typeparam>
public sealed record ProductCommandContext<TPort>
    where TPort : class
{
    /// <summary>The complete typed runtime binding for host-only lifecycles.</summary>
    public required ProductBinding<TPort> Binding { get; init; }

    /// <summary>The selected product port, created lazily on first access.</summary>
    public TPort Port => Binding.Port;

    /// <summary>Path resolver scoped to the invocation working directory.</summary>
    public required PathResolver Paths { get; init; }

    /// <summary>Safe host values shared by command implementations.</summary>
    public required ProductCommandGlobals Globals { get; init; }

    /// <summary>Single bounded reader for user-controlled file and stdin input.</summary>
    public InputSource Inputs => Globals.ResourceBudgets.Inputs;

    /// <summary>Resolves a named environment secret through this invocation's input source.</summary>
    public Func<string, string?> ReadEnvironment => Globals.ReadEnvironment;

    /// <summary>Returns one predeclared optional cross-product capability.</summary>
    public TCapability? Optional<TCapability>(
        ProductCapability<TCapability> slot)
        where TCapability : class =>
        Binding.Optional(slot);

}

/// <summary>
/// Host execution pipeline captured by a command tree from one product.
/// Implementations own output rendering, errors, timeouts and runtime binding.
/// </summary>
/// <typeparam name="TPort">The product's typed port.</typeparam>
public interface IProductCommandHost<TPort>
    where TPort : class
{
    /// <summary>Whether a provider for a predeclared typed slot is compiled.</summary>
    bool HasCapability<TCapability>(ProductCapability<TCapability> slot)
        where TCapability : class;

    /// <summary>Runs a command through the normal result-envelope pipeline.</summary>
    int Run(
        ParseResult parseResult,
        Func<ProductCommandContext<TPort>, ResultEnvelope> handler);
}

/// <summary>
/// Creates a strongly typed command host for a statically discovered module.
/// The generic call is emitted by the product definition itself.
/// </summary>
public interface IProductCommandHostFactory
{
    /// <summary>Creates the host for one known product and port.</summary>
    IProductCommandHost<TPort> Create<TPort>(string productId)
        where TPort : class;
}
