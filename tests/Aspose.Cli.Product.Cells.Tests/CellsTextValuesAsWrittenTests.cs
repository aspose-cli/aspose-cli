using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A delimited text input read without <c>--culture</c> holds a number, a date or a boolean only
/// where the cell the engine types shows the text it read, and a delimited text output writes such
/// a cell as it shows; every other value is text. So an edit leaves each value no operation changed
/// as the input wrote it, and <c>cells read</c> reports it as written or as the value it shows.
/// The checks compare the text of the files the commands write, never a read of them. The commands
/// run with the test license when the run has one, and in evaluation mode otherwise, whose notice
/// row the comparisons leave aside.
/// </summary>
public sealed class CellsTextValuesAsWrittenTests : IDisposable
{
    /// <summary>
    /// Each value, what <c>cells read</c> reports for it (null for the text it is), and how a
    /// workbook converted from the text writes it back as csv (null when as written).
    /// </summary>
    private static readonly (string Value, string? Read, string? Stored)[] Values =
    [
        ("19.90", "number 19.9", "19.9"), ("1.50", "number 1.5", "1.5"), ("1,234.50", "number 1234.5", "1234.5"),
        ("50%", "number 0.5", "0.5"), ("$1,200.50", "number 1200.5", "1200.5"), ("(5)", "number -5", "-5"),
        ("$0.50", "number 0.5", "0.5"), ("20", "number 20", null), ("3.14", "number 3.14", null),
        ("2026-01-05", "datetime 2026-01-05", null), ("2026/10/31", "datetime 2026-10-31", "2026-10-31"),
        ("2026-1-5", "datetime 2026-01-05", "2026-01-05"), ("2026-01-05 10:00", "datetime 2026-01-05T10:00:00", "2026-01-05T10:00:00"),
        ("TRUE", "boolean true", "True"), ("FALSE", "boolean false", "False"),
        ("007", null, null), (" 007", null, null), ("$007", null, null), ("(007)", null, null), ("+86138", null, null),
        ("1E5", null, null), ("123456789012", null, null), ("0.30000000000000004", null, null), ("1/2/2026", null, null),
        ("1/2", null, null), ("12:30", null, null), ("2026-11", null, null), ("2026/10", null, null), ("True", null, null),
        ("true", null, null), ("False", null, null), ("-0", null, null), ("3 1/2", null, null), ("0x1F", null, null),
        ("=1+1", null, null), ("abc", null, null), ("2026-01-05T10:00:00", null, null),
    ];

    /// <summary>Ways to write a number, which an edit of another cell writes back as they are.</summary>
    private static readonly string[] Numbers =
    [
        "0", "-0", "0.1", "1E-06", "1e5", ".5", "1.", "00", "123456789012345", "1234567890123456",
        "12345678901234567", "12345678901234568", "0.30000000000000004", "1.2345678901234567E+19",
        "NaN", "Infinity", "-Infinity", "5E-324", "1E+16", "10000000000000000", "1E+20",
        "100000000000000000000", "1E+21", "1.5E+300", "1.7976931348623157E+308", "1E+400", "-1E-05",
        "0.000123456789012345", "19.90", "0.50", "100.0", "1,234", "12.5%", "123456789012", "12345678901",
    ];

    // An order list whose ids start with zeros and whose prices show two decimals.
    private static readonly string[] Orders =
        ["order,item,price,qty", "007,Widget,19.90,2", "008,Gadget,5.00,1", "009,Cable,12.50,3"];

    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>An edit of one cell leaves every other line of a csv or tsv as the input wrote it.</summary>
    [Theory]
    [InlineData("values.csv", ',')]
    [InlineData("values.tsv", '\t')]
    public void EditOfAnotherCell_WritesEveryOtherValueAsTheInputDid(string input, char separator)
    {
        string[] rows = Table(input, separator, [.. Values.Select(static value => value.Value)]);

        CliResult edited = Cli("cells", "edit", input, "--out", "out" + Path.GetExtension(input), "--verify", "--output-format", "json",
            "--ops", $$"""{"ops":[{"op":"set_values","range":"B{{rows.Length}}","values":[["changed"]]}]}""");

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.Equal([.. rows[..^1], $"total{separator}changed"], Written("out" + Path.GetExtension(input)));
    }

