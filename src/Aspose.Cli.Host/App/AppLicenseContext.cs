using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Viewer;
using Aspose.Cli.Host.ViewerService;

namespace Aspose.Cli.Host.App;

/// <summary>
/// Where the App finds its license: the <c>--license</c> of the command whose <c>app open</c> reached it last, that command's license variables and its
/// working directory. The service outlives the shell that started it, so what Settings reports
/// and what the App renders follow this context, never the service's own environment.
/// </summary>
/// <param name="License">Absolute path the command named with <c>--license</c>, or null.</param>
/// <param name="Requester">The command's working directory and license variables.</param>
internal sealed record AppLicenseContext(string? License, RequesterLicense Requester)
{
    /// <summary>The working directory license files are resolved against.</summary>
    public string WorkDirectory => Requester.WorkDirectory;

    /// <summary>The value of one license variable, or null when the command did not set it.</summary>
    public string? Read(string name) => Requester.Read(name);

    /// <summary>
    /// The context of the command that started the service, which the App follows until an
    /// <c>app open</c> brings its own.
    /// </summary>
    public static AppLicenseContext Of(ProductCatalog catalog, GlobalValues globals)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(globals);
        return Of(catalog, globals, Environment.GetEnvironmentVariable);
    }

    /// <summary>The context of a command that reads its environment through <paramref name="readEnvironment"/>.</summary>
    public static AppLicenseContext Of(
        ProductCatalog catalog, GlobalValues globals, Func<string, string?> readEnvironment)
    {
        string workDirectory = Path.GetFullPath(globals.WorkDir ?? Directory.GetCurrentDirectory());
        return new AppLicenseContext(
            globals.LicensePath is { } license ? Path.GetFullPath(license, workDirectory) : null,
            RequesterLicense.Capture(
                workDirectory, InvocationEnvironment.LicenseVariables(catalog), readEnvironment));
    }

    /// <summary>The context an <c>app open</c> request carries.</summary>
    public static AppLicenseContext Of(ViewerAppRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new AppLicenseContext(
            request.License,
            request.RequesterLicense ?? throw new InvalidDataException("The app request carries no license context."));
    }

    /// <summary>The request fields that carry this context to the service.</summary>
    public ViewerAppRequest ApplyTo(ViewerAppRequest request) => request with
    {
        License = License,
        RequesterLicense = Requester,
    };

    /// <summary>Whether two commands resolve their license the same way.</summary>
    public static bool Same(AppLicenseContext left, AppLicenseContext right) =>
        string.Equals(left.License, right.License, StringComparison.OrdinalIgnoreCase)
            && RequesterLicense.Same(left.Requester, right.Requester);
}
