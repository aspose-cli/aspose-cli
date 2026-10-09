using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Every Slides result type is produced by a real CLI run of these tests.</summary>
[Collection(ResultSchemaCoverageCollection.Name)]
public sealed class SlidesResultSchemaCoverageTests : ResultSchemaCoverageTests
{
    protected override string ProductId => "slides";
}