    /// <summary>
    /// <c>cells read</c> reports a number or a date as the value the text shows, and any other
    /// value as the text it is.
    /// </summary>
    [Fact]
    public void Read_ReportsAValueAsTheInputWroteIt()
    {
        Table("values.csv", ',', [.. Values.Select(static value => value.Value)]);

        CliResult read = Cli("cells", "read", "values.csv", "--output-format", "json");

        Assert.True(read.ExitCode == 0, $"exit {read.ExitCode}: {read.StdErr}{read.StdOut}");
        JsonArray cells = read.Json()["sheet"]!["cells"]!.AsArray();
        Assert.Empty(Values.Select((value, index) => (value, cell: cells[index + 1]![1]!))
            .Select(static pair => (pair.value.Value, Expected: pair.value.Read ?? $"string {pair.value.Value}", Actual: $"{pair.cell["t"]} {pair.cell["v"]}"))
            .Where(static check => check.Actual != check.Expected)
            .Select(static check => $"[{check.Value}] read as {check.Actual}, not {check.Expected}"));
    }

    /// <summary>Each way to write a number comes back from an edit of another cell as the input wrote it.</summary>
    [Fact]
    public void EditOfAnotherCell_WritesEveryNumberAsTheInputDid()
    {
        string[] rows = Table("numbers.csv", ',', Numbers);

        CliResult edited = Cli("cells", "edit", "numbers.csv", "--out", "out.csv", "--output-format", "json",
            "--ops", $$"""{"ops":[{"op":"set_values","range":"B{{rows.Length}}","values":[["after"]]}]}""");

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        string[] written = Written("out.csv");
        Assert.Equal(rows.Length, written.Length);
        Assert.Empty(Enumerable.Range(1, Numbers.Length)
            .Where(row => written[row] != rows[row])
            .Select(row => $"[{Numbers[row - 1]}] written as [{written[row]}]"));
        Assert.Equal("total,after", written[^1]);
    }

    /// <summary>
    /// Prices that show two decimals and amounts with thousands separators add up, and ids that
    /// start with zeros stay text: in a workbook converted from the csv, and in the csv itself,
    /// whose other rows a sum written into it leaves as they were.
    /// </summary>
    [Fact]
    public void Sums_AddTheNumbersAndKeepTheIds()
    {
        Write("orders.csv", Orders);
        Write("amounts.csv", ["region,amount", "East,\"1,234.50\"", "West,\"2,000.00\""]);

        Assert.Equal(0, Cli("cells", "convert", "orders.csv", "--to", "xlsx", "--out", "orders.xlsx").ExitCode);
        Assert.Equal(0, Cli("cells", "convert", "amounts.csv", "--to", "xlsx", "--out", "amounts.xlsx").ExitCode);
        CliResult orders = Cli("cells", "edit", "orders.xlsx", "--in-place", "--verify", "--output-format", "json",
            "--ops", """{"ops":[{"op":"set_formula","sheet":"orders","range":"C5","formula":"=SUM(C2:C4)"}]}""");
        CliResult amounts = Cli("cells", "edit", "amounts.xlsx", "--in-place", "--verify", "--output-format", "json",
            "--ops", """{"ops":[{"op":"set_formula","sheet":"amounts","range":"B4","formula":"=SUM(B2:B3)"}]}""");
        CliResult text = Cli("cells", "edit", "orders.csv", "--out", "total.csv", "--verify", "--output-format", "json",
            "--ops", """{"ops":[{"op":"set_values","range":"A5","values":[["total"]]},{"op":"set_formula","range":"C5","formula":"=SUM(C2:C4)"}]}""");

        Assert.True(orders.ExitCode == 0 && amounts.ExitCode == 0 && text.ExitCode == 0, orders.StdErr + amounts.StdErr + text.StdErr);
        Assert.Equal(["string 007", "number 37.4"], Cells("orders.xlsx", "orders", "A2", "C5"));
        Assert.Equal(["number 3234.5"], Cells("amounts.xlsx", "amounts", "B4"));
        Assert.Equal([.. Orders, "total,,37.4,"], Written("total.csv"));
    }

    /// <summary>
    /// A value an operation writes into a cell that shows two decimals is written as the cell
    /// shows it when that reads back as the value, and as the value when it does not.
    /// </summary>
    [Fact]
    public void ValueWrittenIntoAFormattedCell_IsWrittenAsShownOnlyWhenThatReadsBackAsIt()
    {
        Write("prices.csv", ["item,price", "a,19.90", "b,19.90", "c,19.90"]);

        CliResult edited = Cli("cells", "edit", "prices.csv", "--out", "out.csv", "--verify", "--output-format", "json",
            "--ops", """{"ops":[{"op":"set_values","range":"B2","values":[[21.5]]},{"op":"set_values","range":"B3","values":[[19.945]]}]}""");

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.Equal(["item,price", "a,21.50", "b,19.945", "c,19.90"], Written("out.csv"));
    }

