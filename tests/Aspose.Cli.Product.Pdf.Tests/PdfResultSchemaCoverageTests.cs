using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Every PDF result type is produced by a real CLI run of these tests.</summary>
[Collection(ResultSchemaCoverageCollection.Name)]
public sealed class PdfResultSchemaCoverageTests : ResultSchemaCoverageTests
{
    protected override string ProductId => "pdf";
}
