using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Licensing;

/// <summary>License state of the current process.</summary>
public enum LicenseState
{
    /// <summary>The product engine does not use Aspose licensing.</summary>
    NotApplicable,

    /// <summary>No license applied; engine output carries evaluation limitations.</summary>
    Evaluation,

    /// <summary>A valid license is applied to the engine.</summary>
    Licensed,
}

/// <summary>Mapping between <see cref="LicenseState"/> and its wire names.</summary>
public static class LicenseStateExtensions
{
    /// <summary>The contract string of a license state (see <see cref="LicenseModes"/>).</summary>
    public static string ToContractName(this LicenseState state) =>
        state switch
        {
            LicenseState.NotApplicable => LicenseModes.NotApplicable,
            LicenseState.Evaluation => LicenseModes.Evaluation,
            LicenseState.Licensed => LicenseModes.Licensed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
}

/// <summary>
/// Applies the resolved license to the engine, lazily and exactly once per
/// gate. Implemented by engine adapters because license validation is an
/// engine concern; commands that never touch the engine never trigger it.
/// </summary>
public interface ILicenseGate
{
    /// <summary>Whether Aspose licensing applies to this product engine.</summary>
    bool IsApplicable { get; }

    /// <summary>How the license was (or was not) resolved.</summary>
    LicenseResolution Resolution { get; }

    /// <summary>Opaque identity of the source and exact bytes validated by this gate.</summary>
    string Identity { get; }

    /// <summary>
    /// Ensures the license is applied and returns the resulting state.
    /// Idempotent and cached for this gate.
    /// </summary>
    /// <exception cref="Errors.CliException"><c>LICENSE_INVALID</c> when the engine rejects the license.</exception>
    LicenseState EnsureApplied();
}
