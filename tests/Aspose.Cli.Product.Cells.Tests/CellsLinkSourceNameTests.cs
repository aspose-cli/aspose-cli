using Aspose.Cli.Product.Cells.Engine;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A new link's source names a sheet when it has no file extension, read as
/// <see cref="Path.GetExtension(string)"/> reads one.
/// </summary>
public sealed class CellsLinkSourceNameTests
{
    [Theory]
    [InlineData("Sheet1", true)]
    [InlineData("Sheet.", true)]
    [InlineData("a.b", false)]
    [InlineData(@"dir\x", true)]
    [InlineData("dir/x", true)]
    [InlineData(@"dir.x\y", true)]
    [InlineData(@"dir\Book.xlsx", false)]
    [InlineData("a.b:c", false)]
    [InlineData(".xlsx", false)]
    [InlineData("", true)]
    public void HasNoExtension_ReadsAnExtensionAsTheFrameworkDoes(string source, bool none)
    {
        Assert.Equal(none, CellsEdit.HasNoExtension(source));
        Assert.Equal(Path.GetExtension(source).Length == 0, CellsEdit.HasNoExtension(source));
    }
}