    /// <summary>
    /// A csv converted to xlsx and back holds every text value as the input wrote it and every
    /// number and date as the workbook stores it, as any workbook converted to csv does.
    /// </summary>
    [Fact]
    public void ConvertToXlsxAndBack_WritesTextAsWrittenAndNumbersAsStored()
    {
        string[] rows = Table("values.csv", ',', [.. Values.Select(static value => value.Value)]);

        CliResult workbook = Cli("cells", "convert", "values.csv", "--to", "xlsx", "--out", "values.xlsx", "--output-format", "json");
        CliResult text = Cli("cells", "convert", "values.xlsx", "--to", "csv", "--out", "back.csv", "--output-format", "json");

        Assert.True(workbook.ExitCode == 0, $"exit {workbook.ExitCode}: {workbook.StdErr}{workbook.StdOut}");
        Assert.True(text.ExitCode == 0, $"exit {text.ExitCode}: {text.StdErr}{text.StdOut}");
        Assert.Equal(
            [rows[0], .. Values.Select(static (value, index) => value.Stored is { } stored ? $"r{index + 1:00},{stored}" : null)
                .Zip(rows[1..^1], static (stored, row) => stored ?? row), rows[^1]],
            Written("back.csv"));
    }

    /// <summary>
    /// Under German regional settings, which write a decimal comma and fail on a fraction such as
    /// 3 1/2, a csv reads and writes as under any other: an edit leaves every other value as
    /// written, a sum is written with a decimal point, and a workbook converted to csv writes its
    /// stored numbers with a decimal point. Each command is a CLI process the startup hook of this
    /// assembly (<c>StartupHook.cs</c>) starts under de-DE.
    /// </summary>
    [Fact]
    public void UnderGermanRegionalSettings_TextReadsAndWritesAsUnderAnyOther()
    {
        string[] values = Table("values.csv", ',', [.. Values.Select(static value => value.Value)]);
        Write("orders.csv", Orders);

        CliResult[] runs;
        using (CliWorkers.ChildProcesses())
        {
            runs =
            [
                German("cells", "edit", "values.csv", "--out", "values-out.csv", "--output-format", "json",
                    "--ops", $$"""{"ops":[{"op":"set_values","range":"B{{values.Length}}","values":[["changed"]]}]}"""),
                German("cells", "edit", "orders.csv", "--out", "total.csv", "--output-format", "json",
                    "--ops", """{"ops":[{"op":"set_values","range":"A5","values":[["total"]]},{"op":"set_formula","range":"C5","formula":"=SUM(C2:C4)"}]}"""),
                German("cells", "convert", "orders.csv", "--to", "xlsx", "--out", "orders.xlsx", "--output-format", "json"),
                German("cells", "convert", "orders.xlsx", "--to", "csv", "--out", "stored.csv", "--output-format", "json"),
            ];
        }

        Assert.All(runs, static run => Assert.True(run.ExitCode == 0, $"exit {run.ExitCode}: {run.StdErr}{run.StdOut}"));
        Assert.Equal([.. values[..^1], "total,changed"], Written("values-out.csv"));
        Assert.Equal([.. Orders, "total,,37.4,"], Written("total.csv"));
        Assert.Equal(["order,item,price,qty", "007,Widget,19.9,2", "008,Gadget,5,1", "009,Cable,12.5,3"], Written("stored.csv"));
    }

    /// <summary>
    /// Runs the CLI as <see cref="Cli"/> does, in a child process started under German regional
    /// settings by the startup hook of this assembly.
    /// </summary>
    private CliResult German(params string[] args) =>
        _workspace.RunWithEnv(
            new Dictionary<string, string?> { ["DOTNET_STARTUP_HOOKS"] = typeof(CellsTextValuesAsWrittenTests).Assembly.Location },
            TestLicense.Path is { } license ? [.. args, "--license", license] : args);

    // German prices separated by semicolons: decimal commas (2,71, 0,5) beside numbers a comma
    // would group into thousands elsewhere (1,234, 12,500), a whole number and a date.
    private static readonly string[] GermanPrices =
        ["Artikel;Preis", "A;2,71", "B;1,234", "C;0,5", "D;12,500", "E;20", "F;2026-01-05"];

