using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>Merge and split keep the document-level attachments and the attachment annotations of their inputs.</summary>
public sealed class PdfAttachmentCarryTests : IDisposable
{
    private static readonly byte[] First = Encoding.UTF8.GetBytes("region,total\neast,10\n");
    private static readonly byte[] Second = Encoding.UTF8.GetBytes("region,total\nsouth,20\n");

    private readonly TempWorkspace _workspace = new();

    [Category(TestCategory.Slow)]
    [Fact]
    public void Merge_KeepsTheAttachmentsOfEveryInputThroughTheBuiltCli()
    {
        WriteDocument(_workspace.File("a.pdf"), pages: 1, ("a.csv", First));
        WriteDocument(_workspace.File("b.pdf"), pages: 1, ("b.csv", Second));

        CliResult merge = _workspace.Run("pdf", "merge", "a.pdf", "b.pdf", "--out", "merged.pdf", "--output", "json");
        Assert.True(merge.ExitCode == 0, merge.StdErr);
        CliResult inspect = _workspace.Run("pdf", "inspect", "merged.pdf", "--detail", "attachments", "--output", "json");
        CliResult extract = _workspace.Run(
            "pdf", "extract", "merged.pdf", "--what", "attachments", "--out-dir", "files", "--output", "json");

        Assert.True(inspect.ExitCode == 0, inspect.StdErr);
        Assert.Equal(
            ["a.csv", "b.csv"],
            inspect.Json()["attachments"]!.AsArray().Select(static item => item!["name"]!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.True(extract.ExitCode == 0, extract.StdErr);
        Dictionary<string, string> paths = extract.Json()["items"]!.AsArray().ToDictionary(
            static item => item!["name"]!.GetValue<string>(),
            static item => item!["path"]!.GetValue<string>(),
            StringComparer.Ordinal);
        Assert.Equal(First, File.ReadAllBytes(paths["a.csv"]));
        Assert.Equal(Second, File.ReadAllBytes(paths["b.csv"]));
        Assert.DoesNotContain(merge.Json()["warnings"]?.AsArray() ?? [], static warning =>
            warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION");
    }

    [Fact]
    public void Merge_KeepsTheFirstInputsAttachmentOfANameAndNamesTheInputWhoseOneItDropped()
    {
        using var fixture = new PdfEngineFixture();
        string first = WriteDocument(fixture.File("first.pdf"), pages: 1, ("same.csv", First));
        string second = WriteDocument(fixture.File("second.pdf"), pages: 1, ("same.csv", Second));
        string output = fixture.File("merged.pdf");

        PdfWriteResult result = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(output),
            InputPaths = [first, second],
        });

        Assert.Equal([Entry("same.csv", First)], Attachments(output));
        Warning dropped = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "LOSSY_CONVERSION");
        Assert.Contains("same.csv", dropped.Location, StringComparison.Ordinal);
        Assert.Contains(second, dropped.Location, StringComparison.Ordinal);
        Assert.DoesNotContain(first, dropped.Location, StringComparison.Ordinal);
        Assert.True(dropped.AffectsCompleteness);
    }

