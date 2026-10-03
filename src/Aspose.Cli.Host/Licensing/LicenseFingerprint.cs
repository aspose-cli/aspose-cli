using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Licensing;

/// <summary>
/// Identifies the license a product would apply without applying it: the
/// source and, for a file, its size and write time. Reading the file itself
/// would cost more than it proves, because a license is installed by
/// replacing the file. Environment-carried licenses cannot change under a
/// live process, so their source alone identifies them.
/// </summary>
internal static class LicenseFingerprint
{
    public static string Of(LicenseResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        // Requested evaluation applies nothing, as no source does, so a renderer serves both
        // without recycling; the viewer keeps their documents apart by their open options.
        if (!resolution.IsConfigured)
        {
            return "none";
        }
        if (resolution.Path is not { Length: > 0 } path)
        {
            return resolution.SourceLabel!;
        }
        var file = new FileInfo(path);
        return file.Exists
            ? string.Join('|', resolution.SourceLabel, path, file.Length, file.LastWriteTimeUtc.Ticks)
            : string.Join('|', resolution.SourceLabel, path, "missing");
    }

    /// <summary>The fingerprints of every license-aware product, as one value.</summary>
    public static string Of(
        ProductCatalog catalog,
        string? flagPath,
        string workingDirectory,
        string configDirectory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return string.Join(
            '\n',
            catalog.Products
                .Where(static product => product.Manifest.Engine.LicenseApplicable)
                .Select(product =>
                {
                    try
                    {
                        return product.Manifest.Id + '=' + Of(LicenseResolver.Resolve(
                            flagPath,
                            product.Manifest.Id,
                            Environment.GetEnvironmentVariable,
                            workingDirectory,
                            configDirectory));
                    }
                    catch (CliException exception)
                    {
                        return product.Manifest.Id + '!' + exception.Code.Name + ':' + exception.Message;
                    }
                }));
    }
}
