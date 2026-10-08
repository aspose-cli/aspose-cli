using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// File associations contributed by one product.
/// Default-owner extensions must be unique; accepted-input extensions may overlap.
/// </summary>
public sealed record FileRouteDefinition
{
    /// <summary>Extensions owned by generic app and preview routing.</summary>
    public IReadOnlyList<string> DefaultOwnerExtensions { get; init; } = [];

    /// <summary>Additional extensions accepted by explicit product commands.</summary>
    public IReadOnlyList<string> AcceptedInputExtensions { get; init; } = [];

    /// <summary>Optional bounded recognizer used after deterministic extension routing.</summary>
    public IFileRecognizer? Recognizer { get; init; }
}
