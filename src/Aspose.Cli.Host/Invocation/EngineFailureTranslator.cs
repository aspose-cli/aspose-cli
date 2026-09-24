using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// The one boundary for failures that escape a product without a <see cref="CliException"/>.
/// The stack says who failed: when the first frame owned by neither .NET nor this CLI
/// belongs to a third-party library (a document engine, its imaging stack), the engine failed,
/// reported with the shared <see cref="CliErrors.EngineFailed"/> wording. A failure first raised
/// by CLI code stays an internal error, so our own defects are never reported as document problems.
/// </summary>
internal sealed class EngineFailureTranslator
{
    private readonly IReadOnlySet<Assembly> _own;
    private readonly IReadOnlyDictionary<Assembly, string> _products;

    internal EngineFailureTranslator(IReadOnlySet<Assembly> ownAssemblies, IReadOnlyDictionary<Assembly, string> productNames)
    {
        _own = ownAssemblies;
        _products = productNames;
    }

    internal static EngineFailureTranslator Create(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var products = new Dictionary<Assembly, string>();
        foreach (ProductDefinition product in catalog.Products)
        {
            products[product.PortType.Assembly] = product.Manifest.DisplayName;
        }
        var own = new HashSet<Assembly>(products.Keys)
        {
            typeof(CliException).Assembly,
            typeof(EngineFailureTranslator).Assembly,
        };
        if (Assembly.GetEntryAssembly() is { } launcher)
        {
            own.Add(launcher);
        }
        return new EngineFailureTranslator(own, products);
    }

    /// <summary>Returns the public error for an engine failure, or null for an internal one.</summary>
    internal CliException? Translate(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is CliException or OperationCanceledException)
        {
            return null;
        }
        if (exception is RegexMatchTimeoutException)
        {
            return new CliException(
                ErrorCodes.OperationTimeout,
                "A regular expression exceeded its execution budget.",
                hint: "Simplify the expression or search a narrower part of the document.",
                innerException: exception);
        }

        Assembly[] frames = new StackTrace(exception, fNeedFileInfo: false).GetFrames()
            .Select(static frame => frame.GetMethod()?.DeclaringType?.Assembly)
            .OfType<Assembly>()
            .ToArray();
        string? product = frames.Select(assembly => _products.GetValueOrDefault(assembly)).FirstOrDefault(static name => name is not null);
        if (product is null)
        {
            return null;
        }

        Assembly? owner = frames.FirstOrDefault(assembly => !IsRuntime(assembly));
        if (exception is EngineOpException || owner is not null && !_own.Contains(owner))
        {
            return CliErrors.EngineFailed($"{product} failed inside its document engine: {exception.Message}", exception);
        }

        if (exception is UnauthorizedAccessException
            || exception is IOException and not (FileNotFoundException or FileLoadException))
        {
            return new CliException(
                ErrorCodes.OutputUnwritable,
                $"{product} could not complete a file operation: {exception.Message}",
                hint: "Check file permissions, locks and free disk space, then retry.",
                innerException: exception);
        }

        return null;
    }

    private static bool IsRuntime(Assembly assembly)
    {
        string name = assembly.GetName().Name ?? string.Empty;
        return name is "mscorlib" or "netstandard"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal);
    }
}
