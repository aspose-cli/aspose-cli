using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Licensing;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.App;

/// <summary>
/// Everything the App reports about licences and fonts, answered by the CLI
/// itself in a child process. Applying a licence or opening a font collection
/// loads an engine and pins it to the process that did it; the service must
/// stay free of both, so that work happens somewhere it can end. The child
/// also keeps the answers honest: what Settings shows is exactly what
/// <c>aspose-cli license status</c> would tell the person at a prompt.
/// </summary>
internal sealed class AppCliGateway
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private const int MaximumOutputBytes = 512 * 1024;

    private readonly object _gate = new();
    private readonly object _licenseRefresh = new();
    private readonly ProductCatalog _catalog;
    private readonly GlobalValues _globals;
    private readonly string _configDirectory;
    private readonly string _workDirectory;
    private LicenseStatusResult? _license;
    private string? _licenseFingerprint;
    private readonly Dictionary<string, FontListResult> _fonts = new(StringComparer.Ordinal);
    private readonly Func<IReadOnlyList<string>, ChildProcessResult> _run;

    public AppCliGateway(ProductCatalog catalog, GlobalValues globals, string configDirectory)
        : this(catalog, globals, configDirectory, run: null)
    {
    }

    /// <param name="run">Runs one CLI command; tests replace the child process.</param>
    internal AppCliGateway(
        ProductCatalog catalog,
        GlobalValues globals,
        string configDirectory,
        Func<IReadOnlyList<string>, ChildProcessResult>? run)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _globals = globals ?? throw new ArgumentNullException(nameof(globals));
        _configDirectory = configDirectory;
        _workDirectory = Path.GetFullPath(globals.WorkDir ?? Directory.GetCurrentDirectory());
        _run = run ?? RunChild;
    }

    /// <summary>
    /// The licence state as the CLI sees it now. The answer is kept until the
    /// license a product would apply changes — a file installed, replaced or
    /// removed here or at a prompt — and one refresh serves every request
    /// that arrives while it runs.
    /// </summary>
    public LicenseStatusResult LicenseStatus()
    {
        lock (_licenseRefresh)
        {
            string fingerprint = LicenseFingerprint.Of(
                _catalog, _globals.LicensePath, _workDirectory, _configDirectory);
            lock (_gate)
            {
                if (_license is { } cached && _licenseFingerprint == fingerprint)
                {
                    return cached;
                }
            }
            LicenseStatusResult status = Read(
                ["license", "status"],
                SdkJsonContext.Default.LicenseStatusResult,
                CommonSchemaIds.LicenseStatus);
            lock (_gate)
            {
                _license = status;
                _licenseFingerprint = fingerprint;
            }
            return status;
        }
    }

    /// <summary>Installs a licence file for every compatible product, or one named product.</summary>
    public IReadOnlyList<string> InstallLicense(string sourcePath, string? productId)
    {
        string[] arguments = productId is null
            ? ["license", "install", sourcePath]
            : ["license", "install", sourcePath, "--product", productId];
        LicenseStatusResult installed = Read(
            arguments,
            SdkJsonContext.Default.LicenseStatusResult,
            CommonSchemaIds.LicenseStatus);
        Invalidate();
        return installed.Products
            .Where(static product => product.Mode == LicenseModes.Licensed)
            .Select(static product => product.Product)
            .ToArray();
    }

    /// <summary>Removes the saved licence of one product, or of every product.</summary>
    public void RemoveLicense(string? productId)
    {
        string[] arguments = productId is null
            ? ["license", "remove"]
            : ["license", "remove", "--product", productId];
        _ = Read(
            arguments,
            SdkJsonContext.Default.LicenseStatusResult,
            CommonSchemaIds.LicenseStatus);
        Invalidate();
    }

    /// <summary>
    /// The font sources one product renders with. Fonts change far more
    /// rarely than licences, so the first answer is kept for the session.
    /// </summary>
    public FontListResult Fonts(string productId)
    {
        lock (_gate)
        {
            if (_fonts.TryGetValue(productId, out FontListResult? cached))
            {
                return cached;
            }
        }
        FontListResult fonts = Read(
            ["fonts", "list", "--product", productId],
            SdkJsonContext.Default.FontListResult,
            CommonSchemaIds.FontList);
        lock (_gate) { _fonts[productId] = fonts; }
        return fonts;
    }

    /// <summary>A licence change here: the next status reads it afresh, and fonts follow.</summary>
    private void Invalidate()
    {
        lock (_gate)
        {
            _license = null;
            _licenseFingerprint = null;
            _fonts.Clear();
        }
    }

    private T Read<T>(
        IReadOnlyList<string> arguments,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type,
        string schemaId)
        where T : ResultEnvelope
    {
        byte[] output = Run(arguments);
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                output,
                new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("schema", out JsonElement schema)
                || schema.GetString() != schemaId)
            {
                throw new InvalidDataException($"The CLI answered '{arguments[0]}' with another envelope.");
            }
            BoundedJsonValidation.ValidateNoDuplicateProperties(
                document.RootElement,
                static reason => new JsonException(reason));
            return document.RootElement.Deserialize(type)
                ?? throw new InvalidDataException($"The CLI answered '{arguments[0]}' with nothing.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            throw new CliException(
                ErrorCodes.Internal,
                $"The CLI could not report {arguments[0]} state.",
                hint: $"Run 'aspose-cli {string.Join(' ', arguments)}' to see why.");
        }
    }

    /// <summary>
    /// Runs one CLI command and returns its stdout. Child output stays
    /// private: native diagnostics can quote the document or the licence.
    /// </summary>
    private byte[] Run(IReadOnlyList<string> arguments)
    {
        ChildProcessResult child = _run(arguments);
        return child.ExitCode == 0
            ? child.Stdout
            : throw Failure(arguments, child.ExitCode, child.Stderr);
    }

    private ChildProcessResult RunChild(IReadOnlyList<string> arguments)
    {
        ProcessStartInfo start = SelfProcessLauncher.CreateBackground(
            arguments[0],
            "Run the published 'aspose-cli' executable directly.");
        start.WorkingDirectory = SelfProcessLauncher.ServiceWorkingDirectory;
        start.Environment[Aspose.Cli.Sdk.Configuration.ConfigurationPaths.EnvironmentVariableName] =
            _configDirectory;
        start.ArgumentList.Add("--workdir");
        start.ArgumentList.Add(_workDirectory);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add("json");
        start.ArgumentList.Add("--quiet");

        using var cancellation = new CancellationTokenSource(Timeout);
        try
        {
            return ChildProcess.RunAsync(start, MaximumOutputBytes, cancellation.Token)
                .GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            throw CliErrors.OperationTimeout((int)Timeout.TotalSeconds, arguments[0]);
        }
        catch (Exception exception) when (
            exception is IOException or Win32Exception or InvalidOperationException or ChildOutputLimitException)
        {
            throw new CliException(
                ErrorCodes.Internal,
                $"The CLI could not complete '{arguments[0]}'.");
        }
    }

    /// <summary>
    /// Re-raises the child's own error with its own exit code, so the App
    /// reports what the command reported: an invalid licence file stays
    /// LICENSE_INVALID, and a missing file stays an input error.
    /// </summary>
    private static CliException Failure(IReadOnlyList<string> arguments, int exitCode, byte[] error)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(error, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.TryGetProperty("error", out JsonElement failure)
                && failure.TryGetProperty("code", out JsonElement code)
                && code.GetString() is { Length: > 0 } name
                && failure.TryGetProperty("message", out JsonElement message)
                && message.GetString() is { Length: > 0 } text)
            {
                return new CliException(
                    new ErrorCode(name, (ExitCode)exitCode),
                    text,
                    hint: failure.TryGetProperty("hint", out JsonElement hint) ? hint.GetString() : null);
            }
        }
        catch (JsonException)
        {
            // The child did not answer in the envelope; report it as our own.
        }
        return new CliException(
            ErrorCodes.Internal,
            $"The CLI could not complete '{string.Join(' ', arguments)}'.");
    }
}
