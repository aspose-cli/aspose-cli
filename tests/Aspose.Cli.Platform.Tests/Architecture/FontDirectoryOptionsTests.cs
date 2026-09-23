using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Architecture;

public sealed class FontDirectoryOptionsTests
{
    private static readonly PathResolver Paths = new(Path.GetTempPath());

    [Fact]
    public void Read_UsesAmbientFontsWhenNoDirectoryIsGiven()
    {
        var options = new FontDirectoryOptions();
        var command = new Command("render");
        options.AddTo(command);

        FontSearchProfile profile = options.Read(command.Parse([]), Paths);

        Assert.True(profile.IsAmbient);
        Assert.Empty(profile.Directories);
    }

    [Fact]
    public void Read_DeduplicatesDirectories()
    {
        using var temp = new TempDirectory();
        string root = Path.GetFullPath(temp.Path);
        var options = new FontDirectoryOptions();
        var command = new Command("render");
        options.AddTo(command);

        FontSearchProfile profile = options.Read(command.Parse(
            ["--font-dir", root, "--font-dir", root + Path.DirectorySeparatorChar]), Paths);

        Assert.False(profile.IsAmbient);
        Assert.Equal(root, Assert.Single(profile.Directories));
    }

    [Fact]
    public void Read_ResolvesRelativeDirectoriesAgainstTheWorkingDirectory()
    {
        using var temp = new TempDirectory();
        string root = Path.GetFullPath(temp.Path);
        Directory.CreateDirectory(Path.Combine(root, "fonts"));
        var options = new FontDirectoryOptions();
        var command = new Command("render");
        options.AddTo(command);

        FontSearchProfile profile = options.Read(
            command.Parse(["--font-dir", "fonts"]),
            new PathResolver(root));

        Assert.Equal(Path.Combine(root, "fonts"), Assert.Single(profile.Directories));
    }

    [Fact]
    public void Read_DoesNotConsumeTheFollowingPositionalFile()
    {
        using var temp = new TempDirectory();
        string root = Path.GetFullPath(temp.Path);
        var options = new FontDirectoryOptions();
        var file = new Argument<string>("file");
        var command = new Command("render");
        command.Arguments.Add(file);
        options.AddTo(command);

        ParseResult parse = command.Parse(["--font-dir", root, "document.docx"]);

        Assert.Equal("document.docx", parse.GetRequiredValue(file));
        Assert.Equal(root, Assert.Single(options.Read(parse, Paths).Directories));
    }

    [Fact]
    public void Read_RejectsAReparsePointInTheRootAncestorChain()
    {
        using var temp = new TempDirectory();
        string actual = Path.Combine(temp.Path, "actual");
        string fonts = Path.Combine(actual, "fonts");
        string linked = Path.Combine(temp.Path, "linked");
        Directory.CreateDirectory(fonts);
        try
        {
            Directory.CreateSymbolicLink(linked, actual);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
                or IOException
                or PlatformNotSupportedException)
        {
            return;
        }

        try
        {
            var options = new FontDirectoryOptions();
            var command = new Command("render");
            options.AddTo(command);
            CliException failure = Assert.Throws<CliException>(() => options.Read(
                command.Parse(["--font-dir", Path.Combine(linked, "fonts")]), Paths));

            Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
        }
        finally
        {
            Directory.Delete(linked, recursive: false);
        }
    }

    [Theory]
    [InlineData("missing-relative-fonts")]
    [InlineData(@"\\server\fonts")]
    [InlineData(@"\\?\C:\fonts")]
    public void Read_RejectsMissingOrNonLocalDirectories(string value)
    {
        var options = new FontDirectoryOptions();
        var command = new Command("render");
        options.AddTo(command);

        CliException failure = Assert.Throws<CliException>(() =>
            options.Read(command.Parse(["--font-dir", value]), Paths));

        Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
    }

    [Fact]
    public void Explicit_RejectsAFontFileBeyondTheByteBudget()
    {
        using var temp = new TempDirectory();
        using (FileStream font = File.Create(Path.Combine(temp.Path, "huge.ttf")))
        {
            font.SetLength(64L * 1024 * 1024 + 1);
        }

        CliException failure = Assert.Throws<CliException>(() =>
            FontSearchProfile.Explicit([Path.GetFullPath(temp.Path)]));

        Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
    }
}
