using System.IO.Compression;
using System.IO.Pipes;
using System.Net;
using System.Text;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Loading an OpenDocument spreadsheet, the engine opens what an SVG picture names, by path and
/// past every hook (CELLS-SVG-EGRESS). The package it is given keeps its SVG pictures to
/// themselves, a compressed SVG is left out, and an encrypted package, which cannot be cleaned
/// first, is refused. A pipe of the test's own sees any open of the paths the pictures name.
/// </summary>
public sealed class CellsOdsPackageTests
{
    public static TheoryData<string, string> Forms => new()
    {
        { "ods", "device" },
        { "ods", "share" },
        { "ods", "uri" },
        { "ots", "share" },
    };

    [Theory]
    [MemberData(nameof(Forms))]
    public void AnSvgPicture_IsLoadedWithoutOpeningWhatItNames(string format, string form)
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string name = PipeName();
        string path = temp.File("book." + format);
        Write(path, format == "ots" ? SaveFormat.Ots : SaveFormat.Ods, Reference(form, name));
        // Listening from here: making the workbook drew its picture, which opened the pipe.
        using var pipe = new CountingPipe(name);

        using (LoadedWorkbook loaded = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(path, null))
        {
            Assert.Single(loaded.Workbook.Worksheets[0].Pictures);
            Assert.Contains(loaded.Warnings() ?? [], static warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        }

        Assert.Equal(0, pipe.Settled());
    }

    [Fact]
    public void ACompressedSvgPicture_IsLeftOutWithoutOpeningWhatItNames()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string name = PipeName();
        string path = temp.File("book.ods");
        Write(path, SaveFormat.Ods, Reference("share", name));
        Compress(path);
        using var pipe = new CountingPipe(name);

        using (LoadedWorkbook loaded = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(path, null))
        {
            Assert.Contains(loaded.Warnings() ?? [], static warning => warning.Code.Name == "IMAGES_SKIPPED");
        }

