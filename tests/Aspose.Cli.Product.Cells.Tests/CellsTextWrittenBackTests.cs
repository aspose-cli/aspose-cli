using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// An edit that writes a delimited text input back in the format its name gives it keeps its
/// separator and its UTF-8 byte order mark; a conversion, and any other output, has its format's
/// separator, and the mark only with <c>--bom</c>. The checks read the bytes the commands write.
/// The commands run with the test license when the run has one, and in evaluation mode otherwise,
/// whose notice row the comparisons leave aside.
/// </summary>
public sealed class CellsTextWrittenBackTests : IDisposable
{
    private static readonly byte[] Mark = [0xEF, 0xBB, 0xBF];

    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// An edit of one cell of a .csv separated by semicolons, tabs or pipes writes every other row
    /// as the input did, with a value that holds the separator quoted and a decimal comma as it
    /// was, and its verification reads the output back with the one change.
    /// </summary>
    [Theory]
    [InlineData(';')]
    [InlineData('\t')]
    [InlineData('|')]
    public void Edit_WritesTheSeparatorOfTheInput(char separator)
    {
        string[] rows = [$"land{separator}price{separator}note", $"DE{separator}2,71{separator}\"x{separator}y\"", $"FR{separator}3{separator}plain"];
        Write("table.csv", rows, mark: false);

        CliResult edited = Edit("table.csv", "--out", "out.csv", "--ops", SetC3);

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.Equal([.. rows[..2], $"FR{separator}3{separator}changed"], Written("out.csv", mark: false));
        Assert.Equal(["table!C3 plain -> changed"], DirectChanges(edited));
    }

    /// <summary>
    /// An edit in place of a .csv separated by tabs, which the engine detects as tsv while its
    /// name gives it csv, keeps the tabs of every other row.
    /// </summary>
    [Fact]
    public void EditInPlace_OfACsvSeparatedByTabs_KeepsTheTabs()
    {
        string[] rows = ["land\tprice\tnote", "DE\t2,71\tfirst", "FR\t3\tplain"];
        Write("table.csv", rows, mark: false);

        CliResult edited = Edit("table.csv", "--in-place", "--ops", SetC3);

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.Equal([.. rows[..2], "FR\t3\tchanged"], Written("table.csv", mark: false));
    }

    /// <summary>
    /// An edit of a csv that starts with a UTF-8 byte order mark, to another file or in place,
    /// starts its output with the mark and writes every other row as the input did, without
    /// <c>--bom</c>; its verification reads the output back with the one change.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Edit_KeepsTheByteOrderMarkOfTheInput(bool inPlace)
    {
        string[] rows = ["地区,金额,备注", "华东,1200,首批", "华北,900,plain"];
        Write("table.csv", rows, mark: true);
        string output = inPlace ? "table.csv" : "out.csv";

        CliResult edited = inPlace
            ? Edit("table.csv", "--in-place", "--ops", SetC3)
            : Edit("table.csv", "--out", output, "--ops", SetC3);

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.Equal([.. rows[..2], "华北,900,changed"], Written(output, mark: true));
        Assert.Equal(["table!C3 plain -> changed"], DirectChanges(edited));
    }

