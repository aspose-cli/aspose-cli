using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Licensing;

/// <summary>Inspects each registered product without hiding failures in sibling products.</summary>
internal static class ProductLicenseInspector
{
    public static IReadOnlyList<ProductLicenseInspection> Inspect(
        CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Catalog.Products
            .Select(product => Inspect(product, context))
            .ToArray();
    }

    private static ProductLicenseInspection Inspect(
        ProductDefinition product,
        CommandContext context)
    {
        ILicenseGate gate = context.Activate(product).LicenseGate;
        try
        {
            return new ProductLicenseInspection(
                product,
                gate.IsApplicable,
                gate.Resolution,
                gate.EnsureApplied(),
                Error: null);
        }
        catch (CliException exception)
        {
            return new ProductLicenseInspection(
                product,
                gate.IsApplicable,
                gate.Resolution,
                LicenseState.Evaluation,
                exception);
        }
    }
}

internal sealed record ProductLicenseInspection(
    ProductDefinition Product,
    bool Applicable,
    LicenseResolution Resolution,
    LicenseState State,
    CliException? Error);
