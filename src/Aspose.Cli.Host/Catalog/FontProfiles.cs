using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.Catalog;

/// <summary>
/// Applies a font profile to the product a product-neutral command routed to:
/// review, preview rendering and <c>fonts check</c> refuse explicit
/// directories for a product that does not advertise them, then enter the
/// product's font scope, so every render, layout and font check of that
/// operation sees the same fonts.
/// </summary>
internal static class FontProfiles
{
    /// <exception cref="CliException">
    /// <c>FEATURE_UNSUPPORTED</c> when the profile adds directories and the
    /// product does not advertise <c>supportsFontDiagnostics</c>.
    /// </exception>
    public static IDisposable Use(
        ProductCatalog catalog,
        ProductDefinition product,
        ProductBinding binding,
        FontSearchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.IsAmbient && !product.Manifest.Engine.SupportsFontDiagnostics)
        {
            throw CliErrors.FeatureUnsupported(
                "font directories",
                product.Manifest.Id,
                catalog.Products
                    .Where(static item => item.Manifest.Engine.SupportsFontDiagnostics)
                    .Select(static item => item.Manifest.Id)
                    .ToArray());
        }
        return binding.UseFonts(profile);
    }
}
