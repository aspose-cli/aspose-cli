using System.Collections.Frozen;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>Validated user-configuration changes used to report a pending installation or removal.</summary>
public sealed class UserLicenseChanges(IEnumerable<KeyValuePair<string, string?>> products, bool removeShared = false)
{
    internal FrozenDictionary<string, string?> Products { get; } = products.ToFrozenDictionary(StringComparer.Ordinal);
    public bool RemoveShared { get; } = removeShared;

    public bool IsProductInstalled(string configDirectory, string productId) =>
        Products.TryGetValue(productId, out string? snapshot) ? snapshot is not null
            : File.Exists(LicenseResolver.UserLicensePath(configDirectory, productId));

    public bool IsSharedInstalled(string configDirectory) =>
        !RemoveShared && File.Exists(LicenseResolver.SharedUserLicensePath(configDirectory));
}
