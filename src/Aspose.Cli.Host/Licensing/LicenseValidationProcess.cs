using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Licensing;

/// <summary>Inspects a license snapshot or current configuration without changing the App's native SDK state.</summary>
internal static class LicenseValidationProcess
{
    private const int MaximumOutputBytes = 256 * 1024;
    private const int TimeoutSeconds = 30;

    internal static LicenseStatusResult Inspect(
        CommandContext context, string? snapshotPath, string? productId)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? selectedPath = snapshotPath ?? context.Globals.LicensePath;
        string? licensePath = selectedPath is null ? null
            : Path.GetFullPath(selectedPath, context.ProductActivation.WorkDirectory);
        ProductDefinition[] products = productId is null
            ? context.Catalog.Products.ToArray()
            : [context.Catalog.Get(productId)];
        return InspectAsync(context, licensePath, snapshotPath is not null, productId, products).GetAwaiter().GetResult();
    }

    private static async Task<LicenseStatusResult> InspectAsync(
        CommandContext context, string? licensePath, bool validatingSnapshot, string? productId, ProductDefinition[] products)
    {
        context.Deadline.ThrowIfExpired("license-validation");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.Deadline.Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        ProcessStartInfo start = SelfProcessLauncher.CreateBackground(
            "license", "Run license management from the installed aspose-cli executable.");
        start.WorkingDirectory = context.ProductActivation.WorkDirectory;
        start.Environment[Aspose.Cli.Sdk.Configuration.ConfigurationPaths.EnvironmentVariableName] =
            context.ProductActivation.ConfigDirectory;
        foreach (string argument in new[] { "license", "status", "--output", "json", "--quiet" })
        {
            start.ArgumentList.Add(argument);
        }
        if (licensePath is not null)
        {
            start.ArgumentList.Add("--license");
            start.ArgumentList.Add(licensePath);
        }
        if (productId is not null)
        {
            start.ArgumentList.Add("--product");
            start.ArgumentList.Add(products[0].Manifest.Id);
        }

        using var process = new Process { StartInfo = start };
        bool started = false;
        IDisposable? job = null;
        try
        {
            started = process.Start();
            if (!started)
            {
                throw CliErrors.LicenseInvalid("file", "the license validation process could not be started");
            }
            job = WindowsProcessJob.TryAttach(process);
            Task<byte[]> stdout = ReadBoundedAsync(process.StandardOutput.BaseStream, process, cancellation.Token);
            Task<byte[]> stderr = ReadBoundedAsync(process.StandardError.BaseStream, process, cancellation.Token);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(cancellation.Token)).ConfigureAwait(false);
            context.Deadline.ThrowIfExpired("license-validation");
            if (process.ExitCode != 0)
            {
                // Native diagnostics can contain submitted data. Keep child output private.
                throw CliErrors.LicenseInvalid("file", $"the license validation process failed with exit code {process.ExitCode}");
            }
            return Parse(await stdout.ConfigureAwait(false), validatingSnapshot ? licensePath : null, products);
        }
        catch (OperationCanceledException)
        {
            context.Deadline.ThrowIfExpired("license-validation");
            throw CliErrors.OperationTimeout(TimeoutSeconds, "license-validation");
        }
        catch (Exception exception) when (exception is IOException or JsonException or Win32Exception or InvalidOperationException)
        {
            throw CliErrors.LicenseInvalid("file", "the license validation process did not return a valid status");
        }
        finally
        {
            if (started)
            {
                TryKill(process);
                job?.Dispose();
                if (!process.WaitForExit(5000))
                {
                    throw CliErrors.WorkerTerminationFailed(process.Id);
                }
            }
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream, Process process, CancellationToken cancellationToken)
    {
        try
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
                    throw new InvalidDataException("The license validation output exceeded its byte limit.");
                }
                output.Write(buffer, 0, read);
            }
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static LicenseStatusResult Parse(
        byte[] output, string? snapshot, IReadOnlyList<ProductDefinition> products)
    {
        using JsonDocument document = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 16 });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schema", out JsonElement schema)
            || schema.ValueKind != JsonValueKind.String
            || schema.GetString() != CommonSchemaIds.LicenseStatus
            || !root.TryGetProperty("schemaVersion", out JsonElement version)
            || !version.TryGetInt32(out int number) || number != 2
            || !root.TryGetProperty("sharedUserLicenseInstalled", out JsonElement shared)
            || shared.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("The license validation status envelope is invalid.");
        }
        BoundedJsonValidation.ValidateNoDuplicateProperties(root, static reason => new JsonException(reason));
        LicenseStatusResult result = root.Deserialize(SdkJsonContext.Default.LicenseStatusResult)
            ?? throw new InvalidDataException("The license validation status is empty.");
        var expected = products.ToDictionary(static product => product.Manifest.Id, StringComparer.Ordinal);
        if (result.Products is null || result.Products.Count != expected.Count
            || result.Applicable != products.Any(static product => product.Manifest.Engine.LicenseApplicable))
        {
            throw new InvalidDataException("The license validation product set is invalid.");
        }
        StringComparison paths = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (ProductLicenseStatus status in result.Products)
        {
            if (status is null || string.IsNullOrWhiteSpace(status.Product)
                || !expected.Remove(status.Product, out ProductDefinition? product)
                || status.Name != product.Manifest.DisplayName
                || status.Applicable != product.Manifest.Engine.LicenseApplicable)
            {
                throw new InvalidDataException("The license validation product identity is invalid.");
            }
            bool valid = status.Applicable
                ? status.Mode switch
                {
                    LicenseModes.Licensed => !string.IsNullOrWhiteSpace(status.Source) && status.Problem is null,
                    LicenseModes.Invalid => !string.IsNullOrWhiteSpace(status.Problem),
                    LicenseModes.Evaluation => status.Source is null && status.Path is null && status.Problem is null,
                    _ => false,
                }
                : status.Mode == LicenseModes.NotApplicable && status.Source is null && status.Path is null
                    && status.Problem is null;
            if (snapshot is not null && status.Applicable)
            {
                valid &= status.Source == "flag" && string.Equals(status.Path, snapshot, paths)
                    && status.Mode is LicenseModes.Licensed or LicenseModes.Invalid;
            }
            if (!valid)
            {
                throw new InvalidDataException("The license validation product status is invalid.");
            }
        }
        bool invalid = result.Products.Any(static status => status.Mode == LicenseModes.Invalid);
        bool validIdentity = result.Identity is { Length: 64 } identity
            && identity.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (invalid ? result.Identity is not null : !validIdentity)
        {
            throw new InvalidDataException("The license validation identity is invalid.");
        }
        return result;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // The owned process job and exit confirmation remain the final cleanup boundary.
        }
    }
}
