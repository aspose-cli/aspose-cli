using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Every Cells result type is produced by a real CLI run of these tests.</summary>
[Collection(ResultSchemaCoverageCollection.Name)]
public sealed class CellsResultSchemaCoverageTests : ResultSchemaCoverageTests
{
    protected override string ProductId => "cells";
}
