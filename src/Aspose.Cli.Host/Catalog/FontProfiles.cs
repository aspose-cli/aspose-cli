using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Host.Catalog;

/// <summary>
/// Applies a font profile to one product for the length of a visual
/// operation: review, preview rendering and <c>fonts check</c> all enter the
/// product's font scope here, so every render, layout and font check of that
/// operation sees the same fonts.
/// </summary>
internal static class FontProfiles
{
    /// <exception cref="CliException">
    /// <c>FEATURE_UNSUPPORTED</c> when the profile adds directories and the
    /// product does not advertise <c>supportsExplicitFontProfiles</c>.
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
        if (!profile.IsAmbient && !product.Manifest.Engine.SupportsExplicitFontProfiles)
        {
            throw CliErrors.FeatureUnsupported(
                "font directories",
                product.Manifest.Id,
                catalog.Products
                    .Where(static item => item.Manifest.Engine.SupportsExplicitFontProfiles)
                    .Select(static item => item.Manifest.Id)
                    .ToArray());
        }
        return binding.FontEnvironment?.UseFonts(profile) ?? NoScope.Instance;
    }

    private sealed class NoScope : IDisposable
    {
        public static readonly NoScope Instance = new();

        public void Dispose()
        {
        }
    }
}
