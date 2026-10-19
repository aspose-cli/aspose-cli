using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Host.Licensing;

/// <summary>
/// Identifies the license a product would apply without applying it: the
/// source and, for a file, its size and write time. Reading the file itself
/// would cost more than it proves, because a license is installed by
/// replacing the file. License content carried in an environment variable is
/// identified by its hash: one resident process serves requesters whose
/// environments differ, and the content itself never enters a fingerprint.
/// </summary>
internal static class LicenseFingerprint
{
    /// <param name="resolution">The license a product would apply.</param>
    /// <param name="environment">The environment the resolution read its variables from.</param>
    public static string Of(LicenseResolution resolution, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(environment);
        // Requested evaluation applies nothing, as no source does, so a renderer serves both
        // without recycling; the viewer keeps their documents apart by their open options.
        if (!resolution.IsConfigured)
        {
            return "none";
        }
        if (resolution.Base64EnvironmentVariable is { } variable)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(environment(variable) ?? string.Empty));
            return string.Join('|', resolution.SourceLabel, Convert.ToHexString(hash));
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

    /// <summary>
    /// The fingerprints of every product, as one value, resolved from
    /// <paramref name="environment"/> or, without one, from this process's.
    /// </summary>
    public static string Of(
        ProductCatalog catalog,
        string? flagPath,
        string workingDirectory,
        string configDirectory,
        Func<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Func<string, string?> read = environment ?? Environment.GetEnvironmentVariable;
        return string.Join(
            '\n',
            catalog.Products
                .Select(product =>
                {
                    try
                    {
                        return product.Manifest.Id + '=' + Of(LicenseResolver.Resolve(
                            flagPath,
                            product.Manifest.Id,
                            read,
                            workingDirectory,
                            configDirectory), read);
                    }
                    catch (CliException exception)
                    {
                        return product.Manifest.Id + '!' + exception.Code.Name + ':' + exception.Message;
                    }
                }));
    }
}
