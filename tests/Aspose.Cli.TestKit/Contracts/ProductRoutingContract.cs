using System.Text;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>Reusable positive routing contract for one autonomous product slice.</summary>
public static class ProductRoutingContract
{
    public static async Task AssertPositiveRoutesAsync<TModule>(
        IReadOnlyDictionary<string, byte[]> samples)
        where TModule : IProductModule, new()
    {
        ArgumentNullException.ThrowIfNull(samples);
        ProductCatalog catalog = ProductCatalog.Build([new TModule()]);
        ProductDefinition product = Assert.Single(catalog.Products);
        FormatDescriptor[] routed = product.Formats
            .Where(static format =>
                format.Uses.HasFlag(FormatUse.Input)
                && format.Ownership == RouteOwnership.Default)
            .OrderBy(static format => format.Id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            routed.Select(static format => format.Id),
            samples.Keys.Order(StringComparer.Ordinal));

        var router = new ProductFileRouter(catalog);
        foreach (FormatDescriptor format in routed)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"aspose-cli-{product.Manifest.Id}-route-{Guid.NewGuid():N}{format.Extensions[0]}");
            File.WriteAllBytes(path, samples[format.Id]);
            try
            {
                FileRouteResult result = await router.ResolveAsync(
                    new FileRouteRequest(path));
                Assert.Equal(product.Manifest.Id, result.Product.Manifest.Id);
                Assert.Equal(format.Id, result.FormatId);
                Assert.False(result.Explicit);
                Assert.False(string.IsNullOrWhiteSpace(result.Evidence));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    public static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    public static byte[] CompoundFile() =>
    [
        0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1,
    ];

    public static byte[] ZipMarker(string marker) =>
        [0x50, 0x4B, 0x03, 0x04, .. Utf8(marker)];

    public static byte[] At(int offset, byte[] marker)
    {
        byte[] value = new byte[offset + marker.Length];
        marker.CopyTo(value, offset);
        return value;
    }
}
