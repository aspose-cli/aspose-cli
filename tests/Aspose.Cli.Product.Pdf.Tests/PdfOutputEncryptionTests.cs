using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Facades;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// The encryption of what PDF commands write, through the built CLI: <c>--encrypt</c> on the
/// commands that write a PDF, an edit that keeps its input's encryption, a merge or split that
/// keeps the open password of an input, <c>output.encrypted</c> as the truth of the written file
/// (a file with only an owner password included), and <c>ENCRYPTION_REMOVED</c> whenever an
/// output does not keep the encryption of an input it was made from.
/// </summary>
public sealed class PdfOutputEncryptionTests : IDisposable
{
    private const string UserPassword = "user-secret";
    private const string OwnerPassword = "owner-secret";
    private const string NewPassword = "new-secret";
    private static readonly Dictionary<string, string?> Passwords = new(StringComparer.Ordinal)
    {
        ["PDF_USER"] = UserPassword,
        ["PDF_OWNER"] = OwnerPassword,
        ["PDF_NEW"] = NewPassword,
    };

    private readonly TempWorkspace _workspace = new();

    public PdfOutputEncryptionTests()
    {
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("first page"));
            document.Pages.Add().Paragraphs.Add(new TextFragment("second page"));
            document.Save(_workspace.File("plain.pdf"));
        }

        Encrypt("plain.pdf", "owner-only.pdf", user: string.Empty);
        Encrypt("plain.pdf", "locked.pdf", user: UserPassword);
    }

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Fact]
    public void EditOfOwnerOnlyInput_ReportsTheKeptEncryptionTruthfully()
    {
        JsonNode result = Run("pdf", "edit", "owner-only.pdf", "--ops", Metadata, "--out", "edited.pdf").Json();

        Assert.True(Encrypted(_workspace.File("edited.pdf")));
        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EditThatDecrypts_DisclosesTheRemovedEncryption()
    {
        JsonNode result = Run("pdf", "edit", "locked.pdf", "--password-env", "PDF_USER",
            "--ops", """[{"op":"decrypt_document"}]""", "--out", "decrypted.pdf").Json();

        Assert.False(Encrypted(_workspace.File("decrypted.pdf")));
        Assert.False(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.Contains("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void MergeOfAnEncryptedInput_DisclosesTheRemovedEncryption()
    {
        JsonNode result = Run("pdf", "merge", "owner-only.pdf", "plain.pdf", "--out", "merged.pdf").Json();

        Assert.False(Encrypted(_workspace.File("merged.pdf")));
        Assert.False(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.Contains("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void SplitOfAnOwnerOnlyInput_DisclosesTheRemovedEncryption()
    {
        JsonNode result = Run("pdf", "split", "owner-only.pdf", "--by", "pages", "--every", "1", "--out-dir", "parts").Json();

        Assert.All(Directory.GetFiles(_workspace.File("parts")), part => Assert.False(Encrypted(part)));
        Assert.Equal([false, false], PartsEncrypted(result));
        Assert.Contains("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
    }

    /// <summary>
    /// The in-process session the tests share is wired as activation wires it: its loader tells
    /// the write pipeline that the input is encrypted, so the result discloses the removal.
    /// </summary>
    [Fact]
    public void InProcessConvertOfAnEncryptedInput_DisclosesTheRemovedEncryption()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateEncryptedDocument(UserPassword, OwnerPassword, "locked.pdf");

        PdfConvertResult result = fixture.Disclosed(session => PdfConvert.Run(session, new PdfConvertRequest
        {
            Input = input,
            Password = new Secret(UserPassword),
            Output = TestOutput.At(fixture.File("locked.txt"), format: "txt"),
        }));

        Assert.Contains(result.Warnings ?? [], static warning => warning.Code == WarningCodes.EncryptionRemoved);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void SplitOfAPlainInput_ReportsEveryPartUnencrypted()
    {
        JsonNode result = Run("pdf", "split", "plain.pdf", "--by", "pages", "--every", "1", "--out-dir", "parts").Json();

        Assert.Equal([false, false], PartsEncrypted(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void MergeAndSplitOfAnInputWithAnOpenPassword_KeepThatPassword()
    {
        JsonNode merged = null!, split = null!;
        Parallel.Invoke(
            () => merged = Run("pdf", "merge", "locked.pdf", "plain.pdf", "--password-env", "PDF_USER", "--out", "merged.pdf").Json(),
            () => split = Run("pdf", "split", "locked.pdf", "--password-env", "PDF_USER",
                "--by", "pages", "--every", "1", "--out-dir", "parts").Json());

        Assert.True(merged["output"]!["encrypted"]!.GetValue<bool>());
        Assert.Equal([true, true], PartsEncrypted(split));
        string[] written = [_workspace.File("merged.pdf"), .. Directory.GetFiles(_workspace.File("parts"))];
        Assert.Equal(3, written.Length);
        Assert.All(written, path => OpensOnlyWith(path, UserPassword));
        // The ForbidAll permissions are kept: the open password still grants none of them, and
        // the input's owner password, which the run did not know, does not open the output.
        int forbidden = Permissions(_workspace.File("locked.pdf"), UserPassword);
        Assert.All(written, path => Assert.Equal(forbidden, Permissions(path, UserPassword)));
        Assert.All(written, path => Assert.Throws<InvalidPasswordException>(() => new Document(path, OwnerPassword).Dispose()));
        foreach (JsonNode result in new[] { merged, split })
        {
            Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
            Assert.Contains("PROTECTION_NOT_ENFORCED", ResultWarnings.Codes(result));
        }
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void MergeAndSplitOpenedWithTheOwnerPassword_DiscloseThatItBecomesTheOpenPassword()
    {
        JsonNode merged = null!, split = null!;
        Parallel.Invoke(
            () => merged = Run("pdf", "merge", "locked.pdf", "plain.pdf", "--password-env", "PDF_OWNER", "--out", "merged.pdf").Json(),
            () => split = Run("pdf", "split", "locked.pdf", "--password-env", "PDF_OWNER",
                "--by", "pages", "--every", "1", "--out-dir", "parts").Json());

        string[] written = [_workspace.File("merged.pdf"), .. Directory.GetFiles(_workspace.File("parts"))];
        Assert.All(written, path => OpensOnlyWith(path, OwnerPassword));
        Assert.All(written, path => Assert.Throws<InvalidPasswordException>(() => new Document(path, UserPassword).Dispose()));
        foreach (JsonNode result in new[] { merged, split })
        {
            Warning(result, "PROTECTION_NOT_ENFORCED", "grants every permission");
        }
    }

    /// <summary>
    /// A file whose one password both opens it and is its owner password, as <c>--encrypt</c> and
    /// a merge opened with the owner password write it, has an owner password, and opening it with
    /// that password gives owner access: inspect says so, and a merge of it keeps that password
    /// without claiming that another owner password stays behind.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void OnePasswordThatIsAlsoTheOwnerPassword_IsReportedAsTheOwnerPassword()
    {
        JsonNode merged = null!;
        Parallel.Invoke(
            () => Run("pdf", "edit", "plain.pdf", "--ops", Metadata, "--out", "same.pdf", "--encrypt-env", "PDF_NEW").Json(),
            () => merged = Run("pdf", "merge", "locked.pdf", "plain.pdf", "--password-env", "PDF_OWNER", "--out", "merged.pdf").Json());
        Warning(merged, "PROTECTION_NOT_ENFORCED", "which is also its owner password");
        JsonNode remerged = Run("pdf", "merge", "same.pdf", "plain.pdf", "--password-env", "PDF_NEW", "--out", "remerged.pdf").Json();

        foreach ((string file, string env) in new[] { ("same.pdf", "PDF_NEW"), ("merged.pdf", "PDF_OWNER"), ("remerged.pdf", "PDF_NEW") })
        {
            JsonNode info = Run("pdf", "inspect", file, "--password-env", env, "--detail", "permissions").Json();
            Assert.True(info["permissions"]!["hasOpenPassword"]!.GetValue<bool>(), file);
            Assert.True(info["permissions"]!["hasOwnerPassword"]!.GetValue<bool>(), file);
            Assert.True(info["permissions"]!["ownerAccess"]!.GetValue<bool>(), file);
            Assert.Equal("owner", info["pdf"]!["passwordType"]!.GetValue<string>());
        }

        // The input's one password opens the output and grants what it granted: nothing is lost.
        OpensOnlyWithTheNewPassword(_workspace.File("remerged.pdf"));
        Assert.DoesNotContain("PROTECTION_NOT_ENFORCED", ResultWarnings.Codes(remerged));
        Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(remerged));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ImagesAndExtractedFilesOfAnEncryptedInput_DiscloseNothing()
    {
        using (var document = new Document())
        {
            Page page = document.Pages.Add();
            document.Form.Add(new TextBoxField(page, new Rectangle(72, 650, 280, 680)) { PartialName = "Customer", Value = "Draft" });
            document.Save(_workspace.File("form.pdf"));
        }

        Encrypt("form.pdf", "locked-form.pdf", user: UserPassword);
        JsonNode[] results = new JsonNode[4];
        Parallel.Invoke(
            () => results[0] = Run("pdf", "extract", "locked-form.pdf", "--password-env", "PDF_USER", "--what", "forms", "--form-format", "json", "--out-dir", "fields").Json(),
            () => results[1] = Run("pdf", "extract", "locked.pdf", "--password-env", "PDF_USER", "--what", "text", "--out-dir", "text").Json(),
            () => results[2] = Run("pdf", "convert", "locked.pdf", "--password-env", "PDF_USER", "--to", "tiff", "--out", "pages.tiff").Json(),
            () => results[3] = Run("pdf", "render", "locked.pdf", "--password-env", "PDF_USER", "--out", "render.png").Json());

        Assert.All(results, result => Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result)));
        // Images, text and form data cannot carry a password, so their files report no encryption.
        Assert.All(results, result => Assert.All(result["outputs"]!.AsArray(), output => Assert.Null(output!["encrypted"])));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void TextConvertedFromAnEncryptedInput_DisclosesTheRemovedEncryption()
    {
        JsonNode result = Run("pdf", "convert", "locked.pdf", "--password-env", "PDF_USER", "--to", "txt", "--out", "pages.txt").Json();

        Assert.Contains("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void PlainInput_DisclosesNothing()
    {
        JsonNode result = Run("pdf", "merge", "plain.pdf", "plain.pdf", "--out", "merged.pdf").Json();

        Assert.False(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Encrypt_ProtectsEveryWrittenPdfWithItsPassword()
    {
        File.WriteAllText(_workspace.File("text.txt"), "Created text.\n");
        // The four writes are independent, so they run at once.
        JsonNode[] results = new JsonNode[4];
        Parallel.Invoke(
            () => results[0] = Run("pdf", "create", "created.pdf", "--from-text", "text.txt", "--encrypt-env", "PDF_NEW").Json(),
            () => results[1] = Run("pdf", "edit", "owner-only.pdf", "--ops", Metadata, "--out", "edited.pdf", "--encrypt-env", "PDF_NEW").Json(),
            () => results[2] = Run("pdf", "merge", "locked.pdf", "plain.pdf", "--password-env", "PDF_USER", "--out", "merged.pdf", "--encrypt-env", "PDF_NEW").Json(),
            () => results[3] = Run("pdf", "split", "plain.pdf", "--by", "pages", "--every", "1", "--out-dir", "parts", "--encrypt-env", "PDF_NEW").Json());

        string[] written =
        [
            _workspace.File("created.pdf"), _workspace.File("edited.pdf"), _workspace.File("merged.pdf"),
            .. Directory.GetFiles(_workspace.File("parts")),
        ];
        Assert.Equal(5, written.Length);
        Assert.All(written, OpensOnlyWithTheNewPassword);
        Assert.All(results[..3], result => Assert.True(result["output"]!["encrypted"]!.GetValue<bool>()));
        Assert.Equal([true, true], PartsEncrypted(results[3]));
        Assert.All(results, result => Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result)));
        Assert.All(results, result => Assert.DoesNotContain(NewPassword, result.ToJsonString(), StringComparison.Ordinal));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EncryptOnAnEncryptedInput_KeepsDocumentInformationBeyondLatin1()
    {
        JsonNode result = Run("pdf", "edit", "owner-only.pdf", "--ops", """[{"op":"set_properties","title":"采购申请"}]""",
            "--out", "edited.pdf", "--encrypt-env", "PDF_NEW").Json();

        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        using var document = new Document(_workspace.File("edited.pdf"), NewPassword);
        Assert.Equal("采购申请", document.Info.Title);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EncryptFromStandardInput_ProtectsTheOutput()
    {
        CliResult run = _workspace.RunWithInput(NewPassword + "\n",
            "pdf", "merge", "plain.pdf", "plain.pdf", "--out", "merged.pdf", "--encrypt-stdin", "--output-format", "json");

        Assert.True(run.Json()["output"]!["encrypted"]!.GetValue<bool>());
        OpensOnlyWithTheNewPassword(_workspace.File("merged.pdf"));
    }

    private const string Metadata = """[{"op":"set_properties","title":"Edited"}]""";

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Passwords, [.. arguments, "--output-format", "json"]);

    private void Encrypt(string source, string target, string user)
    {
        using var document = new Document(_workspace.File(source));
        document.Encrypt(user, OwnerPassword, DocumentPrivilege.ForbidAll, CryptoAlgorithm.AESx256, usePdf20: false);
        document.Save(_workspace.File(target));
    }

    // The truth of a file, read without a password: a file with an open password does not even
    // let its encryption be read without one.
    private static bool Encrypted(string path)
    {
        var info = new PdfFileInfo(path);
        try
        {
            return info.IsEncrypted;
        }
        catch (InvalidPasswordException)
        {
            return true;
        }
    }

    private static void OpensOnlyWithTheNewPassword(string path) => OpensOnlyWith(path, NewPassword);

    private static void OpensOnlyWith(string path, string password)
    {
        Assert.Throws<InvalidPasswordException>(() => new Document(path).Dispose());
        using var document = new Document(path, password);
        Assert.True(document.IsEncrypted);
    }

    private static int Permissions(string path, string password)
    {
        using var document = new Document(path, password);
        return document.Permissions;
    }

    private static void Warning(JsonNode result, string code, string text) =>
        Assert.Contains(result["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == code
            && warning["message"]!.GetValue<string>().Contains(text, StringComparison.Ordinal));

    /// <summary>The <c>encrypted</c> of each <c>outputs[]</c> item, which a PDF part always reports.</summary>
    private static bool[] PartsEncrypted(JsonNode result) =>
        [.. result["outputs"]!.AsArray().Select(static output => output!["encrypted"]!.GetValue<bool>())];
}
