namespace Aspose.Cli.Sdk.Licensing;

/// <summary>
/// The license state of one product invocation, which the SDK write pipeline resolves once.
/// Services that only read documents depend on this, not on the pipeline that publishes.
/// </summary>
public interface ILicenseState
{
    /// <summary>
    /// The license state. The first read applies the product's license to its engine, so read
    /// it before the engine opens a document; later reads return the same state.
    /// </summary>
    /// <exception cref="Errors.CliException"><c>LICENSE_INVALID</c> when the engine rejects the license.</exception>
    LicenseState License { get; }

    /// <summary>Whether the engine runs in evaluation mode.</summary>
    bool IsEvaluation => License == LicenseState.Evaluation;
}
