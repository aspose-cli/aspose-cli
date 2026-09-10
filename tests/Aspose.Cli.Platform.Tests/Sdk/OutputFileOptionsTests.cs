using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class OutputFileOptionsTests
{
    private static readonly IReadOnlyList<FormatDescriptor> Formats =
    [
        FormatDescriptor.Render("png", 0, ".png"),
        FormatDescriptor.Render("jpeg", 1, ".jpg", ".jpeg"),
    ];

    [Theory]
    [InlineData("result.jpg")]
    [InlineData("result.JPEG")]
    public void EnsureExtension_AcceptsEveryDeclaredExtension(string path)
    {
        (OutputFileOptions output, ParseResult parse) = Parse(path);

        output.EnsureExtension(parse, Formats, "jpeg");
    }

    [Fact]
    public void EnsureExtension_RejectsAConflictingExplicitExtension()
    {
        (OutputFileOptions output, ParseResult parse) = Parse("result.jpg");

        CliException error = Assert.Throws<CliException>(() =>
            output.EnsureExtension(parse, Formats, "png"));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains(".png", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureExtension_AllowsAnOutputWithoutAnExtension()
    {
        (OutputFileOptions output, ParseResult parse) = Parse("result");

        output.EnsureExtension(parse, Formats, "png");
    }

    private static (OutputFileOptions Output, ParseResult Parse) Parse(string path)
    {
        var command = new Command("render");
        var output = new OutputFileOptions("test output");
        output.AddTo(command);
        return (output, command.Parse(["--out", path]));
    }
}
