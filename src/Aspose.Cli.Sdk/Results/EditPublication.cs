using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Results;

/// <summary>
/// What publishing an edited document produced (<see cref="OutputPipeline{TDocument}.PublishEdit"/>):
/// the output and the product's read-back of the staged output. A dry run publishes nothing, so
/// both are null.
/// </summary>
/// <typeparam name="TVerification">The product's verification of the staged output.</typeparam>
/// <param name="Output">The published output, or null for a dry run.</param>
/// <param name="Verification">The read-back of the staged output, or null when it was not verified.</param>
public sealed record EditPublication<TVerification>(
    OutputInfo? Output,
    TVerification? Verification)
    where TVerification : class;