using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Tests;
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

    /// <summary>
    /// A product command takes <c>--font-dir</c> exactly when its result depends on fonts:
    /// rendering and conversion always do; every other command is decided here from what
    /// its engine path does, so a new command fails until someone decides it.
    /// </summary>
    private static readonly Dictionary<string, bool> FontDependence = new(StringComparer.Ordinal)
    {
        ["cells inspect"] = false,
        ["cells query range"] = false,
        ["cells query search"] = false,
        ["cells create"] = false,
        ["cells edit"] = true, // auto-fit measures text; --verify renders
        ["cells compare"] = false,
        ["pdf inspect"] = false,
        ["pdf query pages"] = false,
        ["pdf query forms"] = false,
        ["pdf query search"] = false,
        ["pdf create"] = true, // lays out text, HTML and Markdown
        ["pdf merge"] = false,
        ["pdf split"] = false,
        ["pdf extract"] = false,
        ["pdf edit"] = true, // text stamps, watermarks and form appearances
        ["pdf validate"] = false,
        ["pdf sign"] = true, // the visible signature appearance
        ["slides inspect"] = false,
        ["slides query slides"] = false,
        ["slides query search"] = false,
        ["slides create"] = true, // saving shrinks auto-fit text with the fonts' metrics
        ["slides edit"] = true,
        ["slides extract"] = false,
        ["words inspect"] = true, // the page count lays out the document
        ["words query blocks"] = false,
        ["words query search"] = false,
        ["words create"] = true, // fixed-page outputs are laid out
        ["words edit"] = true, // field and TOC updates lay out the document
        ["words compare"] = true, // the redline can be a fixed-page output
        ["words split"] = true, // --by pages lays out the document
        ["words extract"] = false,
    };

    [Fact]
    public void EveryFontDependentProductCommandTakesFontDirectoriesAndNoOtherDoes()
    {
        Command root = ActualCommandTree.Parser.Parse([]).ParseResult.RootCommandResult.Command;
        Dictionary<string, bool> actual = root.Subcommands
            .Where(static group => group.Policy().ProductId is not null)
            .SelectMany(group => Leaves(group, group.Name))
            .ToDictionary(
                static leaf => leaf.Path,
                static leaf => leaf.Command.Options.Any(static option => option.Name == "--font-dir"),
                StringComparer.Ordinal);

        Assert.NotEmpty(actual);
        foreach ((string path, bool hasFonts) in actual)
        {
            bool drawing = path.EndsWith(" render", StringComparison.Ordinal) || path.EndsWith(" convert", StringComparison.Ordinal);
            Assert.True(drawing || FontDependence.ContainsKey(path), $"Decide whether '{path}' depends on fonts.");
            Assert.True(
                hasFonts == (drawing || FontDependence[path]),
                $"'{path}' {(hasFonts ? "takes" : "lacks")} --font-dir against the font-dependence rule.");
        }

        Assert.Empty(FontDependence.Keys.Except(actual.Keys));

        static IEnumerable<(string Path, Command Command)> Leaves(Command command, string path) =>
            command.Subcommands.Count == 0
                ? [(path, command)]
                : command.Subcommands.SelectMany(child => Leaves(child, path + " " + child.Name));
    }

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
        FileSystemLinks.CreateDirectoryLink(linked, actual);

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
