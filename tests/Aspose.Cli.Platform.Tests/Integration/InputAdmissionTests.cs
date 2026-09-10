using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Real CLI coverage for input budgets without charging output paths.</summary>
public sealed class InputAdmissionTests
{
    public static TheoryData<string, string, string[]> CreateCases => new()
    {
        { "cells", "xlsx", [] },
        { "pdf", "pdf", ["--from-text", "source.txt"] },
        { "slides", "pptx", ["--from-markdown", "source.md"] },
        { "words", "docx", ["--text", "source.txt"] },
    };

    [Theory]
    [MemberData(nameof(CreateCases))]
    public void Create_ExcludesExistingOutputsRegardlessOfPathOrOptionOrder(
        string product, string extension, string[] sourceOptions)
    {
        using var workspace = new TempWorkspace();
        string work = Directory.CreateDirectory(workspace.File("work with spaces")).FullName;
        File.WriteAllText(Path.Combine(work, "source.txt"), "Audit");
        File.WriteAllText(Path.Combine(work, "source.md"), "# Audit");
        string name = "output." + extension;
        string output = Path.Combine(work, name);

        foreach (bool absolute in new[] { false, true })
        {
            byte[] original = new byte[4096];
            File.WriteAllBytes(output, original);
            string argument = absolute ? output : name;
            string[] command =
            [
                "--workdir", work, "--max-input-bytes=64",
                product, "create", .. sourceOptions, argument, "--output", "json",
            ];

            CliResult refused = workspace.Run(command);
            Assert.Equal(5, refused.ExitCode);
            Assert.Equal("OUTPUT_EXISTS", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
            Assert.Equal(original, File.ReadAllBytes(output));

            CliResult created = workspace.Run([.. command, "--overwrite"]);
            Assert.True(created.ExitCode == 0, created.StdErr);
            Assert.True(new FileInfo(output).Length > 64);
            CliResult inspected = workspace.Run(product, "inspect", output, "--output", "json");
            Assert.True(inspected.ExitCode == 0, inspected.StdErr);

            CliResult tooLarge = workspace.Run(
                "--workdir", work, product, "inspect", argument,
                "--max-input-bytes", "64", "--output", "json");
            Assert.Equal(3, tooLarge.ExitCode);
            Assert.Equal("FILE_TOO_LARGE", JsonNode.Parse(tooLarge.StdErr)!["error"]!["code"]!.GetValue<string>());
            Assert.Equal(
                "pre-runtime-metadata",
                JsonNode.Parse(tooLarge.StdErr)!["error"]!["details"]!["phase"]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData("pdf", "pdf", "--from-text")]
    [InlineData("slides", "pptx", "--from-markdown")]
    [InlineData("words", "docx", "--text")]
    public void Create_AdmitsSourceOptionsBeforeTheOutput(string product, string extension, string sourceOption)
    {
        using var workspace = new TempWorkspace();
        string source = workspace.File(product == "slides" ? "source.md" : "source.txt");
        File.WriteAllText(source, new string('x', 128));
        string output = workspace.File("untouched." + extension);

        foreach (bool absolute in new[] { false, true })
        {
            CliResult result = workspace.Run(
                product, "create", sourceOption + "=" + (absolute ? source : Path.GetFileName(source)),
                output, "--max-input-bytes", "64", "--output", "json");
            Assert.Equal(3, result.ExitCode);
            Assert.Equal("FILE_TOO_LARGE", JsonNode.Parse(result.StdErr)!["error"]!["code"]!.GetValue<string>());
            Assert.False(File.Exists(output));
        }
    }

    [Theory]
    [InlineData("--out", false)]
    [InlineData("--out", true)]
    [InlineData("-o", false)]
    [InlineData("-o", true)]
    public void Convert_ExcludesOutputOptionAliasesAndEqualsSyntax(string option, bool equals)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("source.csv"), "Value\n1\n");
        byte[] original = new byte[4096];
        string output = workspace.File("result.xlsx");
        File.WriteAllBytes(output, original);
        string[] outputOption = equals ? [option + "=result.xlsx"] : [option, "result.xlsx"];

        CliResult result = workspace.Run(
            ["cells", "convert", "source.csv", "--to", "xlsx", .. outputOption,
             "--overwrite", "--max-input-bytes", "64", "--output", "json"]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.NotEqual(original, File.ReadAllBytes(output));
    }
}