    /// <summary>
    /// Decimal commas, and every number written with a comma in a file that writes them, are the
    /// text they are without <c>--culture</c>: <c>cells read</c> reports them as written, and an
    /// edit of another cell writes their rows back as the input did, warns, and verifies the one
    /// change it made, since it reads its output as it read its input.
    /// </summary>
    [Fact]
    public void DecimalCommas_ReadAndWriteBackAsTheInputWroteThem()
    {
        string[] rows = ["Land,Preis", "DE,\"2,71\"", "FR,\"1.253,96\"", "IT,\"1,23\"", "US,\"1,234\"", "UK,\"12,500\"", "CH,20"];
        Write("prices.csv", rows);

        CliResult read = Cli("cells", "read", "prices.csv", "--output-format", "json");
        CliResult edited = Cli("cells", "edit", "prices.csv", "--out", "out.csv", "--verify", "--output-format", "json",
            "--ops", """{"ops":[{"op":"set_values","range":"A1","values":[["Country"]]}]}""");

        Assert.True(read.ExitCode == 0, $"exit {read.ExitCode}: {read.StdErr}{read.StdOut}");
        Assert.Equal(
            ["string 2,71", "string 1.253,96", "string 1,23", "string 1,234", "string 12,500", "number 20"],
            read.Json()["sheet"]!["cells"]!.AsArray().Skip(1).Select(static row => $"{row![1]!["t"]} {row[1]!["v"]}"));
        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.Equal(["Country,Preis", .. rows[1..]], Written("out.csv"));
        Assert.Contains("NUMBERS_READ_AS_TEXT", Warnings(edited));
        JsonNode verification = edited.Json()["verification"]!;
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.Equal(["A1"], verification["directChanges"]!.AsArray()
            .Where(static change => !CellsEvaluation.IsNotice(change!["right"]?["v"]?.ToString()))
            .Select(static change => change!["cell"]!.GetValue<string>()));
    }

    /// <summary>
    /// In a csv that writes a decimal comma, before or after its other numbers, every number
    /// written with a comma or a point is the text it is, while a whole number and a date keep
    /// their type, and <c>cells read</c> says so in one NUMBERS_READ_AS_TEXT warning.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecimalCommaFile_KeepsEveryNumberWithASeparatorAsText(bool decimalCommaLast)
    {
        string[] rows = decimalCommaLast ? [GermanPrices[0], .. GermanPrices[2..], GermanPrices[1]] : GermanPrices;
        Write("prices.csv", rows);

        CliResult read = Cli("cells", "read", "prices.csv", "--output-format", "json");

        Assert.True(read.ExitCode == 0, $"exit {read.ExitCode}: {read.StdErr}{read.StdOut}");
        Assert.Equal(
            rows[1..].Select(static row => row.Split(';')[1] switch
            {
                "20" => "number 20",
                "2026-01-05" => "datetime 2026-01-05",
                var value => $"string {value}",
            }),
            read.Json()["sheet"]!["cells"]!.AsArray().Skip(1).Select(static row => $"{row![1]!["t"]} {row[1]!["v"]}"));
        Assert.Single(Warnings(read), static code => code == "NUMBERS_READ_AS_TEXT");
    }

    /// <summary>A decimal comma past the first 10,000 rows keeps the numbers with a separator before it as text.</summary>
    [Fact]
    public void DecimalCommaFile_IsReadToItsEnd()
    {
        Write("long.csv", ["Artikel;Preis", .. Enumerable.Range(1, 10_050).Select(static row => $"R{row};1,234"), "Z;2,71"]);

        CliResult read = Cli("cells", "read", "long.csv", "--range", "B2:B3", "--output-format", "json");

        Assert.True(read.ExitCode == 0, $"exit {read.ExitCode}: {read.StdErr}{read.StdOut}");
        Assert.Equal(["string 1,234", "string 1,234"],
            read.Json()["sheet"]!["cells"]!.AsArray().Select(static row => $"{row![0]!["t"]} {row[0]!["v"]}"));
        Assert.Contains("NUMBERS_READ_AS_TEXT", Warnings(read));
    }

