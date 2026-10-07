using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class ResolvedOutputWriteTests
{
    private static readonly FormatDescriptor Page = FormatDescriptor.Declare("page", FormatUse.Convert, null, 0, null, false, ".html");

    [Fact]
    public void AFormatWithCompanionFilesPublishesThemBesideTheOutputAsOneSet()
    {
        using var temp = new TempDirectory();
        var writer = new SafeFileWriter(TestBudgets.Create());
        var output = new ResolvedOutput(Page with { CompanionFiles = true }, temp.File("deck.html"));

        IReadOnlyList<OutputInfo> published = writer.Write(output, WriteDeck);

        Assert.Equal([temp.File("deck.html"), temp.File("app.js"), temp.File("style.css")], published.Select(static file => file.Path));
        Assert.All(published, static file => Assert.Equal("page", file.Format));
        Assert.Equal("<html>", File.ReadAllText(temp.File("deck.html")));
        Assert.Equal("script", File.ReadAllText(temp.File("app.js")));
        Assert.Equal(["app.js", "deck.html", "style.css"], Directory.GetFileSystemEntries(temp.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        CliException exists = Assert.Throws<CliException>(() => writer.Write(output, WriteDeck));
        Assert.Equal(ErrorCodes.OutputExists, exists.Code);
        Assert.Equal(3, writer.Write(new ResolvedOutput(output.Format, output.Path, overwrite: true), WriteDeck).Count);
    }

    [Fact]
    public void ACompanionThatExistsRefusesTheWholeSet()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("style.css"), "mine");
        var writer = new SafeFileWriter(TestBudgets.Create());

        CliException exists = Assert.Throws<CliException>(() =>
            writer.Write(new ResolvedOutput(Page with { CompanionFiles = true }, temp.File("deck.html")), WriteDeck));

        Assert.Equal(ErrorCodes.OutputExists, exists.Code);
        Assert.Equal(["style.css"], Directory.GetFileSystemEntries(temp.Path).Select(Path.GetFileName));
        Assert.Equal("mine", File.ReadAllText(temp.File("style.css")));
    }

    /// <summary>
    /// A directory the engine writes beside the output is not published: the set is refused as an
    /// output publication failure, and nothing is left beside the output.
    /// </summary>
    [Fact]
    public void ACompanionDirectoryRefusesTheWholeSet()
    {
        using var temp = new TempDirectory();
        var writer = new SafeFileWriter(TestBudgets.Create());

        CliException refused = Assert.Throws<CliException>(() => writer.Write(
            new ResolvedOutput(Page with { CompanionFiles = true }, temp.File("deck.html")),
            path =>
            {
                WriteDeck(path);
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(path)!, "images"));
            }));

        Assert.Equal(ErrorCodes.OutputPublicationFailed, refused.Code);
        Assert.Contains("'images'", refused.Message, StringComparison.Ordinal);
        Assert.Equal(temp.File("deck.html"), refused.Details!["path"]!.GetValue<string>());
        Assert.Empty(Directory.GetFileSystemEntries(temp.Path));
    }

    [Fact]
    public void AFormatWithoutCompanionFilesPublishesOneFile()
    {
        using var temp = new TempDirectory();
        var writer = new SafeFileWriter(TestBudgets.Create());

        IReadOnlyList<OutputInfo> published = writer.Write(
            new ResolvedOutput(Page, temp.File("page.html")),
            path => File.WriteAllText(path, "<html>"));

        OutputInfo file = Assert.Single(published);
        Assert.Equal(temp.File("page.html"), file.Path);
        Assert.Equal(6, file.SizeBytes);
    }

    private static void WriteDeck(string path)
    {
        File.WriteAllText(path, "<html>");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "style.css"), "css");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "app.js"), "script");
    }
}
