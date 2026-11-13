using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// An ops document and a text file a command reads are refused with <c>INPUT_ENCODING_INVALID</c>
/// when they hold bytes that are not UTF-8 after a UTF-8 byte order mark, as without one, before
/// anything is written, and are read in the encoding a UTF-8 or UTF-16 mark names. The tests write
/// the bytes themselves.
/// </summary>
public sealed class TextInputEncodingCliTests
{
    private const string Before = """{"ops":[{"op":"set_values","sheet":"Sheet1","range":"A1","values":[["a""";
    private const string After = """b"]]}]}""";
    private static readonly byte[] Mark = [0xEF, 0xBB, 0xBF];
    // The first character of the Chinese for "password" in GBK: not UTF-8.
    private static readonly byte[] NotUtf8 = [0xC3, 0xDC];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpsAfterAMark_ThatAreNotUtf8_AreRefusedBeforeAnythingIsWritten(bool standardInput)
    {
        using var workspace = Workspace();
        byte[] ops = [.. Mark, .. Encoding.UTF8.GetBytes(Before), .. NotUtf8, .. Encoding.UTF8.GetBytes(After)];
        File.WriteAllBytes(workspace.File("ops.json"), ops);
        string[] args = ["cells", "edit", "book.xlsx", "--ops", standardInput ? "-" : "ops.json", "--out", "out.xlsx", "--output-format", "json"];

        CliResult refused = standardInput ? Piped(workspace, ops, args) : workspace.Run(args);

        Refused(refused, standardInput ? "stdin-decode" : "text-file-decode");
        Assert.False(File.Exists(workspace.File("out.xlsx")));
    }

    [Theory]
    [InlineData("words", "out.docx", "--from-text", "text.txt", "Hello ")]
    [InlineData("slides", "out.pptx", "--from-markdown", "outline.md", "# Hello ")]
    public void TextFileAfterAMark_ThatIsNotUtf8_IsRefusedBeforeAnythingIsWritten(string product, string output, string option, string file, string text)
    {
        using var workspace = Workspace();
        File.WriteAllBytes(workspace.File(file), [.. Mark, .. Encoding.UTF8.GetBytes(text), .. NotUtf8, 0x0A]);

        Refused(workspace.Run(product, "create", output, option, file, "--output-format", "json"), "text-file-decode");
        Assert.False(File.Exists(workspace.File(output)));
    }

    /// <summary>An ops document after a UTF-8 or UTF-16 byte order mark is read as its text, without the mark.</summary>
    [Theory]
    [InlineData("utf-8", false)]
    [InlineData("utf-8", true)]
    [InlineData("utf-16le", false)]
    [InlineData("utf-16be", false)]
    public void OpsAfterAMark_AreTheirText(string encoding, bool standardInput)
    {
        using var workspace = Workspace();
        Encoding marked = encoding switch
        {
            "utf-8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            "utf-16le" => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            _ => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
        };
        byte[] ops = [.. marked.GetPreamble(), .. marked.GetBytes(Before + "汉" + After)];
        File.WriteAllBytes(workspace.File("ops.json"), ops);
        string[] args = ["cells", "edit", "book.xlsx", "--ops", standardInput ? "-" : "ops.json", "--out", "out.xlsx", "--output-format", "json"];

        CliResult edited = standardInput ? Piped(workspace, ops, args) : workspace.Run(args);

        Assert.True(edited.ExitCode == 0, edited.StdOut + edited.StdErr);
        JsonNode read = workspace.Run("cells", "read", "out.xlsx", "--range", "A1", "--output-format", "json").Json();
        Assert.Equal("a汉b", read["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<string>());
    }

    private static TempWorkspace Workspace()
    {
        var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        workspace.Run("cells", "create", "book.xlsx", "--output-format", "json").Succeeded();
        return workspace;
    }

    private static void Refused(CliResult result, string phase)
    {
        Assert.True(result.ExitCode == 3, result.StdOut + result.StdErr);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal(
            ("INPUT_ENCODING_INVALID", phase),
            (error["code"]!.GetValue<string>(), error["details"]!["phase"]!.GetValue<string>()));
    }

    /// <summary>Runs the CLI with exactly <paramref name="input"/> on stdin.</summary>
    private static CliResult Piped(TempWorkspace workspace, byte[] input, string[] args)
    {
        ProcessStartInfo start = workspace.StartInfo();
        start.RedirectStandardInput = start.RedirectStandardOutput = start.RedirectStandardError = true;
        start.StandardOutputEncoding = start.StandardErrorEncoding = Encoding.UTF8;
        foreach (string argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.BaseStream.Write(input);
        process.StandardInput.Close();
        Assert.True(process.WaitForExit(120_000), string.Join(' ', args));
        return new CliResult(process.ExitCode, stdout.Result, stderr.Result);
    }
}