    /// <summary>
    /// Guardrails: a conversion writes its format's separator and no mark without <c>--bom</c>,
    /// whatever the input's separator and mark, to csv as to tsv; an edit of a .tsv keeps its tabs;
    /// a csv without the mark gets it only with <c>--bom</c>; and a workbook written as csv has
    /// commas and no mark.
    /// </summary>
    [Fact]
    public void ConversionsAndOtherOutputs_HaveTheirFormatsSeparatorAndTheMarkOnlyWithBom()
    {
        Write("semi.csv", ["a;b", "1;2"], mark: true);
        Write("tabs.txt", ["a\tb", "1\t2"], mark: false);
        Write("tabs.tsv", ["a\tb", "1\t2"], mark: false);
        Write("plain.csv", ["a,b", "1,2"], mark: false);
        Assert.Equal(0, Cli("cells", "create", "book.xlsx", "--sheet-names", "Data").ExitCode);
        Assert.Equal(0, Cli("cells", "edit", "book.xlsx", "--in-place", "--set", "Data!A1=a", "--set", "Data!B1=b").ExitCode);

        Assert.Equal(0, Cli("cells", "convert", "semi.csv", "--to", "csv", "--out", "semi-out.csv").ExitCode);
        Assert.Equal(0, Cli("cells", "convert", "tabs.txt", "--to", "csv", "--out", "tabs-out.csv").ExitCode);
        Assert.Equal(0, Cli("cells", "convert", "semi.csv", "--to", "tsv", "--out", "semi.tsv").ExitCode);
        Assert.Equal(0, Edit("tabs.tsv", "--out", "tabs-out.tsv", "--ops", SetB2).ExitCode);
        Assert.Equal(0, Edit("plain.csv", "--out", "plain-out.csv", "--ops", SetB2).ExitCode);
        Assert.Equal(0, Edit("plain.csv", "--out", "plain-bom.csv", "--bom", "--ops", SetB2).ExitCode);
        Assert.Equal(0, Cli("cells", "convert", "book.xlsx", "--to", "csv", "--out", "book.csv").ExitCode);
        Assert.Equal(0, Cli("cells", "edit", "book.xlsx", "--set", "Data!B1=c", "--out", "edited.csv").ExitCode);

        Assert.Equal(["a,b", "1,2"], Written("semi-out.csv", mark: false));
        Assert.Equal(["a,b", "1,2"], Written("tabs-out.csv", mark: false));
        Assert.Equal(["a\tb", "1\t2"], Written("semi.tsv", mark: false));
        Assert.Equal(["a\tb", "1\tchanged"], Written("tabs-out.tsv", mark: false));
        Assert.Equal(["a,b", "1,changed"], Written("plain-out.csv", mark: false));
        Assert.Equal(["a,b", "1,changed"], Written("plain-bom.csv", mark: true));
        Assert.Equal(["a,b"], Written("book.csv", mark: false));
        Assert.Equal(["a,c"], Written("edited.csv", mark: false));
    }

    private const string SetC3 = """{"ops":[{"op":"set_values","range":"C3","values":[["changed"]]}]}""";

    private const string SetB2 = """{"ops":[{"op":"set_values","range":"B2","values":[["changed"]]}]}""";

    private CliResult Edit(string input, params string[] args) =>
        Cli(["cells", "edit", input, .. args, "--verify", "--output-format", "json"]);

    /// <summary>Writes <paramref name="rows"/> as UTF-8, each ended by CRLF, after a byte order mark when <paramref name="mark"/>.</summary>
    private void Write(string name, string[] rows, bool mark) =>
        File.WriteAllText(_workspace.File(name), string.Concat(rows.Select(static row => row + "\r\n")), new UTF8Encoding(mark));

    /// <summary>
    /// The rows of a written text table, each ended by CRLF, without the notice row evaluation
    /// mode writes last, after checking that the file starts with a byte order mark exactly when
    /// <paramref name="mark"/>.
    /// </summary>
    private string[] Written(string name, bool mark)
    {
        byte[] bytes = File.ReadAllBytes(_workspace.File(name));
        Assert.True(bytes.AsSpan().StartsWith(Mark) == mark, $"{name} {(mark ? "does not start" : "starts")} with a byte order mark");
        string text = new UTF8Encoding(false).GetString(bytes[(mark ? Mark.Length : 0)..]);
        int notice = text.LastIndexOf("\r\n", StringComparison.Ordinal) + 2;
        if (notice >= 2 && CellsEvaluation.IsNotice(text[notice..].TrimStart('"')))
        {
            text = text[..notice];
        }

        Assert.EndsWith("\r\n", text, StringComparison.Ordinal);
        return text[..^2].Split("\r\n");
    }

    /// <summary>Each direct change as <c>Sheet!Cell left -> right</c>, without the notice row of evaluation mode.</summary>
    private static string[] DirectChanges(CliResult edited) =>
        [.. JsonNode.Parse(edited.StdOut)!["verification"]!["directChanges"]!.AsArray()
            .Where(static change => !CellsEvaluation.IsNotice(change!["right"]?["v"]?.ToString()))
            .Select(static change => $"{change!["sheet"]}!{change["cell"]} {change["left"]?["v"]} -> {change["right"]?["v"]}")];

    /// <summary>
    /// Runs the CLI with the test license when the run has one (a workspace strips every other
    /// license source), and in evaluation mode otherwise.
    /// </summary>
    private CliResult Cli(params string[] args) =>
        _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);
}
