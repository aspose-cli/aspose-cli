namespace Aspose.Cli.Sdk.Licensing;

/// <summary>Where a license was found in the resolution chain.</summary>
public enum LicenseSourceKind
{
    /// <summary>No license configured anywhere; the CLI runs in evaluation mode.</summary>
    None,

    /// <summary>The <c>--license</c> command-line option.</summary>
    Flag,

    /// <summary>The <c>ASPOSE_LICENSE_B64</c> environment variable (base64 content).</summary>
    EnvBase64,

    /// <summary>The <c>ASPOSE_LICENSE_PATH</c> environment variable.</summary>
    EnvPath,

    /// <summary>A <c>.aspose/license.lic</c> file in the working directory.</summary>
    ProjectFile,

    /// <summary>A <c>license.lic</c> file in the user configuration directory.</summary>
    UserFile,

    /// <summary>A product-specific base64 environment variable.</summary>
    ProductEnvBase64,

    /// <summary>A product-specific license-path environment variable.</summary>
    ProductEnvPath,

    /// <summary>A product-specific file under <c>.aspose/licenses</c>.</summary>
    ProductProjectFile,

    /// <summary>A product-specific file under the user's <c>licenses</c> directory.</summary>
    ProductUserFile,
}

/// <summary>Outcome of the license resolution chain.</summary>
/// <param name="Kind">Which source produced the license.</param>
/// <param name="Path">License file path; null for no source and for either shared or product-specific base64 sources.</param>
public sealed record LicenseResolution(LicenseSourceKind Kind, string? Path)
{
    /// <summary>Normalized product identifier for a product-specific source.</summary>
    public string? ProductId { get; init; }

    /// <summary>A license source was found (it may still fail validation).</summary>
    public bool IsConfigured => Kind != LicenseSourceKind.None;

    /// <summary>The environment variable containing base64 license content, when applicable.</summary>
    public string? Base64EnvironmentVariable => Kind switch
    {
        LicenseSourceKind.EnvBase64 => LicenseResolver.EnvBase64Name,
        LicenseSourceKind.ProductEnvBase64 => LicenseResolver.ProductEnvBase64Name(ProductId!),
        _ => null,
    };

    /// <summary>Stable label used in <c>license status</c> output; null when not configured.</summary>
    public string? SourceLabel => Kind switch
    {
        LicenseSourceKind.None => null,
        LicenseSourceKind.Flag => "flag",
        LicenseSourceKind.EnvBase64 => "env:" + LicenseResolver.EnvBase64Name,
        LicenseSourceKind.EnvPath => "env:" + LicenseResolver.EnvPathName,
        LicenseSourceKind.ProjectFile => "project",
        LicenseSourceKind.UserFile => "user",
        LicenseSourceKind.ProductEnvBase64 => "env:" + LicenseResolver.ProductEnvBase64Name(ProductId!),
        LicenseSourceKind.ProductEnvPath => "env:" + LicenseResolver.ProductEnvPathName(ProductId!),
        LicenseSourceKind.ProductProjectFile => "project:" + ProductId,
        LicenseSourceKind.ProductUserFile => "user:" + ProductId,
        _ => throw new InvalidOperationException($"Unhandled license source kind: {Kind}"),
    };

    /// <summary>The no-license resolution.</summary>
    public static LicenseResolution None { get; } = new(LicenseSourceKind.None, null);
}
