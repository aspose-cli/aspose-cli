using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Architecture;

public sealed class FontDirectoryOptionsTests
{
    [Fact]
    public void Read_UsesAmbientFontsWhenNoExplicitRootIsGiven()
    {
        var options = new FontDirectoryOptions();
        var command = new Command("render");
        options.AddTo(command);

        FontSearchProfile profile = options.Read(command.Parse([]));

        Assert.True(profile.UseAmbientSystemFonts);
        Assert.Empty(profile.Directories);
        Assert.Matches("^[0-9a-f]{64}$", profile.Fingerprint);
    }

    [Fact]
    public void Read_DeduplicatesExplicitRootsAndDisablesAmbientDiscovery()
    {
        using var temp = new TempDirectory();
        string root = Path.GetFullPath(temp.Path);
        var options = new FontDirectoryOptions();
        var command = new Command("render");
        options.AddTo(command);

        FontSearchProfile first = options.Read(command.Parse(
            ["--font-dir", root, "--font-dir", root + Path.DirectorySeparatorChar]));
        FontSearchProfile second = options.Read(command.Parse(
            ["--font-dir", root]));

        Assert.False(first.UseAmbientSystemFonts);
        Assert.Equal(root, Assert.Single(first.Directories));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
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
        Assert.Equal(root, Assert.Single(options.Read(parse).Directories));
    }

    [Fact]
    public void Read_FingerprintChangesWhenSameLengthFontContentChanges()
    {
        using var temp = new TempDirectory();
        string root = Path.GetFullPath(temp.Path);
        string font = Path.Combine(root, "enterprise.ttf");
        File.WriteAllText(font, "font-A");
        var options = new FontDirectoryOptions();
        var command = new Command("render");
        options.AddTo(command);

        string first = options.Read(command.Parse(["--font-dir", root])).Fingerprint;
        FontSearchProfile profile = options.Read(command.Parse(["--font-dir", root]));
        Assert.True(profile.IsCurrent());
        File.WriteAllText(font, "font-B");
        string second = options.Read(command.Parse(["--font-dir", root])).Fingerprint;

        Assert.NotEqual(first, second);
        Assert.False(profile.IsCurrent());
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
                command.Parse(["--font-dir", Path.Combine(linked, "fonts")])));

            Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
        }
        finally
        {
            Directory.Delete(linked, recursive: false);
        }
    }

    [Theory]
    [InlineData("relative-fonts")]
    [InlineData("\\\\server\\fonts")]
    [InlineData("\\\\?\\C:\\fonts")]
    public void Read_RejectsNonLocalOrNonAbsoluteRoots(string value)
    {
        var options = new FontDirectoryOptions();
        var command = new Command("render");
        options.AddTo(command);

        CliException failure = Assert.Throws<CliException>(() =>
            options.Read(command.Parse(["--font-dir", value])));

        Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
    }
}
