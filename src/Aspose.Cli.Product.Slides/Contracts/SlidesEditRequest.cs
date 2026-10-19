using System.Diagnostics.CodeAnalysis;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for one atomic presentation edit batch.</summary>
public sealed record SlidesEditRequest : BoundedEditRequest<SlidesOpsBatch>
{
    /// <summary>Creates a request whose fields an object initializer sets.</summary>
    public SlidesEditRequest()
    {
    }

    /// <summary>Creates the request of one resolved edit invocation.</summary>
    [SetsRequiredMembers]
    public SlidesEditRequest(BoundedEditInvocation<SlidesOpsBatch> edit)
        : base(edit)
    {
    }
}
