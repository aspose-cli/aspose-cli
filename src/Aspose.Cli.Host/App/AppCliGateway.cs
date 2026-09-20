using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Aspose.Cli.Host.Invocation;
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
    private static readonly TimeSpan StatusFreshness = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private const int MaximumOutputBytes = 512 * 1024;

    private readonly object _gate = new();
    private readonly GlobalValues _globals;
    private readonly string _configDirectory;
    private LicenseStatusResult? _license;
    private long _licenseReadAt;
    private readonly Dictionary<string, FontListResult> _fonts = new(StringComparer.Ordinal);

    public AppCliGateway(GlobalValues globals, string configDirectory)
    {
        _globals = globals ?? throw new ArgumentNullException(nameof(globals));
        _configDirectory = configDirectory;
    }

    /// <summary>
    /// The licence state as the CLI sees it now. A change made anywhere —
    /// here, at a prompt, in the environment — is visible within seconds
    /// without restarting anything.
    /// </summary>
    public LicenseStatusResult LicenseStatus()
    {
        lock (_gate)
        {
            if (_license is { } cached
                && Stopwatch.GetElapsedTime(_licenseReadAt) < StatusFreshness)
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
            _licenseReadAt = Stopwatch.GetTimestamp();
        }
        return status;
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
        Invalidate(installed);
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
        Invalidate(Read(
            arguments,
            SdkJsonContext.Default.LicenseStatusResult,
            CommonSchemaIds.LicenseStatus));
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

    private void Invalidate(LicenseStatusResult status)
    {
        lock (_gate)
        {
            _license = status;
            _licenseReadAt = Stopwatch.GetTimestamp();
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
        ProcessStartInfo start = SelfProcessLauncher.CreateBackground(
            arguments[0],
            "Run the published 'aspose-cli' executable directly.");
        start.WorkingDirectory = _globals.WorkDir ?? Directory.GetCurrentDirectory();
        start.Environment[Aspose.Cli.Sdk.Configuration.ConfigurationPaths.EnvironmentVariableName] =
            _configDirectory;
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add("json");
        start.ArgumentList.Add("--quiet");

        using var process = new Process { StartInfo = start };
        using var cancellation = new CancellationTokenSource(Timeout);
        IDisposable? job = null;
        bool started = false;
        try
        {
            started = process.Start();
            if (!started)
            {
                throw new CliException(
                    ErrorCodes.Internal,
                    $"The CLI could not be started to run '{arguments[0]}'.");
            }
            job = WindowsProcessJob.TryAttach(process);
            Task<byte[]> stdout = ReadBoundedAsync(process.StandardOutput.BaseStream, cancellation.Token);
            Task<byte[]> stderr = ReadBoundedAsync(process.StandardError.BaseStream, cancellation.Token);
            Task.WhenAll(stdout, stderr, process.WaitForExitAsync(cancellation.Token))
                .GetAwaiter().GetResult();
            return process.ExitCode == 0
                ? stdout.GetAwaiter().GetResult()
                : throw Failure(arguments, stderr.GetAwaiter().GetResult());
        }
        catch (OperationCanceledException)
        {
            throw CliErrors.OperationTimeout((int)Timeout.TotalSeconds, arguments[0]);
        }
        catch (Exception exception) when (
            exception is IOException or Win32Exception or InvalidOperationException or InvalidDataException)
        {
            throw new CliException(
                ErrorCodes.Internal,
                $"The CLI could not complete '{arguments[0]}'.");
        }
        finally
        {
            if (started)
            {
                try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
                catch (Exception exception) when (exception is InvalidOperationException or Win32Exception) { }
                job?.Dispose();
            }
        }
    }

    /// <summary>
    /// Re-raises the child's own error, so the App reports what the command
    /// reported: an invalid licence file stays LICENSE_INVALID here.
    /// </summary>
    private static CliException Failure(IReadOnlyList<string> arguments, byte[] error)
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
                    new ErrorCode(name, ExitCode.LicenseError),
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

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[4096];
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return output.ToArray();
            }
            if (output.Length + read > MaximumOutputBytes)
            {
                throw new InvalidDataException("The CLI wrote more than its output budget.");
            }
            output.Write(buffer, 0, read);
        }
    }
}
