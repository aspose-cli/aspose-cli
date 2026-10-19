using System.CommandLine;
using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>The examples and links a product command shows below its description.</summary>
/// <param name="Examples">Examples, written after the executable name.</param>
/// <param name="Links">Links to further documentation.</param>
public sealed record CommandHelp(IReadOnlyList<string> Examples, IReadOnlyList<CommandHelpLink> Links);

/// <summary>
/// Runs a product's own cross-cutting concern around every engine call of its commands and views,
/// such as muting what the engine writes to the console, and returns what <paramref name="run"/>
/// returns. An evaluation limit the engine raises is the SDK's to report, never a guard's.
/// A lambda cannot be generic, so the value passes as an object; the SDK casts it back.
/// </summary>
/// <param name="session">The product's engine session.</param>
/// <param name="run">The guarded engine call.</param>
public delegate object? ProductGuard<in TSession>(TSession session, Func<object?> run);

/// <summary>What every command of one product menu shares when its command tree is built.</summary>
/// <param name="Run">The host pipeline.</param>
/// <param name="Guard">The product guard, or null.</param>
/// <param name="DetectFormat">The product's content format detector, or null.</param>
internal sealed record MenuContext<TSession>(
    ProductCommandRunner Run,
    ProductGuard<TSession>? Guard,
    Func<string, string?>? DetectFormat)
    where TSession : class
{
    /// <summary>
    /// Calls <paramref name="handler"/> inside the product guard, and reports an evaluation limit
    /// the engine raised in it through the product's write pipeline, the one translation of an
    /// engine's evaluation exception (<see cref="OutputPipeline.ReportingEvaluationLimits"/>).
    /// </summary>
    internal TResult Guarded<TRequest, TResult>(
        Func<TSession, TRequest, TResult> handler,
        TSession session,
        TRequest request,
        OutputPipeline outputs,
        Func<string, string?> selection) =>
        outputs.ReportingEvaluationLimits(
            () => Guard is null
                ? handler(session, request)
                : (TResult)Guard(session, () => handler(session, request))!,
            selection);
}

/// <summary>The formats each product reads as input documents, declared once on the product's command.</summary>
internal static class ProductReads
{
    private static readonly ConditionalWeakTable<Command, IReadOnlyList<FormatDescriptor>> Reading = [];

    /// <summary>Declares the formats the product whose command is <paramref name="product"/> reads.</summary>
    internal static void Declare(Command product, IReadOnlyList<FormatDescriptor> formats) => Reading.AddOrUpdate(product, formats);

    /// <summary>The formats the product of <paramref name="command"/> reads, or null when it declared none.</summary>
    internal static IReadOnlyList<FormatDescriptor>? Of(Command command)
    {
        for (Command? symbol = command; symbol is not null; symbol = symbol.Parents.OfType<Command>().FirstOrDefault())
        {
            if (Reading.TryGetValue(symbol, out IReadOnlyList<FormatDescriptor>? reads))
            {
                return reads;
            }
        }

        return null;
    }
}

/// <summary>One line of a product menu: a command and the handler that serves it.</summary>
internal interface IMenuEntry<TSession>
    where TSession : class
{
    Command Create(MenuContext<TSession> context);

    /// <summary>The table renderers the entry's command declares.</summary>
    IEnumerable<ProductOutputDefinition> Outputs();

    /// <summary>The operation documents the entry's command applies, under its name.</summary>
    IEnumerable<ProductOperationCommand> Operations();
}
