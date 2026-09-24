using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class OutputDirectoryOptionTests
{
    [Fact]
    public void Resolve_ReturnsTheDirectoryAndRejectsAnExistingFile()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("taken"), "file");
        var paths = new PathResolver(temp.Path);
        (OutputDirectoryOption option, Command command) = Create(required: true);

        Assert.Equal(temp.File("parts"), option.ResolveRequired(command.Parse(["--out-dir", "parts"]), paths));
        CliException error = Assert.Throws<CliException>(
            () => option.ResolveRequired(command.Parse(["--out-dir", "taken"]), paths));
        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
    }

    [Fact]
    public void Option_HasNoOutAliasAndCanBeOptional()
    {
        (OutputDirectoryOption option, Command command) = Create(required: false);
        ParseResult omitted = command.Parse([]);

        Assert.Empty(omitted.Errors);
        Assert.Null(option.Resolve(omitted, new PathResolver(Path.GetTempPath())));
        Assert.NotEmpty(command.Parse(["--out", "parts"]).Errors);
        Assert.NotEmpty(Create(required: true).Command.Parse([]).Errors);
    }

    private static (OutputDirectoryOption Option, Command Command) Create(bool required)
    {
        var option = new OutputDirectoryOption("Parts.", required);
        var command = new Command("split");
        option.AddTo(command);
        return (option, command);
    }
}
