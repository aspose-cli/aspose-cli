using Aspose.Cli.Sdk.Execution;
using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>Reads one bounded license snapshot and applies exactly those bytes to a product SDK.</summary>
public abstract class LicenseGate : ILicenseGate
{
    private readonly Func<string, string?> _environmentVariable;
    private readonly Lazy<(LicenseState State, string Identity)> _application;

    protected LicenseGate(LicenseResolution resolution, Func<string, string?> environmentVariable)
    {
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        _environmentVariable = environmentVariable ?? throw new ArgumentNullException(nameof(environmentVariable));
        _application = new Lazy<(LicenseState, string)>(Apply, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsApplicable => true;
    public LicenseResolution Resolution { get; }
    public string Identity => _application.Value.Identity;
    public LicenseState EnsureApplied() => _application.Value.State;

    /// <summary>The product adapter only applies the supplied snapshot to its own SDK.</summary>
    protected abstract void ApplyLicense(Stream stream);

    private (LicenseState, string) Apply()
    {
        if (!Resolution.IsConfigured)
        {
            return (LicenseState.Evaluation, "evaluation");
        }

        string source = Resolution.SourceLabel!;
        try
        {
            byte[] content = ReadContent(source);
            using var stream = new MemoryStream(content, writable: false);
            ApplyLicense(stream);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            string path = Resolution.Path ?? string.Empty;
            if (OperatingSystem.IsWindows()) { path = path.ToUpperInvariant(); }
            hash.AppendData(Encoding.UTF8.GetBytes(source + "\0" + path + "\0"));
            hash.AppendData(content);
            return (LicenseState.Licensed, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        catch (CliException) { throw; }
        catch (Exception exception)
        {
            throw CliErrors.LicenseInvalid(source, exception.Message, exception);
        }
    }

    private byte[] ReadContent(string source)
    {
        if (Resolution.Base64EnvironmentVariable is { } variable)
        {
            string value = _environmentVariable(variable)
                ?? throw CliErrors.LicenseInvalid(source, "the environment variable is empty");
            if (value.Length > ((LicenseInstaller.MaximumBytes + 2L) / 3) * 4)
            {
                throw CliErrors.LicenseInvalid(source, "the license exceeds the one MiB limit");
            }
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value); }
            catch (FormatException exception)
            {
                throw CliErrors.LicenseInvalid(source, "the value is not valid base64", exception);
            }
            if (bytes.Length > LicenseInstaller.MaximumBytes)
            {
                throw CliErrors.LicenseInvalid(source, "the license exceeds the one MiB limit");
            }
            return bytes;
        }

        using FileStream input = File.OpenRead(WorkerOutputSession.ResolveReadPath(Resolution.Path!));
        if (input.Length > LicenseInstaller.MaximumBytes)
        {
            throw CliErrors.LicenseInvalid(source, "the license exceeds the one MiB limit");
        }
        byte[] content = new byte[checked((int)input.Length)];
        input.ReadExactly(content);
        if (input.ReadByte() != -1)
        {
            throw CliErrors.LicenseInvalid(source, "the license changed while it was being read");
        }
        return content;
    }
}

/// <summary>Centralizes product license resolution and deferred invalid-source reporting.</summary>
internal static class ProductLicenseGateFactory
{
    public static ILicenseGate Create(
        ProductActivationContext context,
        string productId,
        Func<LicenseResolution, ILicenseGate> factory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(factory);
        if (context.RuntimeLicenseForProduct is { } runtime)
        {
            return runtime(productId);
        }
        try
        {
            LicenseResolution resolution = LicenseResolver.Resolve(
                context.LicensePathForProduct(productId), productId, context.EnvironmentVariable,
                context.WorkDirectory, context.ConfigDirectory);
            return factory(resolution)
                ?? throw new InvalidOperationException($"Product '{productId}' returned no license gate.");
        }
        catch (CliException exception) { return new UnavailableLicenseGate(exception); }
    }

    private sealed class UnavailableLicenseGate(CliException error) : ILicenseGate
    {
        public bool IsApplicable => true;
        public LicenseResolution Resolution => LicenseResolution.None;
        public string Identity => throw error;
        public LicenseState EnsureApplied() => throw error;
    }
}

internal sealed class LicenseNotApplicableGate : ILicenseGate
{
    public static LicenseNotApplicableGate Instance { get; } = new();
    public bool IsApplicable => false;
    public LicenseResolution Resolution => LicenseResolution.None;
    public string Identity => "not-applicable";
    public LicenseState EnsureApplied() => LicenseState.NotApplicable;
}
