using System.CommandLine;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.Tests;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every product command that writes a file the caller names refuses an output that
/// resolves to one of its inputs: replacing an input is the in-place mode's job alone.
/// The commands come from this build's command tree, so a new writing command fails
/// here until it has a case, and a case fails until the command resolves its output
/// through the shared SDK rule.
/// </summary>
public sealed class OutputIsInputContractTests
{

    /// <summary>
    /// One invocation per writing command whose named output is spelled differently from,
    /// but resolves to, an input; the value is the parameter the error must name.
    /// </summary>
    private static readonly Dictionary<string, (string[] Arguments, string Parameter)> Cases = new(StringComparer.Ordinal)
    {
        ["cells convert"] = (["cells", "convert", "book.xlsx", "--to", "csv", "--out", "./BOOK.xlsx"], "--out"),
        ["cells render"] = (["cells", "render", "book.xlsx", "--out", "./BOOK.xlsx"], "--out"),
        ["cells edit"] = (["cells", "edit", "book.xlsx", "--set", "Sheet1!A1=1", "--out", "./BOOK.xlsx"], "--out"),
        ["pdf convert"] = (["pdf", "convert", "doc.pdf", "--to", "pdfa-2b", "--out", "./DOC.pdf"], "--out"),
        ["pdf render"] = (["pdf", "render", "doc.pdf", "--out", "./DOC.pdf"], "--out"),
        ["pdf edit"] = (["pdf", "edit", "doc.pdf", "--ops", """{"ops":[]}""", "--out", "./DOC.pdf"], "--out"),
        ["pdf sign"] = (["pdf", "sign", "doc.pdf", "--certificate", "certificate.pfx", "--certificate-password-env", "CERTIFICATE_PASSWORD", "--out", "./DOC.pdf"], "--out"),
        ["pdf merge"] = (["pdf", "merge", "doc.pdf", "other.pdf", "--out", "./OTHER.pdf"], "--out"),
        ["pdf create"] = (["pdf", "create", "./PAGE.png", "--from-images", "page.png", "--overwrite"], "file"),
        ["pdf extract"] = (["pdf", "extract", "doc.pdf", "--what", "forms", "--to", "json", "--out", "./DOC.pdf"], "--out"),
        ["slides convert"] = (["slides", "convert", "deck.pptx", "--to", "pdf", "--out", "./DECK.pptx"], "--out"),
        ["slides render"] = (["slides", "render", "deck.pptx", "--out", "./DECK.pptx"], "--out"),
        ["slides edit"] = (["slides", "edit", "deck.pptx", "--ops", """{"ops":[]}""", "--out", "./DECK.pptx"], "--out"),
        ["slides create"] = (["slides", "create", "./DECK.pptx", "--template", "deck.pptx", "--overwrite"], "file"),
        ["words convert"] = (["words", "convert", "doc.docx", "--to", "pdf", "--out", "./DOC.docx"], "--out"),
        ["words render"] = (["words", "render", "doc.docx", "--out", "./DOC.docx"], "--out"),
        ["words edit"] = (["words", "edit", "doc.docx", "--set", "bookmark:Name=Value", "--out", "./DOC.docx"], "--out"),
        ["words compare"] = (["words", "compare", "doc.docx", "other.docx", "--out", "./OTHER.docx"], "--out"),
        ["words create"] = (["words", "create", "./DOC.docx", "--template", "doc.docx", "--overwrite"], "file"),
    };

    [Fact]
    public void EveryWritingCommandHasACase()
    {
        string[] writing = WritingCommands().Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(writing, Cases.Keys.Order(StringComparer.Ordinal));
    }

    public static TheoryData<string> CommandCases => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(CommandCases))]
    public void AnOutputThatResolvesToAnInputIsRefused(string command)
    {
        using var workspace = new TempWorkspace();
        string[] inputs = ["book.xlsx", "doc.pdf", "other.pdf", "page.png", "deck.pptx", "doc.docx", "other.docx", "certificate.pfx"];
        foreach (string input in inputs)
        {
            File.WriteAllText(workspace.File(input), "input");
        }
        (string[] arguments, string parameter) = Cases[command];

        CliResult result = workspace.RunWithEnv(
            new Dictionary<string, string?> { ["CERTIFICATE_PASSWORD"] = "certificate-secret" },
            [.. arguments, "--output", "json"]);

        Assert.True(result.ExitCode == 2, $"exit {result.ExitCode}: {result.StdErr}");
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(parameter, error["details"]!["option"]!.GetValue<string>());
        Assert.All(inputs, input => Assert.Equal("input", File.ReadAllText(workspace.File(input))));
    }

    /// <summary>
    /// Product commands that name an output file, through <c>--out</c> or an output
    /// argument, and read at least one input file.
    /// </summary>
    private static IEnumerable<string> WritingCommands()
    {
        Command root = ActualCommandTree.Parser.Parse([]).ParseResult.RootCommandResult.Command;
        return root.Subcommands
            .Where(static group => group.Policy().ProductId is not null)
            .SelectMany(static group => Leaves(group, group.Name))
            .Where(static entry => NamesOutput(entry.Command) && ReadsFiles(entry.Command))
            .Select(static entry => entry.Path);
    }

    private static IEnumerable<(string Path, Command Command)> Leaves(Command command, string path) =>
        command.Subcommands.Count == 0
            ? [(path, command)]
            : command.Subcommands.SelectMany(child => Leaves(child, $"{path} {child.Name}"));

    private static bool NamesOutput(Command command) =>
        command.Options.Any(static option => option.Name == "--out")
        || command.Arguments.Any(static argument => argument.GetParameterMetadata().InputKind == InputKind.None);

    private static bool ReadsFiles(Command command) =>
        command.Arguments.Cast<Symbol>().Concat(command.Options)
            .Any(static symbol => symbol.GetParameterMetadata().InputKind == InputKind.File);
}
