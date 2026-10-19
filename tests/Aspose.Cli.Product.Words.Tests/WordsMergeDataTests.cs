using System.Text.Json;
using Aspose.Cli.Product.Words.Engine.Editing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Merge data as mail_merge and repeat_table_row read it from CSV, JSON and inline rows.</summary>
public sealed class WordsMergeDataTests
{
    public static TheoryData<string, string, string[][]> CsvCases() => new()
    {
        { "empty text", "", [] },
        { "one record without a line break", "a,b", [["a", "b"]] },
        { "LF records", "a,b\nc,d\n", [["a", "b"], ["c", "d"]] },
        { "CRLF records", "a,b\r\nc,d\r\n", [["a", "b"], ["c", "d"]] },
        { "lone CR ends a record", "a\rb", [["a"], ["b"]] },
        { "blank lines are skipped", "\n\na\r\n\r\n\nb\n\n", [["a"], ["b"]] },
        { "a whitespace-only line is skipped", "a\n   \nb", [["a"], ["b"]] },
        { "unquoted fields are trimmed", "  a , b\t\n", [["a", "b"]] },
        { "quoted field keeps a comma", "\"Doe, Jane\",x", [["Doe, Jane", "x"]] },
        { "doubled quote is one quote", "\"He said \"\"hi\"\"\"", [["He said \"hi\""]] },
        { "quoted field keeps CRLF and LF", "\"a\r\nb\nc\",d", [["a\r\nb\nc", "d"]] },
        { "quoted field is not trimmed", "\"  a  \",b", [["  a  ", "b"]] },
        { "spaces before an opening quote are dropped", "   \"a\",b", [["a", "b"]] },
        { "text after a closing quote joins the field untrimmed", "\"a\"b ,c", [["ab ", "c"]] },
        { "a quote inside an unquoted field is literal", "a\"b,c", [["a\"b", "c"]] },
        { "a trailing comma adds an empty field", "a,\n", [["a", ""]] },
        { "a line of commas is a record of empty fields", ",\n", [["", ""]] },
        { "an empty quoted field alone is a blank line", "\"\"\na", [["a"]] },
        { "an empty quoted field beside another is kept", "\"\",a", [["", "a"]] },
        { "ragged records keep their own width", "a,b,c\nd\n", [["a", "b", "c"], ["d"]] },
        {
            "RFC 4180 mix",
            "Name, City\r\n\"Doe, Jane\",\"He said \"\"hi\"\"\"\n\n\"Two\nlines\", Oslo \r\n",
            [["Name", "City"], ["Doe, Jane", "He said \"hi\""], ["Two\nlines", "Oslo"]]
        },
    };

