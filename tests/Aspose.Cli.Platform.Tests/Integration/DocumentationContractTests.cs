using System.CommandLine.Parsing;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class DocumentationContractFixture
{
    internal DocumentationCommandValidator Validator { get; } = Load();

    private static DocumentationCommandValidator Load()
    {
        using var workspace = new TempWorkspace();
        return DocumentationCommandValidator.Read(workspace);
    }
}

/// <summary>Repository examples and guard counterexamples use the current executable's grammar.</summary>
public sealed class DocumentationContractTests(DocumentationContractFixture fixture)
    : IClassFixture<DocumentationContractFixture>
{
    [Theory]
    [InlineData("README.md")]
    [InlineData("CONTRIBUTING.md")]
    public void RepositoryDocumentation_UsesCurrentCommandsAndReferences(string name) =>
        fixture.Validator.AssertMarkdown(name, File.ReadAllText(Path.Combine(RepositoryPaths.Root, name)));

    [Theory]
    [InlineData("aspose-cli cells missing-command")]
    [InlineData("aspose-cli cells query missing-command")]
    [InlineData("aspose-cli --quiet cells missing-command")]
    [InlineData("aspose-cli --timeout=30 words missing-command")]
    [InlineData("aspose-cli cells read book.xlsx")]
    [InlineData("aspose-cli docs cells/commands")]
    [InlineData("aspose-cli docs pdf/security")]
    [InlineData("aspose-cli --output json docs unknown-topic")]
    [InlineData("aspose-cli schema v2/pdf/info")]
    [InlineData("aspose-cli fonts check --product pdf")]
    [InlineData("aspose-cli fonts check deck.pptx --product slides")]
    public void Validator_RejectsInvalidSubcommandsAndLiteralReferences(string command) =>
        Assert.NotNull(fixture.Validator.ValidateCommand(command));

    [Theory]
    [InlineData("aspose-cli --version")]
    [InlineData("aspose-cli cells")]
    [InlineData("aspose-cli --timeout 30 cells inspect book.xlsx")]
    [InlineData("aspose-cli cells query range book.xlsx --range A1:B2")]
    [InlineData("aspose-cli preview document.pdf --open")]
    [InlineData("aspose-cli preview status")]
    [InlineData("aspose-cli docs cells/editing")]
    [InlineData("aspose-cli docs pdf/forms-security")]
    [InlineData("aspose-cli schema v2/pdf/pdf-info")]
    [InlineData("aspose-cli schema <id>")]
    [InlineData("aspose-cli cells edit book.xlsx --ops '{\"ops\":[{\"op\":\"set_values\", \"values\":[[\"--opaque\"]]}]}' --in-place")]
    [InlineData("aspose-cli cells convert source.csv --to xlsx --out=\"path with spaces.xlsx\"")]
    [InlineData("aspose-cli docs cells/editing | more")]
    public void Validator_AcceptsCurrentCommandsAndTeachingPlaceholders(string command) =>
        Assert.Null(fixture.Validator.ValidateCommand(command));

    [Fact]
    public void MarkdownExtraction_ChecksInlineAndContinuedCommands()
    {
        string markdown = "Use \u0060aspose-cli\n docs missing-inline-topic\u0060.\n"
            + "\u0060\u0060\u0060powershell\naspose-cli --quiet \u0060\n  cells missing-command\n\u0060\u0060\u0060\n";
        string[] commands = DocumentationCommandValidator.CommandsInMarkdown(markdown).ToArray();
        Assert.Equal(2, commands.Length);
        Assert.All(commands, command => Assert.NotNull(fixture.Validator.ValidateCommand(command)));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ReadmeQuickStart_RunsInAnIsolatedWorkspace()
    {
        using var workspace = new TempWorkspace();
        string readme = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "README.md"));
        Match block = Regex.Match(readme, @"\x60{3}powershell\r?\n(?<body>.*?)\x60{3}", RegexOptions.Singleline);
        Assert.True(block.Success);
        string[] commands = DocumentationCommandValidator.CommandsInMarkdown(block.Value).ToArray();
        Assert.NotEmpty(commands);
        foreach (string command in commands)
        {
            Assert.Null(fixture.Validator.ValidateCommand(command));
            string[] arguments = CommandLineParser.SplitCommandLine(command).Skip(1).ToArray();
            CliResult result = workspace.Run(arguments);
            Assert.True(result.ExitCode == 0, $"{command}\n{result.StdErr}");
        }
        Assert.True(File.Exists(workspace.File(".agents/skills/aspose-cli-cells/SKILL.md")));
    }
}
