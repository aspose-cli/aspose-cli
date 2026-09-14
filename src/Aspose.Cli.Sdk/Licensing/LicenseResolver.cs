using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>
/// Finds the license to use. Product-aware resolution prefers an explicit
/// <c>--license</c>, product-specific sources, common sources, project files,
/// then user files.
/// </summary>
/// <remarks>
/// An explicitly configured source that points to a missing file is an error,
/// not a silent fall-through: quietly degrading to evaluation mode would
/// surprise the user with watermarked output.
/// </remarks>
public static class LicenseResolver
{
    public const string EnvBase64Name = "ASPOSE_LICENSE_B64";
    public const string EnvPathName = "ASPOSE_LICENSE_PATH";

    private const string ProjectRelativePath = ".aspose/license.lic";
    private const string UserFileName = "license.lic";
    private const string ProductLicenseDirectoryName = "licenses";

    /// <summary>Runs the resolution chain for one product.</summary>
    /// <param name="flagPath">Value of the <c>--license</c> option, if given.</param>
    /// <param name="productId">Stable product identifier, such as <c>cells</c> or <c>words</c>.</param>
    /// <param name="getEnvironmentVariable">Environment lookup (injected for testability).</param>
    /// <param name="workingDirectory">Base directory for project-level files.</param>
    /// <param name="userConfigDirectory">Directory of user-level files.</param>
    /// <exception cref="CliException"><c>LICENSE_FILE_NOT_FOUND</c> when an explicit source is broken.</exception>
    public static LicenseResolution Resolve(
        string? flagPath,
        string productId,
        Func<string, string?> getEnvironmentVariable,
        string workingDirectory,
        string userConfigDirectory)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        ArgumentException.ThrowIfNullOrEmpty(userConfigDirectory);
        string product = NormalizeProductId(productId);

        if (!string.IsNullOrWhiteSpace(flagPath))
        {
            string full = Path.GetFullPath(flagPath, workingDirectory);
            return WorkerOutputSession.FileExists(full)
                ? new LicenseResolution(LicenseSourceKind.Flag, full)
                : throw CliErrors.LicenseFileNotFound(full, "--license");
        }

        string base64Name = ProductEnvBase64Name(product);
        if (!string.IsNullOrWhiteSpace(getEnvironmentVariable(base64Name)))
        {
            return ProductResolution(LicenseSourceKind.ProductEnvBase64, product);
        }

        string pathName = ProductEnvPathName(product);
        LicenseResolution? productEnvironment = ResolveEnvPath(
            getEnvironmentVariable,
            pathName,
            LicenseSourceKind.ProductEnvPath,
            workingDirectory,
            product);
        if (productEnvironment is not null)
        {
            return productEnvironment;
        }

        if (!string.IsNullOrWhiteSpace(getEnvironmentVariable(EnvBase64Name)))
        {
            return new LicenseResolution(LicenseSourceKind.EnvBase64, null);
        }

        LicenseResolution? fromEnvPath = ResolveEnvPath(
            getEnvironmentVariable,
            EnvPathName,
            LicenseSourceKind.EnvPath,
            workingDirectory);
        if (fromEnvPath is not null)
        {
            return fromEnvPath;
        }

        string productProjectPath = ProductProjectLicensePath(workingDirectory, product);
        if (WorkerOutputSession.FileExists(productProjectPath))
        {
            return ProductResolution(
                LicenseSourceKind.ProductProjectFile,
                product,
                productProjectPath);
        }

        string projectPath = Path.GetFullPath(ProjectRelativePath, workingDirectory);
        if (WorkerOutputSession.FileExists(projectPath))
        {
            return new LicenseResolution(LicenseSourceKind.ProjectFile, projectPath);
        }

        string productUserPath = UserLicensePath(userConfigDirectory, product);
        if (WorkerOutputSession.FileExists(productUserPath))
        {
            return ProductResolution(
                LicenseSourceKind.ProductUserFile,
                product,
                productUserPath);
        }

        string userPath = SharedUserLicensePath(userConfigDirectory);
        if (WorkerOutputSession.FileExists(userPath))
        {
            return new LicenseResolution(LicenseSourceKind.UserFile, userPath);
        }

        return LicenseResolution.None;
    }

    /// <summary>Default user configuration directory of the CLI.</summary>
    public static string DefaultUserConfigDirectory() =>
        Configuration.ConfigurationPaths.UserDirectory();

    /// <summary>Full path of the shared user-level fallback; installation writes compatible product-specific files.</summary>
    public static string UserLicensePath() => SharedUserLicensePath(DefaultUserConfigDirectory());

    /// <summary>Full path of the shared user license in the selected configuration directory.</summary>
    public static string SharedUserLicensePath(string userConfigDirectory) =>
        Path.Combine(userConfigDirectory, UserFileName);

    /// <summary>Full path of one product's installed user license.</summary>
    public static string UserLicensePath(string productId) =>
        UserLicensePath(DefaultUserConfigDirectory(), productId);

    /// <summary>Full path of one product's installed user license in a selected configuration directory.</summary>
    public static string UserLicensePath(string userConfigDirectory, string productId) =>
        Path.Combine(
            Path.GetFullPath(userConfigDirectory),
            ProductLicenseDirectoryName,
            NormalizeProductId(productId) + ".lic");

    /// <summary>Environment variable containing base64 license content for one product.</summary>
    public static string ProductEnvBase64Name(string productId) =>
        $"ASPOSE_{NormalizeProductId(productId).ToUpperInvariant()}_LICENSE_B64";

    /// <summary>Environment variable containing a license path for one product.</summary>
    public static string ProductEnvPathName(string productId) =>
        $"ASPOSE_{NormalizeProductId(productId).ToUpperInvariant()}_LICENSE_PATH";

    private static LicenseResolution? ResolveEnvPath(
        Func<string, string?> getEnvironmentVariable,
        string variableName,
        LicenseSourceKind kind,
        string workingDirectory,
        string? productId = null)
    {
        string? value = getEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string full = Path.GetFullPath(value, workingDirectory);
        return WorkerOutputSession.FileExists(full)
            ? productId is null
                ? new LicenseResolution(kind, full)
                : ProductResolution(kind, productId, full)
            : throw CliErrors.LicenseFileNotFound(full, "env:" + variableName);
    }

    private static string ProductProjectLicensePath(string workingDirectory, string productId) =>
        Path.GetFullPath(
            Path.Combine(".aspose", ProductLicenseDirectoryName, productId + ".lic"),
            workingDirectory);

    private static LicenseResolution ProductResolution(
        LicenseSourceKind kind,
        string productId,
        string? path = null) =>
        new(kind, path) { ProductId = productId };

    private static string NormalizeProductId(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        string normalized = productId.Trim().ToLowerInvariant();
        if (normalized.Any(static character =>
            !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Product identifiers may contain only ASCII letters, digits, '-' and '_'.",
                nameof(productId));
        }

        return normalized;
    }
}
