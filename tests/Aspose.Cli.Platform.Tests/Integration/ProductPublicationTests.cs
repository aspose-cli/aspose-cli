using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class ProductPublicationTests
{
    public static TheoryData<string, string, string[], string> EditCases => new()
    {
        { "words", "docx", ["--from-text", "source.txt"], """{"ops":[{"op":"set_text","target":{"find":"Anchor"},"text":"Edited"}]}""" },
        { "slides", "pptx", ["--from-markdown", "source.md"], """{"ops":[{"op":"set_title","slide":1,"text":"Edited"}]}""" },
        { "pdf", "pdf", ["--from-text", "source.txt"], """{"ops":[{"op":"set_properties","title":"Edited"}]}""" },
    };

    [Category(TestCategory.Slow)]
    [Theory]
    [MemberData(nameof(EditCases))]
    public void EditPublishesTheCandidate(string product, string extension,
        string[] sourceOptions, string ops)
    {
        using var workspace = new TempWorkspace();
        string input = CreateInput(workspace, product, extension, sourceOptions);
        string output = workspace.File("edited." + extension);
        CliResult edited = workspace.Run([product, "edit", input, "--ops", ops, "--out", output,
            "--output-format", "json", "--max-input-bytes", new FileInfo(input).Length.ToString()]);
        edited.Succeeded();
        Assert.True(File.Exists(output));
        Assert.Equal([output], Directory.GetFiles(workspace.File("."), "edited.*").Select(Path.GetFullPath));
        AssertDescribesThePublishedFile(JsonNode.Parse(edited.StdOut)!["output"]!);
    }

    /// <summary>
    /// An in-place edit whose engine still holds its input open when the edit commits leaves no
    /// staging beside the file.
    /// </summary>
    [Theory]
    [InlineData("slides", "pptx", "--from-markdown", "source.md", """{"ops":[{"op":"set_title","slide":1,"text":"Edited"}]}""")]
    [InlineData("pdf", "pdf", "--from-text", "source.txt", """{"ops":[{"op":"set_properties","title":"Edited"}]}""")]
    public void InPlaceEdit_WhoseEngineHoldsItsInput_LeavesNoStaging(string product, string extension,
        string sourceOption, string source, string ops)
    {
        using var workspace = new TempWorkspace();
        string input = CreateInput(workspace, product, extension, [sourceOption, source]);

        workspace.Run([product, "edit", input, "--ops", ops, "--in-place", "--output-format", "json"]).Succeeded();

        Assert.Empty(Directory.EnumerateDirectories(workspace.Path, ".aspose-publication-*"));
    }

    /// <summary>
    /// The reported size is that of the published files, for one written file, a page per file
    /// and a deck with its companion files.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void ReportedOutputsDescribeThePublishedFiles()
    {
        using var workspace = new TempWorkspace();
        string document = CreateInput(workspace, "words", "docx", ["--from-text", "source.txt"]);
        string deck = CreateInput(workspace, "slides", "pptx", ["--from-markdown", "source.md"]);

        CliResult converted = workspace.Run(["words", "convert", document, "--to", "pdf", "--out", workspace.File("converted.pdf"),
            "--output-format", "json"]);
        CliResult pages = workspace.Run(["pdf", "render", workspace.File("converted.pdf"), "--out", workspace.File("page.png"),
            "--output-format", "json"]);
        CliResult site = workspace.Run(["slides", "convert", deck, "--to", "html5", "--out", workspace.File(Path.Combine("site", "deck.html")),
            "--output-format", "json"]);

        converted.Succeeded();
        pages.Succeeded();
        site.Succeeded();
        AssertDescribesThePublishedFile(Assert.Single(JsonNode.Parse(converted.StdOut)!["outputs"]!.AsArray())!);
        Assert.All(JsonNode.Parse(pages.StdOut)!["outputs"]!.AsArray(), static output => AssertDescribesThePublishedFile(output!));
        JsonArray files = JsonNode.Parse(site.StdOut)!["outputs"]!.AsArray();
        Assert.True(files.Count > 1, "The HTML5 deck is published with its companion files.");
        Assert.All(files, static output => AssertDescribesThePublishedFile(output!));
    }

    /// <summary>Checks that a reported file object states the path and size of the file on disk.</summary>
    private static void AssertDescribesThePublishedFile(JsonNode file)
    {
        string path = file["path"]!.GetValue<string>();
        Assert.True(File.Exists(path), path);
        Assert.Equal(new FileInfo(path).Length, file["sizeBytes"]!.GetValue<long>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void WordsVerificationReadsTheCandidate()
    {
        using var workspace = new TempWorkspace();
        string input = CreateInput(workspace, "words", "docx", ["--from-text", "source.txt"]);
        CliResult edited = workspace.Run(["words", "edit", input,
            "--ops", """{"ops":[{"op":"set_text","target":{"find":"Anchor"},"text":"Edited"}]}""",
            "--out", workspace.File("edited.docx"), "--verify", "--output-format", "json"]);
        edited.Succeeded();
        JsonNode verification = JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.True(verification["semanticChangesDetected"]!.GetValue<bool>());
    }

    [Fact]
    public void AllSheetsFailureDoesNotLeaveEarlierOutputFiles()
    {
        using var workspace = new TempWorkspace();
        workspace.Run("cells", "create", "input.xlsx", "--sheet-names", "First,Second").Succeeded();
        workspace.Run("cells", "edit", "input.xlsx", "--set", "First!A1=1", "--set", "Second!A1=2", "--in-place").Succeeded();
        File.WriteAllText(workspace.File("render.Second.png"), "existing second sheet");
        CliResult result = workspace.Run(["cells", "render", "input.xlsx", "--sheets", "all", "--out", "render.png", "--output-format", "json"]);
        Assert.Equal(5, result.ExitCode);
        Assert.False(File.Exists(workspace.File("render.First.png")));
        Assert.Equal("existing second sheet", File.ReadAllText(workspace.File("render.Second.png")));
    }

    [Fact]
    public void SignVerifiesTheCandidate()
    {
        const string certificatePassword = "publication-test-certificate";
        using var workspace = new TempWorkspace();
        string input = CreateInput(workspace, "pdf", "pdf", ["--from-text", "source.txt"]);
        string output = workspace.File("signed.pdf");

        CliResult signed = workspace.RunWithEnv(
            new Dictionary<string, string?> { ["ASPOSE_CLI_TEST_CERT_PASSWORD"] = certificatePassword },
            ["pdf", "sign", input,
             "--certificate", CreateCertificate(workspace, certificatePassword),
             "--certificate-password-env", "ASPOSE_CLI_TEST_CERT_PASSWORD",
             "--out", output, "--output-format", "json"]);

        signed.Succeeded();
        Assert.True(File.Exists(output));
        JsonNode signature = JsonNode.Parse(signed.StdOut)!["signature"]!;
        Assert.True(signature["signed"]!.GetValue<bool>());
        Assert.True(signature["intact"]!.GetValue<bool>());
    }

    private static string CreateCertificate(TempWorkspace workspace, string password)
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Aspose CLI Publication Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        string path = workspace.File("signing.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
        return path;
    }

    private static string CreateInput(TempWorkspace workspace, string product, string extension, string[] sourceOptions)
    {
        File.WriteAllText(workspace.File("source.txt"), "Anchor");
        File.WriteAllText(workspace.File("source.md"), "# Anchor\n\nBody");
        string input = workspace.File("input." + extension);
        workspace.Run([product, "create", input, .. sourceOptions]).Succeeded();
        return input;
    }
}
