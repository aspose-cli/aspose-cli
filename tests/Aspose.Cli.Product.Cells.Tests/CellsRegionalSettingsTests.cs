using System.IO.Compression;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// What a command computes and shows does not change with the machine's regional settings: under
/// German ones, which write a decimal comma and read "1.5" as a date, an edit recalculates the
/// workbook's formulas, a conversion shows dates, amounts and percentages, and a review words its
/// findings as under en-US. Each such command is a CLI process started under the culture it names
/// (<see cref="TempWorkspace.RunUnderCulture"/>).
/// </summary>
public sealed partial class CellsRegionalSettingsTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public CellsRegionalSettingsTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// An edit of B1 recalculates <c>TEXT</c>, <c>VALUE</c> and a concatenation of B1 as under
    /// en-US, and its verification passes: <c>cells read</c>, which calculates the workbook again
    /// as it loads it, reports what it reports under en-US, and the workbook saves the results
    /// en-US calculates, for a program that shows them without calculating. Guardrail: with
    /// <c>--timeout</c> the edit, which then runs in a worker process that the culture of this
    /// run does not reach, saves the same results. That the CLI calculates as en-US whatever
    /// culture its process starts with, the worker's included, is
    /// <c>CliWorkerTests.AWorker_KeepsTheStartupHooksOfTheRun</c>.
    /// </summary>
    [Fact]
    public void EditUnderGermanRegionalSettings_RecalculatesAsUnderEnglish()
    {
        Workbook("book.xlsx",
            """{"op":"set_formula","sheet":"Sheet1","range":"C1","formula":"=TEXT(B1,\"#,##0.00\")"}""",
            """{"op":"set_formula","sheet":"Sheet1","range":"D1","formula":"=VALUE(\"1.5\")"}""",
            """{"op":"set_formula","sheet":"Sheet1","range":"E1","formula":"=B1&\"\""}""");

        (string Culture, string Out, string[] Options)[] edits =
            [("en-US", "english.xlsx", []), ("de-DE", "german.xlsx", []), ("de-DE", "worker.xlsx", ["--timeout", "300"])];
        foreach ((string culture, string output, string[] options) in edits)
        {
            CliResult edited = _workspace.RunUnderCulture(culture,
                ["cells", "edit", "book.xlsx", "--set", "Sheet1!B1=19.9", "--verify", "--out", output, "--output-format", "json", .. options]);
            Assert.True(edited.ExitCode == 0, $"{culture} {output}: {edited.StdErr}{edited.StdOut}");
            Assert.True(edited.Json()["verification"]!["ok"]!.GetValue<bool>(), edited.StdOut);
        }

        string[] english = Row("en-US", "english.xlsx");
        Assert.Equal(["number 19.9", "string 19.90", "number 1.5", "string 19.9"], english);
        Assert.Equal(english, Row("de-DE", "german.xlsx"));
        string[] saved = Saved("english.xlsx");
        Assert.Equal(["19.90", "1.5", "19.9"], saved);
        Assert.Equal(saved, Saved("german.xlsx"));
        Assert.Equal(saved, Saved("worker.xlsx"));
    }

    /// <summary>A conversion to HTML shows a date, an amount, a number and a percentage as under en-US.</summary>
    [Fact]
    public void ConvertUnderGermanRegionalSettings_ShowsValuesAsUnderEnglish()
    {
        Workbook("shown.xlsx",
            """{"op":"set_values","sheet":"Sheet1","range":"A1:A4","values":[[46000],[1234.5],[4321.5],[0.256]]}""",
            """{"op":"format_range","sheet":"Sheet1","range":"A1","style":{"numberFormat":"m/d/yyyy"}}""",
            """{"op":"format_range","sheet":"Sheet1","range":"A2","style":{"numberFormat":"$#,##0.00"}}""",
            """{"op":"format_range","sheet":"Sheet1","range":"A3","style":{"numberFormat":"#,##0.00"}}""",
            """{"op":"format_range","sheet":"Sheet1","range":"A4","style":{"numberFormat":"0.0%"}}""");

        string english = Converted("en-US", "english");

        Assert.All([">12/9/2025<", ">$1,234.50<", ">4,321.50<", ">25.6%<"], shown => Assert.Contains(shown, english, StringComparison.Ordinal));
        Assert.Equal(english, Converted("de-DE", "german"));
    }

    /// <summary>A review's finding that a used area is sparse writes its percentage as under en-US.</summary>
    [Fact]
    public void ReviewUnderGermanRegionalSettings_WordsAFindingAsUnderEnglish()
    {
        // Three cells of the 320 in A1:J32.
        Workbook("sparse.xlsx",
            """{"op":"set_values","sheet":"Sheet1","range":"A1","values":[["a"]]}""",
            """{"op":"set_values","sheet":"Sheet1","range":"B2","values":[["b"]]}""",
            """{"op":"set_values","sheet":"Sheet1","range":"J32","values":[["z"]]}""");

        string[] english = Findings("en-US", "english.review");

        Assert.Equal(["Only 0.94% of the 320 cells in the used area contain data; inspect for stray far-away content or excessive whitespace."], english);
        Assert.Equal(english, Findings("de-DE", "german.review"));
    }

    /// <summary>Creates a workbook and applies <paramref name="operations"/> to it.</summary>
    private void Workbook(string name, params string[] operations)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", name).ExitCode);
        CliResult edited = _workspace.Run("cells", "edit", name, "--in-place", "--output-format", "json",
            "--ops", $$"""{"ops":[{{string.Join(',', operations)}}]}""");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
    }

    /// <summary>B1:E1 of a workbook, as <c>cells read</c> in a process under <paramref name="culture"/> reports each cell's type and value.</summary>
    private string[] Row(string culture, string workbook)
    {
        CliResult read = _workspace.RunUnderCulture(culture,
            "cells", "read", workbook, "--sheet", "Sheet1", "--range", "B1:E1", "--output-format", "json");
        Assert.True(read.ExitCode == 0, read.StdErr);
        return [.. read.Json()["sheet"]!["cells"]![0]!.AsArray().Select(static cell => $"{cell!["t"]} {cell["v"]}")];
    }

    /// <summary>The results the workbook saves for the formulas of C1:E1, as its first sheet's XML holds them.</summary>
    private string[] Saved(string workbook)
    {
        using ZipArchive package = ZipFile.OpenRead(_workspace.File(workbook));
        using var reader = new StreamReader(package.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        Dictionary<string, string> values = SavedValue().Matches(reader.ReadToEnd())
            .ToDictionary(static match => match.Groups["cell"].Value, static match => match.Groups["value"].Value);
        return [.. new[] { "C1", "D1", "E1" }.Select(cell => values.GetValueOrDefault(cell, $"no value in {cell}"))];
    }

    [GeneratedRegex("<c r=\"(?<cell>[A-Z]+[0-9]+)\"[^>]*(?<!/)>(?:(?!</c>).)*?<v>(?<value>[^<]*)</v>", RegexOptions.Singleline)]
    private static partial Regex SavedValue();

    /// <summary>
    /// The HTML a conversion of <c>shown.xlsx</c> in a process under <paramref name="culture"/>
    /// writes, to a file of the same name in <paramref name="folder"/>.
    /// </summary>
    private string Converted(string culture, string folder)
    {
        Directory.CreateDirectory(_workspace.File(folder));
        CliResult converted = _workspace.RunUnderCulture(culture,
            "cells", "convert", "shown.xlsx", "--to", "html", "--out", Path.Combine(folder, "shown.html"), "--output-format", "json");
        Assert.True(converted.ExitCode == 0, $"{culture}: {converted.StdErr}{converted.StdOut}");
        return File.ReadAllText(_workspace.File(Path.Combine(folder, "shown.html")));
    }

    /// <summary>The messages of the sparse-area findings of a review in a process under <paramref name="culture"/>.</summary>
    private string[] Findings(string culture, string evidence)
    {
        CliResult review = _workspace.RunUnderCulture(culture,
            "review", "sparse.xlsx", "--code", "CELLS_USED_RANGE_SPARSE", "--out", evidence, "--output-format", "json");
        Assert.True(review.ExitCode == 0, review.StdErr);
        return [.. review.Json()["findings"]!.AsArray().Select(static finding => finding!["message"]!.GetValue<string>())];
    }
}
