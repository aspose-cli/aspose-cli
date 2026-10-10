using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>Reads one bounded license snapshot and applies exactly those bytes to a product SDK.</summary>
public sealed class LicenseGate : ILicenseGate
{
    private readonly Func<string, string?> _environmentVariable;
    private readonly Action<Stream> _applyLicense;
    private readonly Lazy<(LicenseState State, string Identity)> _application;

    /// <summary>Creates a gate that applies the resolved license once, on first use.</summary>
    /// <param name="resolution">Where the license comes from.</param>
    /// <param name="environmentVariable">Reads the environment variable a resolution names.</param>
    /// <param name="applyLicense">Applies the license snapshot to the product's own SDK.</param>
    public LicenseGate(LicenseResolution resolution, Func<string, string?> environmentVariable, Action<Stream> applyLicense)
    {
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        _environmentVariable = environmentVariable ?? throw new ArgumentNullException(nameof(environmentVariable));
        _applyLicense = applyLicense ?? throw new ArgumentNullException(nameof(applyLicense));
        _application = new Lazy<(LicenseState, string)>(Apply, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsApplicable => true;
    public LicenseResolution Resolution { get; }
    public string Identity => _application.Value.Identity;
    public LicenseState EnsureApplied() => _application.Value.State;

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
            _applyLicense(stream);
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
            throw CliErrors.LicenseInvalid(
                source,
                $"it is not an Aspose license the SDK can verify, so it is damaged, edited or not a license ({exception.Message})",
                exception);
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

        using FileStream input = File.OpenRead(Resolution.ContentPath ?? Resolution.Path!);
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

internal sealed class LicenseNotApplicableGate : ILicenseGate
{
    public static LicenseNotApplicableGate Instance { get; } = new();
    public bool IsApplicable => false;
    public LicenseResolution Resolution => LicenseResolution.None;
    public string Identity => "not-applicable";
    public LicenseState EnsureApplied() => LicenseState.NotApplicable;
}
