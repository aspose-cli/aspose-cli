using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility;

internal static class ProductCapabilityDeriver
{
    public static IReadOnlyDictionary<ProductDefinition, ProductCapabilities>
        Derive(
            IReadOnlyList<ProductDefinition> products,
            IEnumerable<object> capabilityProviders,
            IReadOnlyDictionary<string, string> resolvedOwners)
    {
        var providers = new HashSet<object>(
            capabilityProviders,
            ReferenceEqualityComparer.Instance);
        return products.ToDictionary(
            static product => product,
            product => DeriveProduct(
                product,
                providers,
                resolvedOwners));
    }

    private static ProductCapabilities DeriveProduct(
        ProductDefinition product,
        IReadOnlySet<object> providers,
        IReadOnlyDictionary<string, string> resolvedOwners)
    {
        ProductManifest manifest = product.Manifest;
        ProductCapabilities capabilities = new()
        {
            Id = manifest.Id,
            Operations = manifest.Operations,
            Engine = manifest.Engine,
            AvailableEngines = manifest.AvailableEngines,
            ResourceBudgets = manifest.ResourceBudgets,
        };
        FormatDescriptor[] activeFormats = product.Formats
            .Where(format =>
                format.CapabilitySlot is null
                || providers.Contains(format.CapabilitySlot))
            .ToArray();
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
                DefaultView = product.Preview.DefaultView,
                Views = product.Preview.Views,
                Background = capabilities.Preview?.Background ?? true,
            },
            Review = new ProductReviewCapabilities
            {
                DefaultView = product.Review.DefaultView,
                Views = product.Review.Views,
                VisualInspectionRequired = product.Review.VisualInspectionRequired,
            },
        };

        return capabilities;
    }

    private static ProductFormatCapabilities DescribeFormat(
        ProductDefinition product,
        FormatDescriptor format,
        IReadOnlyDictionary<string, string> resolvedOwners)
    {
        string[] finalOwners = format.Extensions
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
        Array.AsReadOnly(formats
            .Where(format => format.Uses.HasFlag(use))
            .OrderBy(format => use switch
            {
                FormatUse.Input => format.InputOrder,
                FormatUse.Convert => format.ConvertOrder,
                FormatUse.Render => format.RenderOrder,
                _ => int.MaxValue,
            })
            .Select(static format => format.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray());

}
