using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>
/// Applies one product SDK license lazily while centralizing source decoding,
/// evaluation behavior, and domain-error translation.
/// </summary>
public abstract class LicenseGate : ILicenseGate
{
    private readonly Func<string, string?> _environmentVariable;
    private readonly Lazy<LicenseState> _state;

    /// <summary>Creates a gate for one resolved product license.</summary>
    protected LicenseGate(
        LicenseResolution resolution,
        Func<string, string?> environmentVariable)
    {
        Resolution = resolution
            ?? throw new ArgumentNullException(nameof(resolution));
        _environmentVariable = environmentVariable
            ?? throw new ArgumentNullException(nameof(environmentVariable));
        _state = new Lazy<LicenseState>(
            Apply,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public bool IsApplicable => true;

    /// <inheritdoc />
    public LicenseResolution Resolution { get; }

    /// <inheritdoc />
    public LicenseState EnsureApplied() => _state.Value;

    /// <summary>
    /// Optional product SDK synchronization root. Override only when the SDK
    /// requires license application to share its process-wide engine lock.
    /// </summary>
    protected virtual object? SynchronizationRoot => null;

    /// <summary>Applies a license from a file path to the product SDK.</summary>
    protected abstract void ApplyLicense(string path);

    /// <summary>Applies a decoded license stream to the product SDK.</summary>
    protected abstract void ApplyLicense(Stream stream);

    /// <summary>
    /// Returns the state after the SDK call succeeds. Products that need an
    /// engine-specific verification probe may override this method.
    /// </summary>
    protected virtual LicenseState VerifyApplied() => LicenseState.Licensed;

    /// <summary>
    /// Allows a product to configure evaluation-mode SDK behavior before the
    /// shared gate returns the evaluation state.
    /// </summary>
    protected virtual void ConfigureEvaluation()
    {
    }

    private LicenseState Apply()
    {
        object? synchronizationRoot = SynchronizationRoot;
        if (synchronizationRoot is null)
        {
            return ApplyCore();
        }

        lock (synchronizationRoot)
        {
            return ApplyCore();
        }
    }

    private LicenseState ApplyCore()
    {
        if (!Resolution.IsConfigured)
        {
            ConfigureEvaluation();
            return LicenseState.Evaluation;
        }

        string source = Resolution.SourceLabel!;
        try
        {
            if (Resolution.Base64EnvironmentVariable is { } variable)
            {
                string value = _environmentVariable(variable)
                    ?? throw CliErrors.LicenseInvalid(
                        source,
                        "the environment variable is empty");
                using var stream = Decode(value, source);
                ApplyLicense(stream);
            }
            else
            {
                ApplyLicense(Resolution.Path!);
            }

            return VerifyApplied();
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CliErrors.LicenseInvalid(
                source,
                exception.Message,
                exception);
        }
    }

    private static MemoryStream Decode(string value, string source)
    {
        try
        {
            return new MemoryStream(Convert.FromBase64String(value));
        }
        catch (FormatException exception)
        {
            throw CliErrors.LicenseInvalid(
                source,
                "the value is not valid base64",
                exception);
        }
    }
}
