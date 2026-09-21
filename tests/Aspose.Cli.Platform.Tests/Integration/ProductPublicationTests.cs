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
        { "words", "docx", ["--text", "source.txt"], """{"ops":[{"op":"set_text","at":{"find":"Anchor"},"text":"Edited"}]}""" },
        { "slides", "pptx", ["--from-markdown", "source.md"], """{"ops":[{"op":"set_title","slide":1,"text":"Edited"}]}""" },
        { "pdf", "pdf", ["--from-text", "source.txt"], """{"ops":[{"op":"set_metadata","title":"Edited"}]}""" },
    };

    [Theory]
    [MemberData(nameof(EditCases))]
    public void EditPublishesTheCandidateInBothExecutionModes(string product, string extension,
        string[] sourceOptions, string ops)
    {
        foreach (bool supervised in new[] { false, true })
        {
            using var workspace = new TempWorkspace();
            string input = CreateInput(workspace, product, extension, sourceOptions);
            string output = workspace.File("edited." + extension);
            CliResult edited = workspace.Run([product, "edit", input, "--ops", ops, "--out", output,
                "--output", "json", "--max-input-bytes", new FileInfo(input).Length.ToString(),
                .. (supervised ? new[] { "--timeout", "30" } : Array.Empty<string>())]);
            AssertSuccess(edited);
            Assert.True(File.Exists(output));
            Assert.Equal([output], Directory.GetFiles(workspace.File("."), "edited.*").Select(Path.GetFullPath));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WordsVerificationReadsTheCandidateInBothExecutionModes(bool supervised)
    {
        using var workspace = new TempWorkspace();
        string input = CreateInput(workspace, "words", "docx", ["--text", "source.txt"]);
        CliResult edited = workspace.Run(["words", "edit", input,
            "--ops", """{"ops":[{"op":"set_text","at":{"find":"Anchor"},"text":"Edited"}]}""",
            "--out", workspace.File("edited.docx"), "--verify", "--output", "json",
            .. (supervised ? new[] { "--timeout", "30" } : Array.Empty<string>())]);
        AssertSuccess(edited);
        JsonNode verification = JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.True(verification["semanticChangesDetected"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllSheetsFailureDoesNotLeaveEarlierOutputFiles(bool supervised)
    {
        using var workspace = new TempWorkspace();
        AssertSuccess(workspace.Run("cells", "create", "input.xlsx", "--sheets", "First,Second"));
        AssertSuccess(workspace.Run("cells", "edit", "input.xlsx", "--set", "First!A1=1", "--set", "Second!A1=2", "--in-place"));
        File.WriteAllText(workspace.File("render.Second.png"), "existing second sheet");
        CliResult result = workspace.Run(["cells", "render", "input.xlsx", "--all-sheets", "--out", "render.png", "--output", "json",
            .. (supervised ? new[] { "--timeout", "30" } : Array.Empty<string>())]);
        Assert.Equal(5, result.ExitCode);
        Assert.False(File.Exists(workspace.File("render.First.png")));
        Assert.Equal("existing second sheet", File.ReadAllText(workspace.File("render.Second.png")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SignVerifiesTheCandidateInBothExecutionModes(bool supervised)
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
             "--out", output, "--output", "json",
             .. (supervised ? new[] { "--timeout", "60" } : Array.Empty<string>())]);

        AssertSuccess(signed);
        Assert.True(File.Exists(output));
        JsonNode signature = JsonNode.Parse(signed.StdOut)!["signature"]!;
        Assert.True(signature["signed"]!.GetValue<bool>());
        Assert.True(signature["valid"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SkillInstallPublishesItsTreeInBothExecutionModes(bool supervised)
    {
        using var workspace = new TempWorkspace();
        string target = workspace.File("skills");

        CliResult installed = workspace.Run(["skill", "install", "aspose-cli-pdf",
            "--target", target, "--output", "json",
            .. (supervised ? new[] { "--timeout", "60" } : Array.Empty<string>())]);

        AssertSuccess(installed);
        string tree = Path.Combine(target, "aspose-cli-pdf");
        Assert.True(File.Exists(Path.Combine(tree, "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(tree, ".aspose-skill-manifest.json")));
        Assert.Equal(
            JsonNode.Parse(installed.StdOut)!["files"]!.GetValue<int>(),
            Directory.GetFiles(tree, "*", SearchOption.AllDirectories).Length);
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
        AssertSuccess(workspace.Run([product, "create", input, .. sourceOptions]));
        return input;
    }

    private static void AssertSuccess(CliResult result) => Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
}
