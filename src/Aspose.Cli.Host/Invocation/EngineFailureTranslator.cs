using System.Reflection;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// The one boundary for failures that escape a product without a <see cref="CliException"/>.
/// The stack says who failed (<see cref="ExceptionOrigin.FromEngine"/>): a failure the engine
/// raised is reported with the shared <see cref="EngineErrors.EngineFailed"/> wording, named after
/// the product it passed through. A failure first raised by CLI code stays an internal error, so
/// our own defects are never reported as document problems.
/// </summary>
internal sealed class EngineFailureTranslator
{
    private readonly IReadOnlyDictionary<Assembly, string> _products;

    internal EngineFailureTranslator(IReadOnlyDictionary<Assembly, string> productNames) => _products = productNames;

    internal static EngineFailureTranslator Create(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var products = new Dictionary<Assembly, string>();
        foreach (ProductPackageResources package in catalog.Resources.Products)
        {
            products[package.ResourceAssembly] = catalog.Get(package.ProductId).Manifest.DisplayName;
        }
        return new EngineFailureTranslator(products);
    }

    /// <summary>Returns the public error for an engine failure, or null for an internal one.</summary>
    internal CliException? Translate(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is CliException or OperationCanceledException)
        {
            return null;
        }
        if (exception is RegexMatchTimeoutException regex)
        {
            return CliErrors.RegexTimeout(regex);
        }

        string? product = ExceptionOrigin.Frames(exception)
            .Select(assembly => _products.GetValueOrDefault(assembly))
            .FirstOrDefault(static name => name is not null);
        if (product is null)
        {
            return null;
        }

        if (ExceptionOrigin.FromEngine(exception))
        {
            return EngineErrors.EngineFailed(product, exception);
        }

        if (exception is UnauthorizedAccessException
            || exception is IOException and not (FileNotFoundException or FileLoadException))
        {
            return CliErrors.FileOperationFailed(product, exception);
        }

        return null;
    }
}
