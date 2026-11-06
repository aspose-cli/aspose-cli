using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// The commands a review of a workbook names in its hints open the workbook as the review did: for
/// a workbook read with <c>--password-env</c> they pass the same variable on, never the password,
/// and each runs as written; for a workbook without a password they name none.
/// </summary>
public sealed partial class CellsReviewHintCliTests : IDisposable
{
    private const string Variable = "REVIEW_BOOK_PASSWORD";
    private const string Password = "review-hint-secret-41c7";
    private static readonly Dictionary<string, string?> PasswordVariable = new() { [Variable] = Password };
    private readonly TempWorkspace _workspace = new();

    public CellsReviewHintCliTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// The hidden sheet's finding, the warning for a sheet that prints two areas and the warning for
    /// a sheet too tall for one image each give a command with <c>--password-env</c>, which runs as
    /// written; the password is in neither the result nor the evidence.
    /// </summary>
    [Fact]
    public void EncryptedWorkbook_EachHintsCommandPassesThePasswordVariableAndRuns()
    {
        CreateWorkbook("plain.xlsx");
        CliResult encrypted = _workspace.RunWithEnv(PasswordVariable,
            "cells", "convert", "plain.xlsx", "--to", "xlsx", "--out", "enc.xlsx", "--encrypt-env", Variable, "--output-format", "json");
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);

        CliResult review = _workspace.RunWithEnv(PasswordVariable,
            "review", "enc.xlsx", "--password-env", Variable, "--out", "enc.review", "--output-format", "json");

        Assert.True(review.ExitCode is 0 or 8, review.StdErr);
        (string hidden, string areas, string window) = Hints(JsonNode.Parse(review.StdOut)!);
        foreach (string hint in new[] { hidden, areas, window })
        {
            string command = Command(hint);
            Assert.Contains($" --password-env {Variable} ", command, StringComparison.Ordinal);
            DeleteRenderedImages();
            CliResult run = _workspace.RunCommandLine(command, PasswordVariable);
            Assert.True(run.ExitCode == 0, $"{command}{Environment.NewLine}{run.StdErr}");
        }

        Assert.DoesNotContain(Password, review.StdOut + review.StdErr, StringComparison.Ordinal);
        Assert.All(Directory.EnumerateFiles(_workspace.File("enc.review"), "*", SearchOption.AllDirectories), file =>
        {
            byte[] bytes = File.ReadAllBytes(file);
            Assert.DoesNotContain(Password, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain(Password, Encoding.Unicode.GetString(bytes), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Without a password the same three hints name the workbook and nothing else: the commands
    /// they named before, which now end in <c>--output-format json</c> as every continuation does.
    /// </summary>
    [Fact]
    public void WorkbookWithoutAPassword_HintsNameNoPasswordVariable()
    {
        CreateWorkbook("plain.xlsx");

        CliResult review = _workspace.Run("review", "plain.xlsx", "--out", "plain.review", "--output-format", "json");

        Assert.True(review.ExitCode is 0 or 8, review.StdErr);
        (string hidden, string areas, string window) = Hints(JsonNode.Parse(review.StdOut)!);
        string input = $"\"{_workspace.File("plain.xlsx")}\"";
        Assert.Equal($"If it should be seen, render it on its own: aspose-cli cells render {input} --sheets 2 --output-format json", hidden);
        Assert.Equal($"Draw each other area on its own: aspose-cli cells render {input} --range \"'Data'!D1:E5\" --output-format json", areas);
        Assert.Matches(
            "^Render the next window, then the ones after it the same way: aspose-cli cells render "
                + Regex.Escape(input)
                + " --range \"'Big'!A[0-9]+:A[0-9]+\" --output-format json; or trust 'cells read' for the data\\.$",
            window);
    }

    /// <summary>
    /// A workbook whose sheets are a visible <c>Data</c> printing two areas, a hidden sheet, and a
    /// <c>Big</c> sheet 3000 rows tall.
    /// </summary>
    private void CreateWorkbook(string file)
    {
        CliResult created = _workspace.Run("cells", "create", file, "--sheet-names", "Data,Hidden,Big", "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
        File.WriteAllText(_workspace.File("layout.json"), """
            {"ops":[
             {"op":"set_values","sheet":"Data","range":"A1","values":[["a","b","c","d","e"],[1,2,3,4,5],[1,2,3,4,5],[1,2,3,4,5],[1,2,3,4,5]]},
             {"op":"set_print_area","sheet":"Data","range":"A1:B5,D1:E5"},
             {"op":"set_values","sheet":"Hidden","range":"A1","values":[["secret"]]},
             {"op":"set_sheet_visibility","sheet":"Hidden","visibility":"hidden"},
             {"op":"set_values","sheet":"Big","range":"A1","values":[["top"]]},
             {"op":"set_values","sheet":"Big","range":"A3000","values":[["bottom"]]}
            ]}
            """);
        CliResult laidOut = _workspace.Run("cells", "edit", file, "--in-place", "--ops", "layout.json", "--output-format", "json");
        Assert.True(laidOut.ExitCode == 0, laidOut.StdErr);
    }

    /// <summary>The hint of the hidden sheet's finding, of the two print areas and of the tall sheet's window.</summary>
    private static (string Hidden, string Areas, string Window) Hints(JsonNode review)
    {
        JsonNode hidden = Assert.Single(review["findings"]!.AsArray(), static finding => finding!["code"]!.GetValue<string>() == "CELLS_SHEET_HIDDEN")!;
        JsonNode[] partial = [.. review["warnings"]!.AsArray()
            .Where(static warning => warning!["code"]!.GetValue<string>() == "SHEET_PARTIALLY_RENDERED")
            .Select(static warning => warning!)];
        string Of(string sheet) => Assert.Single(partial, warning => warning["location"]!.GetValue<string>() == "sheet/" + sheet)["hint"]!.GetValue<string>();
        return (hidden["hint"]!.GetValue<string>(), Of("Data"), Of("Big"));
    }

    /// <summary>The command a hint names, from <c>aspose-cli</c> to the <c>--output-format json</c> it ends with.</summary>
    private static string Command(string hint)
    {
        Match command = CommandPattern().Match(hint);
        Assert.True(command.Success, hint);
        return command.Value;
    }

    // Each render writes the workbook's default image; the next command must find no file there.
    private void DeleteRenderedImages()
    {
        foreach (string image in Directory.EnumerateFiles(_workspace.Path, "enc*.png"))
        {
            File.Delete(image);
        }
    }

    [GeneratedRegex("aspose-cli .*? --output-format json")]
    private static partial Regex CommandPattern();
}
