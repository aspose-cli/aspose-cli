using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Every Words result type is produced by a real CLI run of these tests.</summary>
[Collection(ResultSchemaCoverageCollection.Name)]
public sealed class WordsResultSchemaCoverageTests : ResultSchemaCoverageTests
{
    protected override string ProductId => "words";
}
