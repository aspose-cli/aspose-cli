using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsMutationBoundaryTests
{
    [Fact]
    public void AutoFitMeasuresFormulaResultsWrittenEarlierInTheSameBatch()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheets", "Data").ExitCode);

        CliResult edited = workspace.Run("cells", "edit", "source.xlsx", "--ops",
            """{"ops":[{"op":"set_formula","sheet":"Data","range":"A1","formula":"=REPT(\"x\",40)"},{"op":"resize_columns","sheet":"Data","from":"A"}]}""",
            "--out", "edited.xlsx", "--output", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        using var workbook = new Workbook(workspace.File("edited.xlsx"));
        Assert.True(workbook.Worksheets["Data"].Cells.GetColumnWidth(0) > 30);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ImageInputBudgetAbortsPublicationEvenInBestEffort(bool bestEffort, bool supervised)
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheets", "Data").ExitCode);
        byte[] image = new byte[128 * 1024];
        ResourceHttpServer.Image.CopyTo(image, 0);
        File.WriteAllBytes(workspace.File("large.png"), image);
        string operations = System.Text.Json.JsonSerializer.Serialize(new
        { ops = new[] { new { op = "insert_image", sheet = "Data", at = "A1", path = workspace.File("large.png") } } });
        var args = new List<string> { "cells", "edit", "source.xlsx", "--ops", operations,
            "--out", "result.xlsx", "--max-input-bytes", "16384", "--output", "json" };
        if (bestEffort) { args.Add("--best-effort"); }
        if (supervised) { args.AddRange(["--timeout", "60"]); }
        CliResult edited = workspace.Run(args.ToArray());
        Assert.NotEqual(0, edited.ExitCode);
        Assert.Contains("FILE_TOO_LARGE", edited.StdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("result.xlsx")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditingEncryptedInputPreservesItsPassword(bool changePassword)
    {
        using var workspace = new TempWorkspace();
        var environment = new Dictionary<string, string?> { ["OLD_PASSWORD"] = "old-test-password", ["NEW_PASSWORD"] = "new-test-password" };
        CliResult create = workspace.RunWithEnv(environment, "cells", "create", "secret.xlsx", "--sheets", "Data", "--encrypt-env", "OLD_PASSWORD", "--output", "json");
        Assert.True(create.ExitCode == 0, create.StdErr);
        var arguments = new List<string> { "cells", "edit", "secret.xlsx", "--password-env", "OLD_PASSWORD", "--set", "Data!A1=7", "--out", "edited.xlsx", "--timeout", "60", "--output", "json" };
        if (changePassword) { arguments.AddRange(["--encrypt-env", "NEW_PASSWORD"]); }
        CliResult edit = workspace.RunWithEnv(environment, arguments.ToArray());
        Assert.True(edit.ExitCode == 0, edit.StdErr);
        Assert.True(JsonNode.Parse(edit.StdOut)!["output"]!["encrypted"]!.GetValue<bool>());
        CliResult inspect = workspace.RunWithEnv(environment, "cells", "inspect", "edited.xlsx", "--password-env", changePassword ? "NEW_PASSWORD" : "OLD_PASSWORD", "--output", "json");
        Assert.True(inspect.ExitCode == 0, inspect.StdErr);
        Assert.True(JsonNode.Parse(inspect.StdOut)!["source"]!["encrypted"]!.GetValue<bool>());
        Assert.NotEqual(0, workspace.Run("cells", "inspect", "edited.xlsx", "--output", "json").ExitCode);
        Assert.DoesNotContain("test-password", edit.StdOut + edit.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportingEncryptedInputToTextDisclosesTheLostProtection()
    {
        using var workspace = new TempWorkspace();
        var environment = new Dictionary<string, string?> { ["EXPORT_PASSWORD"] = "test-export-secret" };
        Assert.Equal(0, workspace.RunWithEnv(environment, "cells", "create", "source.xlsx", "--sheets", "Data",
            "--encrypt-env", "EXPORT_PASSWORD", "--output", "json").ExitCode);
        CliResult result = workspace.RunWithEnv(environment, "cells", "edit", "source.xlsx", "--password-env", "EXPORT_PASSWORD",
            "--set", "Data!A1=7", "--out", "result.csv", "--output", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Contains("WORKBOOK_ENCRYPTION_REMOVED", result.StdOut, StringComparison.Ordinal);
        Assert.False(JsonNode.Parse(result.StdOut)!["output"]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("test-export-secret", result.StdOut + result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlEditIsSelfContainedAndDoesNotBlockLaterWrites()
    {
        using var workspace = new TempWorkspace();
        using (var workbook = new Workbook())
        {
            workbook.Worksheets[0].Name = "Data";
            workbook.Worksheets.Add("Other");
            workbook.Worksheets[0].Cells["A1"].PutValue("image");
            using var picture = new MemoryStream(ResourceHttpServer.Image);
            workbook.Worksheets[0].Pictures.Add(1, 1, picture);
            workbook.Save(workspace.File("source.xlsx"));
        }
        CliResult edit = workspace.Run("cells", "edit", "source.xlsx", "--set", "Data!A1=changed", "--out", "edited.html", "--output", "json");
        Assert.True(edit.ExitCode == 0, edit.StdErr);
        Assert.Contains("data:image", File.ReadAllText(workspace.File("edited.html")), StringComparison.OrdinalIgnoreCase);
        Assert.True(workspace.Run("cells", "inspect", "edited.html", "--output", "json").ExitCode == 0);
        CliResult next = workspace.Run("cells", "edit", "source.xlsx", "--set", "Data!A1=next", "--out", "next.xlsx", "--output", "json");
        Assert.True(next.ExitCode == 0, next.StdErr);
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(workspace.File("source.xlsx"))!, ".aspose-publication-*"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerificationExaminesTheCandidateInSupervisedExecution(bool inPlace)
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheets", "Data").ExitCode);
        var args = new List<string> { "cells", "edit", "source.xlsx", "--set", "Data!A1==1/0", "--verify", "--timeout", "60", "--output", "json" };
        args.AddRange(inPlace ? ["--in-place"] : ["--out", "edited.xlsx"]);
        CliResult edited = workspace.Run(args.ToArray());
        Assert.True(edited.ExitCode == 8, edited.StdErr + edited.StdOut);
        JsonNode verification = JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.False(verification["ok"]!.GetValue<bool>());
        Assert.NotEmpty(verification["formulaErrors"]!.AsArray());
        Assert.DoesNotContain("FILE_NOT_FOUND", edited.StdOut, StringComparison.Ordinal);
    }
}