    [Fact]
    public void Split_GivesEveryPartAllAttachmentsOfTheSource()
    {
        using var fixture = new PdfEngineFixture();
        string input = WriteDocument(fixture.File("report.pdf"), pages: 3, ("a.csv", First), ("b.csv", Second));

        PdfSplitResult result = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("parts")),
            Every = 1,
        });

        Assert.Equal(3, result.Outputs.Count);
        Assert.All(result.Outputs, part => Assert.Equal([Entry("a.csv", First), Entry("b.csv", Second)], Attachments(part.Output.Path)));
        Assert.Null(result.Warnings);
    }

    [Fact]
    public void MergeAndSplit_CopyThePortfolioAttachmentsAndDiscloseTheLostPortfolio()
    {
        using var fixture = new PdfEngineFixture();
        string portfolio = fixture.File("portfolio.pdf");
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Portfolio cover"));
            document.Pages.Add().Paragraphs.Add(new TextFragment("Portfolio notes"));
            document.Collection = new Collection();
            document.Collection.Add(Specification("a.csv", First));
            document.Collection.Add(Specification("b.csv", Second));
            document.Save(portfolio);
        }

        string plain = fixture.CreateDocument("plain.pdf", pages: 1);
        string merged = fixture.File("merged.pdf");

        PdfWriteResult merge = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(merged),
            InputPaths = [plain, portfolio],
        });
        PdfSplitResult split = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = portfolio,
            Output = new ResolvedDirectory(fixture.File("parts")),
            Every = 1,
        });

        Assert.Equal([Entry("a.csv", First), Entry("b.csv", Second)], Attachments(merged));
        Assert.All(split.Outputs, part => Assert.Equal([Entry("a.csv", First), Entry("b.csv", Second)], Attachments(part.Output.Path)));
        Warning mergeWarning = Assert.Single(merge.Warnings!, static warning => warning.Code.Name == "LOSSY_CONVERSION");
        Assert.Contains("portfolio", mergeWarning.Message, StringComparison.Ordinal);
        Assert.Contains(portfolio, mergeWarning.Location, StringComparison.Ordinal);
        Warning splitWarning = Assert.Single(split.Warnings!, static warning => warning.Code.Name == "LOSSY_CONVERSION");
        Assert.Contains("portfolio", splitWarning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeAndSplit_KeepTheFileAttachmentAnnotationsOfTheirPages()
    {
        using var fixture = new PdfEngineFixture();
        string annotated = fixture.File("annotated.pdf");
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Cover"));
            Page page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Evidence"));
            page.Annotations.Add(new FileAttachmentAnnotation(page, new Rectangle(72, 600, 96, 624), Specification("evidence.csv", First)));
            document.Save(annotated);
        }

        string plain = fixture.CreateDocument("plain.pdf", pages: 1);
        string merged = fixture.File("merged.pdf");

        PdfWriteResult merge = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(merged),
            InputPaths = [plain, annotated],
        });
        PdfSplitResult split = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = annotated,
            Output = new ResolvedDirectory(fixture.File("parts")),
            PageGroups = [Sdk.Addressing.PageRange.Parse("2")],
        });

        Assert.Equal(First, AnnotationFile(merged, page: 3));
        Assert.Equal(First, AnnotationFile(Assert.Single(split.Outputs).Output.Path, page: 1));
        Assert.Null(merge.Warnings);
        Assert.Null(split.Warnings);
    }

    [Fact]
    public void MergeAndSplit_WithoutAttachments_AddNoWarning()
    {
        using var fixture = new PdfEngineFixture();
        string first = fixture.CreateDocument("first.pdf", pages: 2);
        string second = fixture.CreateDocument("second.pdf", pages: 1);
        string merged = fixture.File("merged.pdf");

        PdfWriteResult merge = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(merged),
            InputPaths = [first, second],
        });
        PdfSplitResult split = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = first,
            Output = new ResolvedDirectory(fixture.File("parts")),
            Every = 1,
        });

        Assert.Null(merge.Warnings);
        Assert.Null(split.Warnings);
        Assert.Empty(Attachments(merged));
    }

    /// <summary>An attachment of a name the merged document holds with the same contents loses nothing.</summary>
    [Fact]
    public void Merge_OfTheSameInputTwice_KeepsOneCopyOfEachAttachmentWithoutWarning()
    {
        using var fixture = new PdfEngineFixture();
        string input = WriteDocument(fixture.File("a.pdf"), pages: 1, ("a.csv", First));
        string merged = fixture.File("merged.pdf");

        PdfWriteResult result = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(merged),
            InputPaths = [input, input],
        });

        Assert.Equal([Entry("a.csv", First)], Attachments(merged));
        Assert.Null(result.Warnings);
    }

    /// <summary>
    /// The engine lists both attachments of one document that show the same name under two keys of
    /// its name tree; both are the input's data, so merge and split keep both.
    /// </summary>
    [Fact]
    public void MergeAndSplit_KeepTwoAttachmentsOfOneInputThatShowTheSameName()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.File("twins.pdf");
        using (var document = new Document())
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment("Page 1"));
            document.Pages.Add().Paragraphs.Add(new TextFragment("Page 2"));
            document.EmbeddedFiles.Add("first", Specification("same.csv", First));
            document.EmbeddedFiles.Add("second", Specification("same.csv", Second));
            document.Save(input);
        }

        string plain = fixture.CreateDocument("plain.pdf", pages: 1);
        string merged = fixture.File("merged.pdf");
        string[] expected = [Entry("same.csv", First), Entry("same.csv", Second)];
        Assert.Equal(expected, Attachments(input));

        PdfWriteResult merge = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(merged),
            InputPaths = [plain, input],
        });
        PdfSplitResult split = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("parts")),
            Every = 1,
        });

        Assert.Equal(expected, Attachments(merged));
        Assert.All(split.Outputs, part => Assert.Equal(expected, Attachments(part.Output.Path)));
        Assert.Null(merge.Warnings);
        Assert.Null(split.Warnings);
    }

    [Fact]
    public void Merge_KeepsTheUnnamedAttachmentsOfEveryInput()
    {
        using var fixture = new PdfEngineFixture();
        string first = WriteDocument(fixture.File("first.pdf"), pages: 1, (string.Empty, First));
        string second = WriteDocument(fixture.File("second.pdf"), pages: 1, (string.Empty, Second));
        string merged = fixture.File("merged.pdf");

        PdfWriteResult result = PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(merged),
            InputPaths = [first, second],
        });

        Assert.Equal([Entry(string.Empty, First), Entry(string.Empty, Second)], Attachments(merged));
        Assert.Null(result.Warnings);
    }

    [Fact]
    public void MergeAndSplit_OfAnEncryptedInput_KeepItsAttachments()
    {
        using var fixture = new PdfEngineFixture();
        string input = WriteDocument(fixture.File("locked.pdf"), pages: 2, ("a.csv", First));
        using (var document = new Document(input))
        {
            document.Encrypt("open-sesame", "owner-sesame", Aspose.Pdf.Facades.DocumentPrivilege.ForbidAll, CryptoAlgorithm.AESx256, usePdf20: false);
            document.Save(input);
        }

        string other = WriteDocument(fixture.File("other.pdf"), pages: 1, ("b.csv", Second));
        using (var document = new Document(other))
        {
            document.Encrypt("open-sesame", "owner-sesame", Aspose.Pdf.Facades.DocumentPrivilege.ForbidAll, CryptoAlgorithm.AESx256, usePdf20: false);
            document.Save(other);
        }

        string merged = fixture.File("merged.pdf");
        var password = new Secret("open-sesame");

        PdfMerge.Run(fixture.Session, new PdfMergeRequest
        {
            Output = TestOutput.At(merged),
            InputPaths = [input, other],
            Password = password,
        });
        PdfSplitResult split = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("parts")),
            Every = 1,
            Password = password,
        });

        Assert.Equal([Entry("a.csv", First), Entry("b.csv", Second)], Attachments(merged));
        Assert.All(split.Outputs, part => Assert.Equal([Entry("a.csv", First)], Attachments(part.Output.Path)));
    }

    public void Dispose() => _workspace.Dispose();

    private static string WriteDocument(string path, int pages, params (string Name, byte[] Content)[] attachments)
    {
        using var document = new Document();
        for (int page = 1; page <= pages; page++)
        {
            document.Pages.Add().Paragraphs.Add(new TextFragment($"Page {page}"));
        }

        foreach ((string name, byte[] content) in attachments)
        {
            document.EmbeddedFiles.Add(name, Specification(name, content));
        }

        document.Save(path);
        return path;
    }

    private static FileSpecification Specification(string name, byte[] content) =>
        new(new MemoryStream(content), name, name) { UnicodeName = name };

    private static string Entry(string name, byte[] content) => $"{name}={Convert.ToHexString(content)}";

    /// <summary>The attachments of a document as name and contents, in name order.</summary>
    private static List<string> Attachments(string path)
    {
        using var document = new Document(path);
        return document.EmbeddedFiles
            .Select(static file => Entry(file.UnicodeName ?? file.Name, Read(file)))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static byte[] AnnotationFile(string path, int page)
    {
        using var document = new Document(path);
        FileAttachmentAnnotation annotation = Assert.Single(document.Pages[page].Annotations.OfType<FileAttachmentAnnotation>());
        return Read(annotation.File);
    }

    private static byte[] Read(FileSpecification file)
    {
        using var copy = new MemoryStream();
        Stream contents = file.Contents;
        if (contents.CanSeek)
        {
            contents.Position = 0;
        }

        contents.CopyTo(copy);
        return copy.ToArray();
    }
}