    [Theory]
    [MemberData(nameof(CsvCases))]
    public void Csv_ReadsRecords(string name, string text, string[][] expected)
    {
        IReadOnlyList<string[]> records = WordsMergeData.ReadCsv(text);

        Assert.True(expected.Length == records.Count, $"{name}: {records.Count} records");
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], records[index]);
        }
    }

    [Theory]
    [InlineData("\"open")]
    [InlineData("a,b\n\"c,d\n")]
    [InlineData("\"a\"\"")]
    public void Csv_EndingInsideAQuotedField_IsInvalid(string text)
    {
        CliException error = Assert.Throws<CliException>(() => WordsMergeData.ReadCsv(text));

        Assert.Equal(WordsDiagnostics.MergeDataInvalid, error.Code);
        Assert.Contains("inside a quoted field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvFile_MapsCellsToHeaderNames_WithEmptyAndMissingCellsAsNull()
    {
        using var temp = new TempDirectory();
        string path = Write(temp, "rows.csv", "Name,City,Zip\nAva,,1\nBo\nCy,Oslo,2,extra\n");

        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = WordsMergeData.Read(path, Inputs());

        Assert.Equal(3, rows.Count);
        Assert.Equal(Row(("Name", "Ava"), ("City", null), ("Zip", "1")), rows[0]);
        Assert.Equal(Row(("Name", "Bo"), ("City", null), ("Zip", null)), rows[1]);
        Assert.Equal(Row(("Name", "Cy"), ("City", "Oslo"), ("Zip", "2")), rows[2]);
    }

    [Fact]
    public void CsvFile_ExtensionIsCaseInsensitive_AndAHeaderAloneIsNoRows()
    {
        using var temp = new TempDirectory();
        string path = Write(temp, "rows.CSV", "Name,City\r\n");

        Assert.Empty(WordsMergeData.Read(path, Inputs()));
    }

    [Theory]
    [InlineData("", "no header row")]
    [InlineData("\n\n", "no header row")]
    [InlineData("Name,City,Name\nA,B,C\n", "'Name' more than once")]
    [InlineData("Name,\"x\nopen", "inside a quoted field")]
    public void CsvFile_WithABadHeaderOrQuote_IsInvalid(string text, string reason)
    {
        using var temp = new TempDirectory();
        string path = Write(temp, "rows.csv", text);

        CliException error = Assert.Throws<CliException>(() => WordsMergeData.Read(path, Inputs()));

        Assert.Equal(WordsDiagnostics.MergeDataInvalid, error.Code);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvFile_HeaderNamesDifferingOnlyInCase_AreDistinctColumns()
    {
        using var temp = new TempDirectory();
        string path = Write(temp, "rows.csv", "name,Name\na,b\n");

        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = WordsMergeData.Read(path, Inputs());

        Assert.Equal(Row(("name", "a"), ("Name", "b")), Assert.Single(rows));
    }

    [Fact]
    public void JsonFile_ReadsScalarsAsTheirJsonText()
    {
        using var temp = new TempDirectory();
        string path = Write(temp, "rows.json", """[{"s":"x","n":1.50,"t":true,"f":false,"z":null},{}]""");

        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = WordsMergeData.Read(path, Inputs());

        Assert.Equal(2, rows.Count);
        Assert.Equal(Row(("s", "x"), ("n", "1.50"), ("t", "true"), ("f", "false"), ("z", null)), rows[0]);
        Assert.Empty(rows[1]);
    }

    [Theory]
    [InlineData("""{"a":1}""", "must be an array")]
    [InlineData("""[1]""", "must be an object")]
    [InlineData("""[{"a":1,"a":2}]""", "'a' more than once")]
    [InlineData("""[{"a":[1]}]""", "string, number, Boolean or null")]
    [InlineData("""[{"a":{"b":1}}]""", "string, number, Boolean or null")]
    public void JsonFile_WithTheWrongShape_IsInvalid(string json, string reason)
    {
        using var temp = new TempDirectory();
        string path = Write(temp, "rows.json", json);

        CliException error = Assert.Throws<CliException>(() => WordsMergeData.Read(path, Inputs()));

        Assert.Equal(WordsDiagnostics.MergeDataInvalid, error.Code);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonFile_WithBadSyntax_IsInvalidWithASyntaxHint()
    {
        using var temp = new TempDirectory();
        string path = Write(temp, "rows.json", "[{\"a\":");

        CliException error = Assert.Throws<CliException>(() => WordsMergeData.Read(path, Inputs()));

        Assert.Equal(WordsDiagnostics.MergeDataInvalid, error.Code);
        Assert.Contains("JSON syntax", error.Hint!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rows.csv")]
    [InlineData("rows.json")]
    public void MissingFile_IsFileNotFound(string name)
    {
        using var temp = new TempDirectory();

        CliException error = Assert.Throws<CliException>(
            () => WordsMergeData.Read(temp.File(name), Inputs()));

        Assert.Equal(ErrorCodes.FileNotFound, error.Code);
    }

    [Theory]
    [InlineData("rows.csv", "Name\nAva\nBo\nCy\n")]
    [InlineData("rows.json", """[{"Name":"Ava"},{"Name":"Bo"}]""")]
    public void DataFile_BeyondTheDecodedTextBudget_IsRejected(string name, string text)
    {
        using var temp = new TempDirectory();
        string path = Write(temp, name, text);

        CliException error = Assert.Throws<CliException>(() => WordsMergeData.Read(
            path,
            Inputs((ResourceBudgetKinds.DecodedTextCharacters, text.Length - 1L))));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, error.Code);
    }

    [Fact]
    public void DataFile_LargerThanTheInputBudget_IsRejected()
    {
        using var temp = new TempDirectory();
        string text = "Name\nAva\n";
        string path = Write(temp, "rows.csv", text);

        CliException error = Assert.Throws<CliException>(() => WordsMergeData.Read(
            path,
            Inputs((ResourceBudgetKinds.InputBytes, text.Length - 1L))));

        Assert.Equal(ErrorCodes.FileTooLarge, error.Code);
    }

    [Fact]
    public void InlineRows_MergeAsTheirJsonText()
    {
        using JsonDocument json = JsonDocument.Parse("""{"e":2.0}""");
        IReadOnlyDictionary<string, object?>[] inline =
        [
            new Dictionary<string, object?>
            {
                ["s"] = "x",
                ["t"] = true,
                ["f"] = false,
                ["i"] = 3,
                ["d"] = 1.5m,
                ["z"] = null,
                ["e"] = json.RootElement.GetProperty("e"),
            },
        ];

        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = WordsMergeData.Inline(inline);

        Assert.Equal(
            Row(("s", "x"), ("t", "true"), ("f", "false"), ("i", "3"), ("d", "1.5"), ("z", null), ("e", "2.0")),
            Assert.Single(rows));
    }

    [Fact]
    public void InlineRows_WithANestedValue_AreInvalid()
    {
        using JsonDocument json = JsonDocument.Parse("""{"a":[1]}""");
        IReadOnlyDictionary<string, object?>[] inline =
            [new Dictionary<string, object?> { ["a"] = json.RootElement.GetProperty("a") }];

        CliException error = Assert.Throws<CliException>(() => WordsMergeData.Inline(inline));

        Assert.Equal(WordsDiagnostics.MergeDataInvalid, error.Code);
    }

    private static string Write(TempDirectory temp, string name, string text)
    {
        string path = temp.File(name);
        File.WriteAllText(path, text);
        return path;
    }

    private static InputSource Inputs(params (string Kind, long Limit)[] overrides)
    {
        var limits = WordsModule.Manifest.ResourceBudgets.ToDictionary(static item => item.Kind, static item => item.Default);
        foreach ((string kind, long limit) in overrides)
        {
            limits[kind] = limit;
        }

        return new ResourceBudgetLedger(OperationDeadline.Start(null), limits).Inputs;
    }

    private static Dictionary<string, string?> Row(params (string Key, string? Value)[] cells) =>
        cells.ToDictionary(static cell => cell.Key, static cell => cell.Value, StringComparer.Ordinal);
}
