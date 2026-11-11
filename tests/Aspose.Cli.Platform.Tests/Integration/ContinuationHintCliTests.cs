using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The commands that an error's hint builds with <c>ContinuationCommand</c> (D14) run as written:
/// each value quoted as the shell needs it, and JSON asked for. A delimited text input carries no
/// password, so its import has no encrypted variant.
/// </summary>
public sealed partial class ContinuationHintCliTests : IDisposable
{
    private const string Variable = "HINT_TEMPLATE_PASSWORD";
    private readonly TempWorkspace _workspace = new();

    public ContinuationHintCliTests()
    {
        ScenarioLicense.Project(_workspace.Path);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A delimited text input that is not UTF-8 is refused with the import that reads it, which
    /// runs as written on a path with a space.
    /// </summary>
    [Fact]
    public void TextImportHint_RunsAsWrittenOnAPathWithASpace()
    {
        Directory.CreateDirectory(_workspace.File("erp exports"));
        string input = _workspace.File(Path.Combine("erp exports", "orders gbk.csv"));
        File.WriteAllBytes(input, Encoding.GetEncoding("GB18030").GetBytes("大区,金额\n华东一区,\"1,234.50\"\n"));

        JsonNode error = Error(_workspace.Run("cells", "read", input, "--output-format", "json"), "INPUT_ENCODING_INVALID");
        string command = Command(error["hint"]!.GetValue<string>());
        CliResult run = _workspace.RunCommandLine(command);

        Assert.True(run.ExitCode == 0, $"{command}{Environment.NewLine}{run.StdErr}");
        Assert.True(File.Exists(Path.ChangeExtension(input, ".xlsx")), command);
    }

    /// <summary>
    /// A delimited text input that writes decimal commas is read with its numbers as text and the
    /// NUMBERS_READ_AS_TEXT warning, whose import runs as written on a path with a space and reads
    /// them as the German numbers they are, without the warning: they add up as written.
    /// </summary>
    [Fact]
    public void DecimalCommaHint_RunsAsWrittenOnAPathWithASpace()
    {
        Directory.CreateDirectory(_workspace.File("erp exports"));
        string input = _workspace.File(Path.Combine("erp exports", "de preise.csv"));
        File.WriteAllText(input, "Artikel;Preis\r\nA;2,71\r\nB;1,234\r\nC;0,5\r\nD;12,500\r\n", new UTF8Encoding(false));

        CliResult read = _workspace.Run("cells", "read", input, "--output-format", "json");
        JsonNode warning = Assert.Single(read.Json()["warnings"]!.AsArray(), static warning => Code(warning!) == "NUMBERS_READ_AS_TEXT")!;
        string command = Command(warning["hint"]!.GetValue<string>());
        CliResult run = _workspace.RunCommandLine(command);
        string workbook = Path.ChangeExtension(input, ".xlsx");
        CliResult summed = _workspace.Run("cells", "edit", workbook, "--in-place", "--output-format", "json",
            "--ops", """{"ops":[{"op":"set_formula","sheet":"de preise","range":"B6","formula":"=SUM(B2:B5)"}]}""");
        CliResult cells = _workspace.Run("cells", "read", workbook, "--sheet", "de preise", "--range", "B2:B6", "--output-format", "json");

        Assert.True(run.ExitCode == 0, $"{command}{Environment.NewLine}{run.StdErr}");
        Assert.DoesNotContain(run.Json()["warnings"]?.AsArray() ?? [], static warning => Code(warning!) == "NUMBERS_READ_AS_TEXT");
        Assert.True(summed.ExitCode == 0, summed.StdErr);
        double[] values = [.. cells.Json()["sheet"]!["cells"]!.AsArray().Select(static row => row![0]!["v"]!.GetValue<double>())];
        Assert.Equal([2.71, 1.234, 0.5, 12.5], values[..4]);
        Assert.Equal(16.944, values[4], 9);
    }

    private static string Code(JsonNode warning) => warning["code"]!.GetValue<string>();

    /// <summary>
    /// On a plain path the import is the one the hint named before, now ending in
    /// <c>--output-format json</c>: the engine names the input by its full path, which is quoted.
    /// </summary>
    [Fact]
    public void TextImportHint_OnAPlainPath_EndsInJson()
    {
        string input = _workspace.File("orders.csv");
        File.WriteAllBytes(input, Encoding.GetEncoding("GB18030").GetBytes("大区\n华东\n"));

        JsonNode error = Error(_workspace.Run("cells", "read", "orders.csv", "--output-format", "json"), "INPUT_ENCODING_INVALID");

        Assert.Equal(
            $"Import it with its encoding: aspose-cli cells convert \"{input}\" --to xlsx --encoding gb18030 --output-format json "
                + "(Chinese Windows and ERP exports; use big5, shift_jis, windows-1252 or another name for other sources).",
            error["hint"]!.GetValue<string>());
    }

    /// <summary>
    /// An encrypted template is refused with the conversion that writes an unencrypted copy: with
    /// its placeholder naming the variable that holds the password, it runs as written on a path
    /// with a space, and the copy it writes serves as the template.
    /// </summary>
    [Fact]
    public void EncryptedTemplateHint_RunsAsWrittenWithThePasswordVariable()
    {
        Directory.CreateDirectory(_workspace.File("brand kit"));
        string template = _workspace.File(Path.Combine("brand kit", "letter template.docx"));
        File.WriteAllBytes(template, ScenarioFixtures.Read("words.encrypted"));
        var variables = new Dictionary<string, string?> { [Variable] = ScenarioFixtures.Password };

        JsonNode error = Error(_workspace.Run("words", "create", "letter.docx", "--template", template, "--output-format", "json"), code: null);
        string command = Command(error["hint"]!.GetValue<string>());
        Assert.Contains("--password-env <password-env> ", command, StringComparison.Ordinal);

        CliResult run = _workspace.RunCommandLine(command.Replace("<password-env>", Variable, StringComparison.Ordinal), variables);

        Assert.True(run.ExitCode == 0, $"{command}{Environment.NewLine}{run.StdErr}");
        string copy = _workspace.File(Path.Combine("brand kit", "letter template.unencrypted.xml"));
        Assert.True(File.Exists(copy), command);
        CliResult created = _workspace.Run("words", "create", "letter.docx", "--template", copy, "--output-format", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
    }

    /// <summary>
    /// <c>app open</c> given a file and <c>--welcome</c> names the command that opens the file, with
    /// the file as one argument; the App itself is not started here, so the catalog checks the command.
    /// </summary>
    [Fact]
    public void AppOpenWelcomeHint_NamesTheFileAsOneArgument()
    {
        File.WriteAllText(_workspace.File("my book.xlsx"), "not opened");

        JsonNode error = Error(_workspace.Run("app", "open", "my book.xlsx", "--welcome", "--no-open", "--output-format", "json"), "OPTION_INVALID");

        Assert.Equal(
            "Run 'aspose-cli app open \"my book.xlsx\" --output-format json' to open the file, or 'aspose-cli app open --welcome' for the guide.",
            error["hint"]!.GetValue<string>());
    }

    private static JsonNode Error(CliResult result, string? code)
    {
        Assert.NotEqual(0, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        if (code is not null)
        {
            Assert.Equal(code, error["code"]!.GetValue<string>());
        }
        return error;
    }

    /// <summary>The command a hint names, from <c>aspose-cli</c> to the <c>--output-format json</c> it ends with.</summary>
    private static string Command(string hint)
    {
        Match command = CommandPattern().Match(hint);
        Assert.True(command.Success, hint);
        return command.Value;
    }

    [GeneratedRegex("aspose-cli .*? --output-format json")]
    private static partial Regex CommandPattern();
}
