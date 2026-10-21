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
    public void EditPublishesTheCandidateInBothExecutionModes(string product, string extension,
        string[] sourceOptions, string ops)
    {
        foreach (bool supervised in new[] { false, true })
        {
            using var workspace = new TempWorkspace();
            string input = CreateInput(workspace, product, extension, sourceOptions);
            string output = workspace.File("edited." + extension);
            CliResult edited = workspace.Run([product, "edit", input, "--ops", ops, "--out", output,
                "--output-format", "json", "--max-input-bytes", new FileInfo(input).Length.ToString(),
                .. (supervised ? new[] { "--timeout", TestDeadline.CliTimeout } : Array.Empty<string>())]);
            edited.Succeeded();
            Assert.True(File.Exists(output));
            Assert.Equal([output], Directory.GetFiles(workspace.File("."), "edited.*").Select(Path.GetFullPath));
            AssertDescribesThePublishedFile(JsonNode.Parse(edited.StdOut)!["output"]!);
        }
    }

    /// <summary>
    /// A supervised worker hands its outputs to the parent, which publishes them after the worker
    /// has reported them; the reported size and fingerprint are still those of the published files,
    /// for one written file, a page per file and a deck with its companion files.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReportedOutputsDescribeThePublishedFilesInBothExecutionModes(bool supervised)
    {
        using var workspace = new TempWorkspace();
        string[] timeout = supervised ? ["--timeout", TestDeadline.CliTimeout] : [];
        string document = CreateInput(workspace, "words", "docx", ["--from-text", "source.txt"]);
        string deck = CreateInput(workspace, "slides", "pptx", ["--from-markdown", "source.md"]);

        CliResult converted = workspace.Run(["words", "convert", document, "--to", "pdf", "--out", workspace.File("converted.pdf"),
            "--output-format", "json", .. timeout]);
        CliResult pages = workspace.Run(["pdf", "render", workspace.File("converted.pdf"), "--out", workspace.File("page.png"),
            "--output-format", "json", .. timeout]);
        CliResult site = workspace.Run(["slides", "convert", deck, "--to", "html5", "--out", workspace.File(Path.Combine("site", "deck.html")),
            "--output-format", "json", .. timeout]);

        converted.Succeeded();
        pages.Succeeded();
        site.Succeeded();
        AssertDescribesThePublishedFile(Assert.Single(JsonNode.Parse(converted.StdOut)!["outputs"]!.AsArray())!);
        Assert.All(JsonNode.Parse(pages.StdOut)!["outputs"]!.AsArray(), static output => AssertDescribesThePublishedFile(output!));
        JsonArray files = JsonNode.Parse(site.StdOut)!["outputs"]!.AsArray();
        Assert.True(files.Count > 1, "The HTML5 deck is published with its companion files.");
        Assert.All(files, static output => AssertDescribesThePublishedFile(output!));
    }

    /// <summary>Checks that a reported file object states the path, size and SHA-256 of the file on disk.</summary>
    private static void AssertDescribesThePublishedFile(JsonNode file)
    {
        string path = file["path"]!.GetValue<string>();
        Assert.True(File.Exists(path), path);
        Assert.Equal(new FileInfo(path).Length, file["sizeBytes"]!.GetValue<long>());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
            file["fingerprint"]!["sha256"]!.GetValue<string>());
    }

    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WordsVerificationReadsTheCandidateInBothExecutionModes(bool supervised)
    {
        using var workspace = new TempWorkspace();
        string input = CreateInput(workspace, "words", "docx", ["--from-text", "source.txt"]);
        CliResult edited = workspace.Run(["words", "edit", input,
            "--ops", """{"ops":[{"op":"set_text","target":{"find":"Anchor"},"text":"Edited"}]}""",
            "--out", workspace.File("edited.docx"), "--verify", "--output-format", "json",
            .. (supervised ? new[] { "--timeout", TestDeadline.CliTimeout } : Array.Empty<string>())]);
        edited.Succeeded();
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
        workspace.Run("cells", "create", "input.xlsx", "--sheet-names", "First,Second").Succeeded();
        workspace.Run("cells", "edit", "input.xlsx", "--set", "First!A1=1", "--set", "Second!A1=2", "--in-place").Succeeded();
        File.WriteAllText(workspace.File("render.Second.png"), "existing second sheet");
        CliResult result = workspace.Run(["cells", "render", "input.xlsx", "--sheets", "all", "--out", "render.png", "--output-format", "json",
            .. (supervised ? new[] { "--timeout", TestDeadline.CliTimeout } : Array.Empty<string>())]);
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
             "--out", output, "--output-format", "json",
             .. (supervised ? new[] { "--timeout", TestDeadline.CliTimeout } : Array.Empty<string>())]);

        signed.Succeeded();
        Assert.True(File.Exists(output));
        JsonNode signature = JsonNode.Parse(signed.StdOut)!["signature"]!;
        Assert.True(signature["signed"]!.GetValue<bool>());
        Assert.True(signature["intact"]!.GetValue<bool>());
    }

    // SkillInstallTests cover the install of every Skill without a timeout.
    [Fact]
    public void SkillInstallPublishesItsTreeUnderATimeout()
    {
        using var workspace = new TempWorkspace();
        string target = workspace.File("skills");

        CliResult installed = workspace.Run(["skill", "install", "aspose-cli-pdf",
            "--target", target, "--output-format", "json", "--timeout", TestDeadline.CliTimeout]);

        installed.Succeeded();
        string tree = Path.Combine(target, "aspose-cli-pdf");
        Assert.True(File.Exists(Path.Combine(tree, "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(tree, ".aspose-skill-manifest.json")));
        Assert.Equal(
            JsonNode.Parse(installed.StdOut)!["fileCount"]!.GetValue<int>(),
            Directory.GetFiles(tree, "*", SearchOption.AllDirectories).Length);
    }

    // A CLI killed with its worker leaves the worker's private staging, which can hold decrypted
    // copies, in the user's temporary directory; the next supervised command removes it.
    [Fact]
    public void ATimeoutCommandRemovesOnlyTheWorkerRootsOfOwnersThatHaveExited()
    {
        using var workspace = new TempWorkspace();
        string workers = Directory.CreateDirectory(
            Path.Combine(Aspose.Cli.Sdk.IO.Paths.UserStorage.TemporaryRoot(), "worker")).FullName;
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        long started = self.StartTime.ToUniversalTime().Ticks;
        // The owner's identity is its process id and start time, so a start time this process
        // never had names an owner that has exited.
        string exited = Root(workers, $"{Environment.ProcessId}-1-{Guid.NewGuid():N}");
        string live = Root(workers, $"{Environment.ProcessId}-{started}-{Guid.NewGuid():N}");
        string other = Root(workers, $"not-a-worker-{Guid.NewGuid():N}");
        try
        {
            workspace.Run(["skill", "install", "aspose-cli-pdf", "--target", workspace.File("skills"),
                "--output-format", "json", "--timeout", TestDeadline.CliTimeout]).Succeeded();

            Assert.False(Directory.Exists(exited));
            Assert.True(File.Exists(Path.Combine(live, "copy.bin")));
            Assert.True(File.Exists(Path.Combine(other, "copy.bin")));
        }
        finally
        {
            foreach (string root in new[] { exited, live, other })
            {
                if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
            }
        }

        static string Root(string workers, string name)
        {
            string root = Directory.CreateDirectory(Path.Combine(workers, name, "000001-scratch")).Parent!.FullName;
            File.WriteAllText(Path.Combine(root, "copy.bin"), "decrypted");
            File.WriteAllText(Path.Combine(root, "000001-scratch", "part.bin"), "decrypted");
            return root;
        }
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
