using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for transactional document splitting.</summary>
public sealed record WordsSplitRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    /// <summary>How the document is divided: by sections, at each Heading 1, or by pages.</summary>
    public required SplitPlan Plan { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public Secret? Password { get; init; }
}