    /// <summary>
    /// The load raises the warning, so every command that reads such a file carries it, and the
    /// workbook a conversion writes holds the numbers as the text cells it read.
    /// </summary>
    [Fact]
    public void DecimalCommaFile_EveryCommandThatReadsItWarns()
    {
        Write("prices.csv", GermanPrices);
        Write("copy.csv", GermanPrices);

        CliResult[] runs =
        [
            Cli("cells", "read", "prices.csv", "--output-format", "json"),
            Cli("cells", "inspect", "prices.csv", "--output-format", "json"),
            Cli("cells", "search", "prices.csv", "--pattern", "2,71", "--output-format", "json"),
            Cli("cells", "edit", "prices.csv", "--out", "edited.csv", "--output-format", "json",
                "--ops", """{"ops":[{"op":"set_values","range":"A2","values":[["X"]]}]}"""),
            Cli("cells", "convert", "prices.csv", "--to", "xlsx", "--out", "prices.xlsx", "--output-format", "json"),
            Cli("cells", "compare", "prices.csv", "copy.csv", "--output-format", "json"),
        ];

        Assert.All(runs, static run => Assert.True(run.ExitCode == 0, $"exit {run.ExitCode}: {run.StdErr}{run.StdOut}"));
        Assert.All(runs, static run => Assert.Contains("NUMBERS_READ_AS_TEXT", Warnings(run)));
        Assert.Equal(["string 2,71", "string 1,234", "string 0,5", "string 12,500"], Cells("prices.xlsx", "prices", "B2", "B3", "B4", "B5"));
    }

    /// <summary>
    /// Guardrail: a csv without a decimal comma reads its numbers with thousands separators as
    /// the numbers they show, without the warning.
    /// </summary>
    [Fact]
    public void FileWithoutADecimalComma_ReadsNumbersWithSeparatorsAsNumbers()
    {
        Write("us.csv", ["Region,Amount,Units", "East,\"1,234.50\",\"12,500\"", "West,980,\"2,000\""]);

        CliResult read = Cli("cells", "read", "us.csv", "--output-format", "json");

        Assert.True(read.ExitCode == 0, $"exit {read.ExitCode}: {read.StdErr}{read.StdOut}");
        Assert.Equal(["number 1234.5", "number 12500", "number 980", "number 2000"],
            read.Json()["sheet"]!["cells"]!.AsArray().Skip(1).SelectMany(static row => row!.AsArray().Skip(1)).Select(static cell => $"{cell!["t"]} {cell["v"]}"));
        Assert.DoesNotContain("NUMBERS_READ_AS_TEXT", Warnings(read));
    }

    /// <summary>The codes of a result's warnings.</summary>
    private static string[] Warnings(CliResult result) =>
        [.. (result.Json()["warnings"]?.AsArray() ?? []).Select(static warning => warning!["code"]!.GetValue<string>())];

    /// <summary>
    /// Writes a table of a header, one row per value and a total row, every row two fields wide,
    /// and returns its rows.
    /// </summary>
    private string[] Table(string name, char separator, string[] values)
    {
        string[] rows =
        [
            $"name{separator}value",
            .. values.Select((value, index) => $"r{index + 1:00}{separator}{(value.Contains(separator) ? $"\"{value}\"" : value)}"),
            $"total{separator}0",
        ];
        Write(name, rows);
        return rows;
    }

    /// <summary>Writes <paramref name="rows"/> as UTF-8 without a byte order mark, each ended by CRLF.</summary>
    private void Write(string name, string[] rows) =>
        File.WriteAllText(_workspace.File(name), string.Concat(rows.Select(static row => row + "\r\n")), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    /// <summary>Each cell, of a sheet of a workbook, as <c>cells read</c> reports its type and value.</summary>
    private string[] Cells(string workbook, string sheet, params string[] cells) =>
        [.. cells.Select(cell => Cli("cells", "read", workbook, "--sheet", sheet, "--range", cell, "--output-format", "json").Json()["sheet"]!["cells"]![0]![0]!)
            .Select(static cell => $"{cell["t"]} {cell["v"]}")];

    /// <summary>
    /// The rows of a written text table, each ended by CRLF, without the notice row evaluation
    /// mode writes last.
    /// </summary>
    private string[] Written(string name)
    {
        string text = File.ReadAllText(_workspace.File(name), Encoding.UTF8);
        int notice = text.LastIndexOf("\r\n", StringComparison.Ordinal) + 2;
        if (notice >= 2 && CellsEvaluation.IsNotice(text[notice..].TrimStart('"')))
        {
            text = text[..notice];
        }

        Assert.EndsWith("\r\n", text, StringComparison.Ordinal);
        return text[..^2].Split("\r\n");
    }

    /// <summary>
    /// Runs the CLI with the test license when the run has one (a workspace strips every other
    /// license source), and in evaluation mode otherwise.
    /// </summary>
    private CliResult Cli(params string[] args) =>
        _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);
}
