using System.IO.Compression;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class OfficePackageInventoryTests
{
    [Fact]
    public void CompareTo_ReportsDeterministicDecompressedPartChanges()
    {
        using var temp = new TempDirectory();
        string beforePath = Package(temp.File("before.xlsx"),
            ("a.xml", "one"), ("b.xml", "same"), ("removed.xml", "old"));
        string afterPath = Package(temp.File("after.xlsx"),
            ("a.xml", "two"), ("b.xml", "same"), ("added.xml", "new"));

        PackageMutationReceipt mutation = OfficePackageInventory
            .Capture(beforePath, 10, 1024)
            .CompareTo(OfficePackageInventory.Capture(afterPath, 10, 1024));

        Assert.Equal(1, mutation.PreservedParts);
        Assert.Equal(
            [("a.xml", "modified"), ("added.xml", "added"), ("removed.xml", "removed")],
            mutation.ChangedParts.Select(static change => (change.Path, change.Change)));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("case")]
    [InlineData("traversal")]
    [InlineData("drive")]
    public void Capture_RejectsAmbiguousOrUnsafePartNames(string scenario)
    {
        using var temp = new TempDirectory();
        string path = temp.File("unsafe.xlsx");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            if (scenario == "duplicate")
            {
                Write(archive, "same.xml", "one");
                Write(archive, "same.xml", "two");
            }
            else if (scenario == "case")
            {
                Write(archive, "same.xml", "one");
                Write(archive, "SAME.xml", "two");
            }
            else if (scenario == "traversal")
            {
                Write(archive, "../escape.xml", "bad");
            }
            else
            {
                Write(archive, "C:/escape.xml", "bad");
            }
        }

        Assert.Throws<InvalidDataException>(() =>
            OfficePackageInventory.Capture(path, 10, 1024));
    }

    [Fact]
    public void Capture_EnforcesPartAndDecompressedByteBudgets()
    {
        using var temp = new TempDirectory();
        string path = Package(temp.File("bounded.xlsx"),
            ("one.xml", "1234"), ("two.xml", "5678"));

        Assert.Throws<InvalidDataException>(() =>
            OfficePackageInventory.Capture(path, 1, 1024));
        Assert.Throws<InvalidDataException>(() =>
            OfficePackageInventory.Capture(path, 10, 7));
    }

    [Fact]
    public void Capture_IndexesOpcContentTypesAndNormalizedRelationships()
    {
        using var temp = new TempDirectory();
        string path = Package(
            temp.File("document.docx"),
            ("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="png" ContentType="image/png"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>"""),
            ("_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="officeDocument" Target="/word/document.xml"/></Relationships>"""),
            ("word/document.xml", "<document/>"),
            ("word/_rels/document.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="image" Target="media/image%201.png"/><Relationship Id="rId2" Type="hyperlink" Target="https://example.com" TargetMode="External"/></Relationships>"""),
            ("word/media/image 1.png", "image"));

        OfficePackageInventory inventory = OfficePackageInventory.Capture(path, 10, 16_384);

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            Assert.Single(inventory.Parts, static part => part.Path == "word/document.xml").ContentType);
        Assert.Equal(
            "image/png",
            Assert.Single(inventory.Parts, static part => part.Path == "word/media/image 1.png").ContentType);
        Assert.Contains(
            inventory.ContentTypeDeclarations,
            static item => item is { Scope: "default", Name: "png", ContentType: "image/png" });
        Assert.Contains(
            inventory.ContentTypeDeclarations,
            static item => item is
            {
                Scope: "override",
                Name: "word/document.xml",
                ContentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            });
        Assert.Equal(
            [
                ("", "rId1", "word/document.xml", false),
                ("word/document.xml", "rId1", "word/media/image 1.png", false),
                ("word/document.xml", "rId2", null, true),
            ],
            inventory.Relationships.Select(static relationship =>
                (relationship.SourcePart, relationship.Id, relationship.TargetPart, relationship.External)));
    }

    [Fact]
    public void VerifyChanges_RejectsPartsOutsideTheCallerProvenClosure()
    {
        using var temp = new TempDirectory();
        OfficePackageInventory before = OfficePackageInventory.Capture(
            Package(temp.File("before.docx"), ("document.xml", "old"), ("styles.xml", "same")),
            10,
            1024);
        OfficePackageInventory after = OfficePackageInventory.Capture(
            Package(temp.File("after.docx"), ("document.xml", "new"), ("styles.xml", "changed")),
            10,
            1024);

        CliException error = Assert.Throws<CliException>(() =>
            before.VerifyChanges(after, static change => change.Path == "document.xml"));

        Assert.Equal(ErrorCodes.UnexpectedPackageMutation, error.Code);
        Assert.Equal("modified:styles.xml", Assert.Single(error.Details!["available"]!.AsArray())!.GetValue<string>());
        PackageMutationReceipt receipt = before.VerifyChanges(
            after,
            static change => change.Path is "document.xml" or "styles.xml");
        Assert.Equal(2, receipt.ChangedParts.Count);
    }

    [Fact]
    public void VerifyChanges_NeverAllowsAnExistingPartContentTypeToBeRemapped()
    {
        using var temp = new TempDirectory();
        OfficePackageInventory before = OfficePackageInventory.Capture(
            Package(
                temp.File("before.docx"),
                ("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/document.xml" ContentType="type/one"/></Types>"""),
                ("document.xml", "same")),
            10,
            4096);
        OfficePackageInventory after = OfficePackageInventory.Capture(
            Package(
                temp.File("after.docx"),
                ("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/document.xml" ContentType="type/two"/></Types>"""),
                ("document.xml", "same")),
            10,
            4096);

        CliException error = Assert.Throws<CliException>(() =>
            before.VerifyChanges(after, static _ => true));

        Assert.Equal(ErrorCodes.UnexpectedPackageMutation, error.Code);
        Assert.Contains(
            "content-type-modified:override:document.xml:type/one->type/two",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyChanges_RejectsUnrelatedContentTypeDeclarations()
    {
        using var temp = new TempDirectory();
        OfficePackageInventory before = OfficePackageInventory.Capture(
            Package(
                temp.File("before.docx"),
                ("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/document.xml" ContentType="type/document"/></Types>"""),
                ("document.xml", "same")),
            10,
            4096);
        OfficePackageInventory after = OfficePackageInventory.Capture(
            Package(
                temp.File("after.docx"),
                ("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="unused" ContentType="type/unused"/><Override PartName="/document.xml" ContentType="type/document"/></Types>"""),
                ("document.xml", "same")),
            10,
            4096);

        CliException error = Assert.Throws<CliException>(() =>
            before.VerifyChanges(after, static _ => true));

        Assert.Equal(ErrorCodes.UnexpectedPackageMutation, error.Code);
        Assert.Contains("content-type-added:default:unused", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<Relationship Id=\"rId1\" Type=\"a\" Target=\"one.xml\"/><Relationship Id=\"rId1\" Type=\"b\" Target=\"two.xml\"/>")]
    [InlineData("<Relationship Id=\"rId1\" Type=\"a\" Target=\"%2e%2e/escape.xml\"/>")]
    public void Capture_RejectsAmbiguousOrEscapingOpcRelationships(string relationships)
    {
        using var temp = new TempDirectory();
        string path = Package(
            temp.File("unsafe.docx"),
            ("_rels/.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{relationships}</Relationships>"));

        Assert.Throws<InvalidDataException>(() =>
            OfficePackageInventory.Capture(path, 10, 16_384));
    }

    [Fact]
    public void RelationshipClosure_RequiresEveryOwnerOfASharedTarget()
    {
        using var temp = new TempDirectory();
        OfficePackageInventory inventory = OfficePackageInventory.Capture(
            Package(
                temp.File("shared.docx"),
                ("a.xml", "a"),
                ("_rels/a.xml.rels", Relationships("rId1", "image", "media/shared.png")),
                ("b.xml", "b"),
                ("_rels/b.xml.rels", Relationships("rId2", "image", "media/shared.png")),
                ("media/shared.png", "image")),
            10,
            16_384);

        IReadOnlySet<string> oneOwner = inventory.RelationshipClosure(
            ["a.xml"],
            static relationship => relationship.Type == "image");
        IReadOnlySet<string> allOwners = inventory.RelationshipClosure(
            ["a.xml", "b.xml"],
            static relationship => relationship.Type == "image");

        Assert.Contains("_rels/a.xml.rels", oneOwner);
        Assert.DoesNotContain("media/shared.png", oneOwner);
        Assert.Contains("media/shared.png", allOwners);
    }

    [Fact]
    public void VerifyRelationshipChanges_IgnoresIdsButRejectsSemanticAdditions()
    {
        using var temp = new TempDirectory();
        OfficePackageInventory before = OfficePackageInventory.Capture(
            Package(temp.File("before.docx"),
                ("_rels/.rels", Relationships("rId1", "document", "document.xml")),
                ("document.xml", "document")),
            10,
            4096);
        OfficePackageInventory renumbered = OfficePackageInventory.Capture(
            Package(temp.File("renumbered.docx"),
                ("_rels/.rels", Relationships("rId99", "document", "document.xml")),
                ("document.xml", "document")),
            10,
            4096);

        before.VerifyRelationshipChanges(
            renumbered,
            static (_, _) => throw new InvalidOperationException("rId-only changes are not semantic"));

        OfficePackageInventory added = OfficePackageInventory.Capture(
            Package(temp.File("added.docx"),
                ("_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="document" Target="document.xml"/><Relationship Id="rId2" Type="metadata" Target="metadata.xml"/></Relationships>"""),
                ("document.xml", "document"),
                ("metadata.xml", "metadata")),
            10,
            4096);
        Assert.Throws<CliException>(() =>
            before.VerifyRelationshipChanges(added, static (_, _) => false));
    }

    private static string Package(string path, params (string Path, string Content)[] parts)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string partPath, string content) in parts)
        {
            Write(archive, partPath, content);
        }
        return path;
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        using StreamWriter writer = new(archive.CreateEntry(path).Open());
        writer.Write(content);
    }

    private static string Relationships(string id, string type, string target) =>
        $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"{id}\" Type=\"{type}\" Target=\"{target}\"/></Relationships>";
}