        Assert.Equal(0, pipe.Settled());
    }

    [Fact]
    public void AnEncryptedPackage_IsRefusedBeforeTheEngineLoadsIt()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string name = PipeName();
        string path = temp.File("book.ods");
        Write(path, SaveFormat.Ods, Reference("share", name), password: "secret");
        using var pipe = new CountingPipe(name);

        CliException refused = Assert.Throws<CliException>(() =>
            CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(path, new Secret("secret")).Dispose());

        Assert.Equal(ErrorCodes.FeatureUnsupported, refused.Code);
        Assert.Equal(0, pipe.Settled());
    }

    /// <summary>
    /// What an SVG entry may hold besides its images: a document type whose external subset, or
    /// a parameter entity of it, names the pipe, and broken markup or compressed data that cannot be judged.
    /// </summary>
    public static TheoryData<string> Entries => ["doctype-system", "parameter-entity", "broken", "broken-compressed"];

    [Theory]
    [MemberData(nameof(Entries))]
    public void AnSvgEntry_IsLoadedWithoutOpeningWhatItsMarkupNames(string entry)
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string name = PipeName();
        string path = temp.File("book.ods");
        Write(path, SaveFormat.Ods, "#none");
        string reference = WebUtility.HtmlEncode(Reference("share", name));
        string svg = entry switch
        {
            "doctype-system" => $"""<?xml version="1.0"?><!DOCTYPE svg SYSTEM "{reference}"><svg xmlns="http://www.w3.org/2000/svg" width="40" height="40"><rect width="40" height="40"/></svg>""",
            "parameter-entity" => $"""<?xml version="1.0"?><!DOCTYPE svg [<!ENTITY % remote SYSTEM "{reference}"> %remote;]><svg xmlns="http://www.w3.org/2000/svg" width="40" height="40"><rect width="40" height="40"/></svg>""",
            _ => $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><image xlink:href="{reference}" """,
        };
        // A gzip header over data that does not inflate: what it holds cannot be judged.
        byte[] replacement = entry == "broken-compressed" ? [0x1F, 0x8B, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0B, 0xFF, 0xFF, 0xFF, 0x00, 0x13]
            : Encoding.UTF8.GetBytes(svg);
        Rewrite(path, (entryName, data) => entryName.EndsWith(".svg", StringComparison.Ordinal) ? replacement : data);
        using var pipe = new CountingPipe(name);

        using (LoadedWorkbook loaded = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(path, null))
        {
            if (entry.StartsWith("broken", StringComparison.Ordinal))
            {
                Assert.Contains(loaded.Warnings() ?? [], static warning => warning.Code.Name == "IMAGES_SKIPPED");
            }
        }

        Assert.Equal(0, pipe.Settled());
    }

    /// <summary>An SVG picture stored inline in content.xml, as office:binary-data, is kept to itself too.</summary>
    [Fact]
    public void AnInlineSvgPicture_IsLoadedWithoutOpeningWhatItNames()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string name = PipeName();
        string path = temp.File("book.ods");
        Write(path, SaveFormat.Ods, Reference("share", name));
        byte[]? picture = null;
        Rewrite(path, (entryName, data) =>
        {
            if (entryName.EndsWith(".svg", StringComparison.Ordinal))
            {
                picture = data;
                return null;
            }
            return data;
        });
        Rewrite(path, (entryName, data) => entryName == "content.xml" ? Inline(data, picture!) : data);
        using var pipe = new CountingPipe(name);

        using (LoadedWorkbook loaded = CellsWorkbookLoader.ForFontInspection(ProductTestBudgets.Create<CellsModule>()).Open(path, null))
        {
            Assert.Contains(loaded.Warnings() ?? [], static warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        }

        Assert.Equal(0, pipe.Settled());
    }

    // content.xml with every draw:image holding the picture inline instead of linking it.
    private static byte[] Inline(byte[] content, byte[] picture)
    {
        System.Xml.Linq.XNamespace draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        System.Xml.Linq.XNamespace office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        System.Xml.Linq.XNamespace xlink = "http://www.w3.org/1999/xlink";
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(new MemoryStream(content));
        foreach (System.Xml.Linq.XElement image in document.Descendants(draw + "image"))
        {
            image.Attribute(xlink + "href")?.Remove();
            image.Add(new System.Xml.Linq.XElement(office + "binary-data", Convert.ToBase64String(picture)));
        }
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    // Rewrites the entries of a package; a null result leaves the entry out.
    private static void Rewrite(string path, Func<string, byte[], byte[]?> change)
    {
        byte[] original = File.ReadAllBytes(path);
        using var input = new ZipArchive(new MemoryStream(original));
        using var output = new FileStream(path, FileMode.Create);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (ZipArchiveEntry entry in input.Entries)
        {
            using Stream source = entry.Open();
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            if (change(entry.FullName, buffer.ToArray()) is not { } data)
            {
                continue;
            }
            ZipArchiveEntry copy = archive.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
            using Stream target = copy.Open();
            target.Write(data);
        }
    }

    private static string PipeName() => "aspose-cli-ods-" + Guid.NewGuid().ToString("N");

    private static string Reference(string form, string name) => form switch
    {
        "device" => @"\\.\pipe\" + name,
        "share" => @"\\localhost\pipe\" + name,
        _ => "file://localhost/pipe/" + name,
    };

    // A workbook with one SVG picture whose image names the reference.
    private static void Write(string path, SaveFormat format, string reference, string? password = null)
    {
        byte[] svg = Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="40" height="40"><image xlink:href="{WebUtility.HtmlEncode(reference)}" width="40" height="40"/></svg>""");
        using var workbook = new Workbook();
        workbook.Worksheets[0].Cells["A1"].PutValue("Picture");
        workbook.Worksheets[0].Shapes.AddSvg(1, 0, 1, 0, 40, 40, svg, null);
        if (password is not null)
        {
            workbook.Settings.Password = password;
        }
        workbook.Save(path, format);
    }

    // Stores every SVG entry of the package gzip-compressed, as an svgz.
    private static void Compress(string path)
    {
        byte[] original = File.ReadAllBytes(path);
        using var input = new ZipArchive(new MemoryStream(original));
        using var output = new FileStream(path, FileMode.Create);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (ZipArchiveEntry entry in input.Entries)
        {
            using Stream source = entry.Open();
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            byte[] data = buffer.ToArray();
            if (entry.FullName.EndsWith(".svg", StringComparison.Ordinal))
            {
                using var compressed = new MemoryStream();
                using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                {
                    gzip.Write(data);
                }
                data = compressed.ToArray();
            }
            ZipArchiveEntry copy = archive.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
            using Stream target = copy.Open();
            target.Write(data);
        }
    }

    /// <summary>A named pipe that counts every connection to it until disposed.</summary>
    private sealed class CountingPipe : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _accepting;
        private int _connections;

        public CountingPipe(string name)
        {
            Name = name;
            NamedPipeServerStream first = Create();
            _accepting = Task.Run(async () =>
            {
                NamedPipeServerStream server = first;
                try
                {
                    while (true)
                    {
                        try
                        {
                            await server.WaitForConnectionAsync(_stop.Token);
                        }
                        catch (IOException)
                        {
                        }
                        Interlocked.Increment(ref _connections);
                        NamedPipeServerStream next = Create();
                        await server.DisposeAsync();
                        server = next;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    await server.DisposeAsync();
                }
            });
        }

        public string Name { get; }

        public int Settled()
        {
            Thread.Sleep(250);
            return Volatile.Read(ref _connections);
        }

        private NamedPipeServerStream Create() => new(
            Name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        public void Dispose()
        {
            _stop.Cancel();
            _accepting.Wait();
            _stop.Dispose();
        }
    }
}
