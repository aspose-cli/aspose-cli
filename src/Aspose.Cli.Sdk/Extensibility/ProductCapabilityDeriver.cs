using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility;

internal static class ProductCapabilityDeriver
{
    public static IReadOnlyDictionary<ProductDefinition, ProductCapabilities>
        Derive(
            IReadOnlyList<ProductDefinition> products,
            IReadOnlyDictionary<string, string> resolvedOwners)
    {
        return products.ToDictionary(
            static product => product,
            product => DeriveProduct(
                product,
                resolvedOwners));
    }

    private static ProductCapabilities DeriveProduct(
        ProductDefinition product,
        IReadOnlyDictionary<string, string> resolvedOwners)
    {
        ProductManifest manifest = product.Manifest;
        ProductCapabilities capabilities = new()
        {
            Id = manifest.Id,
            Operations = Array.AsReadOnly(manifest.Operations.Select(static operation => operation.Descriptor).ToArray()),
            Engine = manifest.Engine,
            AvailableEngines = manifest.AvailableEngines,
            ResourceBudgets = manifest.ResourceBudgets,
        };
        FormatDescriptor[] activeFormats = product.Formats.ToArray();
        if (activeFormats.Length > 0)
        {
            capabilities = capabilities with
            {
                LoadFormats = SelectFormatIds(activeFormats, FormatUse.Input),
                ConvertFormats = SelectFormatIds(activeFormats, FormatUse.Convert),
                RenderFormats = SelectFormatIds(activeFormats, FormatUse.Render),
                Formats = Array.AsReadOnly(activeFormats
                    .OrderBy(static format => format.Id, StringComparer.Ordinal)
                    .Select(format => DescribeFormat(
                        product,
                        format,
                        resolvedOwners))
                    .ToArray()),
            };
        }

        capabilities = capabilities with
        {
            Preview = new ProductPreviewCapabilities
            {
                DefaultView = product.View.LiveView,
                Views = Array.AsReadOnly(
                    product.View.Views.Select(static view => view.Id).ToArray()),
                Background = capabilities.Preview?.Background ?? true,
            },
            Review = new ProductReviewCapabilities
            {
                DefaultView = product.View.ReviewView,
                Views = product.View.ReviewViews,
                VisualInspectionRequired = product.View.VisualInspectionRequired,
                Checks = product.View.Checks,
            },
        };

        return capabilities;
    }

    private static ProductFormatCapabilities DescribeFormat(
        ProductDefinition product,
        FormatDescriptor format,
        IReadOnlyDictionary<string, string> resolvedOwners)
    {
        string[] finalOwners = format.RoutedExtensions
            .Select(ProductCatalog.NormalizeExtension)
            .Where(resolvedOwners.ContainsKey)
            .Select(extension => resolvedOwners[extension])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        bool generic = finalOwners.Contains(
            product.Manifest.Id,
            StringComparer.Ordinal);
        return new ProductFormatCapabilities
        {
            Id = format.Id,
            Extensions = Array.AsReadOnly(format.Extensions
                .Select(ProductCatalog.NormalizeExtension)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()),
            Aliases = Array.AsReadOnly(format.Aliases
                .Order(StringComparer.Ordinal)
                .ToArray()),
            Uses = Array.AsReadOnly(Enum.GetValues<FormatUse>()
                .Where(use => use is FormatUse.Input
                    or FormatUse.Convert
                    or FormatUse.Render)
                .Where(use => format.Uses.HasFlag(use))
                .Select(static use => use.ToString().ToLowerInvariant())
                .ToArray()),
            Operations = format.Uses.HasFlag(FormatUse.Input)
                ? Array.AsReadOnly((format.Operations.Count == 0
                    ? StandardFileRouteOperations.All
                    : format.Operations)
                    .Order(StringComparer.Ordinal)
                    .ToArray())
                : [],
            DeclaredOwnership =
                format.Ownership.ToString().ToLowerInvariant(),
            FinalOwner = finalOwners.Length == 1
                ? finalOwners[0]
                : null,
            GenericAvailable = generic,
            ExplicitAvailable = format.Uses.HasFlag(FormatUse.Input),
            RecognizerStrategy = generic
                ? product.Files.Recognizer?.Descriptor.Strategy
                : null,
            MaxProbeBytes = generic
                ? product.Files.Recognizer?.Descriptor.MaxProbeBytes
                : null,
        };
    }

    private static IReadOnlyList<string> SelectFormatIds(
        IEnumerable<FormatDescriptor> formats,
        FormatUse use) =>
        formats.IdsFor(use);

}
