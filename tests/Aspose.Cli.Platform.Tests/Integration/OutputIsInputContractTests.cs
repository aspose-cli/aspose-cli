using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// A command that writes a file the caller names refuses an output that resolves to one of
/// its inputs: replacing an input is the in-place mode's job alone. The command template owns
/// the rule and its tests cover it for every trait; these cases run it end to end once per
/// output shape, through the real host and products.
/// </summary>
public sealed class OutputIsInputContractTests
{
    /// <summary>
    /// One invocation per output shape whose named output is spelled differently from, but
    /// resolves to, an input; the value is the parameter the error must name.
    /// </summary>
    private static readonly Dictionary<string, (string[] Arguments, string Parameter)> Cases = new(StringComparer.Ordinal)
    {
        ["file"] = (["cells", "convert", "book.xlsx", "--to", "csv", "--out", "./BOOK.xlsx"], "--out"),
        ["required file over product inputs"] = (["pdf", "merge", "doc.pdf", "other.pdf", "--out", "./OTHER.pdf"], "--out"),
        ["file or directory"] = (["pdf", "extract", "doc.pdf", "--what", "forms", "--to", "json", "--out", "./DOC.pdf"], "--out"),
        ["file named by a product option"] = (["pdf", "sign", "doc.pdf", "--certificate", "cert.pfx", "--certificate-password-env", "CERT_PASSWORD", "--out", "./CERT.pfx"], "--out"),
        ["created file"] = (["words", "create", "./DOC.docx", "--template", "doc.docx", "--overwrite"], "file"),
        ["mutation"] = (["cells", "edit", "book.xlsx", "--set", "Sheet1!A1=1", "--out", "./BOOK.xlsx"], "--out"),
    };

    public static TheoryData<string> OutputShapes => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(OutputShapes))]
    public void AnOutputThatResolvesToAnInputIsRefused(string shape)
    {
        using var workspace = new TempWorkspace();
        string[] inputs = ["book.xlsx", "doc.pdf", "other.pdf", "doc.docx", "cert.pfx"];
        foreach (string input in inputs)
        {
            File.WriteAllText(workspace.File(input), "input");
        }
        (string[] arguments, string parameter) = Cases[shape];

        CliResult result = workspace.Run([.. arguments, "--output", "json"]);

        Assert.True(result.ExitCode == 2, $"exit {result.ExitCode}: {result.StdErr}");
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(parameter, error["details"]!["option"]!.GetValue<string>());
        Assert.All(inputs, input => Assert.Equal("input", File.ReadAllText(workspace.File(input))));
    }
}
