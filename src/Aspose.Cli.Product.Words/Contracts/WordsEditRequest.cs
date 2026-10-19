using System.Diagnostics.CodeAnalysis;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for an atomic Words edit batch.</summary>
public sealed record WordsEditRequest : BoundedEditRequest<WordsOpsBatch>
{
    /// <summary>Creates a request whose fields an object initializer sets.</summary>
    public WordsEditRequest()
    {
    }

    /// <summary>Creates the request of one resolved edit invocation.</summary>
    [SetsRequiredMembers]
    public WordsEditRequest(BoundedEditInvocation<WordsOpsBatch> edit)
        : base(edit)
    {
    }

    public bool TrackChanges { get; init; }
    public string? Author { get; init; }
}
