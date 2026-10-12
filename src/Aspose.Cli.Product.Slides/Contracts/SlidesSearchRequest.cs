using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for bounded shape and notes search.</summary>
public sealed record SlidesSearchRequest
{
    /// <summary>The presentation the command reads.</summary>
    public required string Input { get; init; }
    /// <summary>The pattern, hit window and scope (one of <see cref="SlidesSearchScopes.Values"/>).</summary>
    public required SearchQuery Query { get; init; }
    public Secret? Password { get; init; }
}
