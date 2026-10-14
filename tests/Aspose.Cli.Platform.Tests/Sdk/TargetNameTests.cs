using Aspose.Cli.Sdk.Addressing;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class TargetNameTests
{
    [Theory]
    [InlineData("Q3 plan", "Q3 plan")]
    [InlineData("业绩回顾 Results", "业绩回顾 Results")]
    [InlineData(" Q3 / plan 100% ", "%20Q3 %2F plan 100%25%20")]
    [InlineData("a/b", "a%2Fb")]
    [InlineData("100%", "100%25")]
    [InlineData("%2F", "%252F")]
    [InlineData("tab\there", "tab%09here")]
    [InlineData("line\nbreak\u007f", "line%0Abreak%7F")]
    [InlineData("\t two  words \r\n", "%09%20two  words%20%0D%0A")]
    [InlineData("　全角　", "%E3%80%80全角%E3%80%80")]
    [InlineData("   ", "%20%20%20")]
    [InlineData("a!b's \"c\"", "a!b's \"c\"")]
    [InlineData("😀/x", "😀%2Fx")]
    [InlineData("", "%00")]
    public void Escape_EscapesOnlyWhatBreaksAnAddress(string name, string expected) =>
        Assert.Equal(expected, TargetName.Escape(name));

    [Theory]
    [InlineData("a!b %", "a%21b %25")]
    [InlineData("业绩!", "业绩%21")]
    [InlineData("no separator", "no separator")]
    public void Escape_AlsoEscapesTheSeparatorsAProductNames(string name, string expected) =>
        Assert.Equal(expected, TargetName.Escape(name, '!'));

    [Fact]
    public void Escape_EscapesSeveralSeparatorsAsTheirUtf8Bytes() =>
        Assert.Equal("a%21b%3Ac%E3%80%81d", TargetName.Escape("a!b:c、d", '!', ':', '、'));
}
