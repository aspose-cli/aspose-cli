using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class TextWidthTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("汇总", 4)]
    [InlineData("\uFF21\uFF22", 4)]
    [InlineData("\uD55C\uAE00", 4)]
    [InlineData("\u304B\u306A", 4)]
    [InlineData("e\u0301", 1)]
    [InlineData("a\u200Bb", 2)]
    [InlineData("\U0001F600", 2)]
    [InlineData("\U00020000", 2)]
    [InlineData("\uFF71", 1)]
    public void Of_CountsTerminalColumns(string text, int expected) =>
        Assert.Equal(expected, TextWidth.Of(text));

    [Fact]
    public void PadRight_PadsToTheDisplayWidth()
    {
        Assert.Equal("汇总  ", TextWidth.PadRight("汇总", 6));
        Assert.Equal("汇总", TextWidth.PadRight("汇总", 3));
    }
}
