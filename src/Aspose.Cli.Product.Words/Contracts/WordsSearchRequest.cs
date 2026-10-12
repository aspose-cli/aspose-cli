using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for bounded document search; the query's scope is one of <see cref="WordsTextScopes"/>.</summary>
public sealed record WordsSearchRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    public required SearchQuery Query { get; init; }
    public Secret? Password { get; init; }
}
