using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Output;

/// <summary>
/// Renders results and errors. The stream contract is fixed: stdout carries
/// exactly one result, stderr carries diagnostics, warnings and errors—
/// never the other way around, so pipelines stay parseable.
/// </summary>
internal interface IOutputWriter
{
    /// <summary>Writes a successful result to stdout.</summary>
    void WriteResult(ResultEnvelope result);

    /// <summary>Writes an error to stderr.</summary>
    void WriteError(ErrorEnvelope error);
}
