using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;

namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// Named seed files for scenarios. Documents are built once per test process with the products'
/// own commands, under the run's license mode, and copied into each scenario workspace:
/// <list type="bullet">
/// <item><c>&lt;product&gt;.&lt;format&gt;</c>: a small document; the product's primary format
/// (<see cref="PrimaryFormat"/>) is created with its <c>create</c> command, any other format is
/// that document converted with its <c>convert --to</c>.</item>
/// <item><c>&lt;product&gt;.encrypted</c>: the product's primary-format document encrypted with
/// <see cref="Password"/>.</item>
/// <item><c>&lt;product&gt;.ops</c>: a valid one-operation document for the product's <c>edit --ops</c>.</item>
/// <item><c>slides.media</c> and <c>pdf.media</c>: the primary document with media every value of
/// its <c>extract --what</c> writes a file from.</item>
/// <item><c>&lt;product&gt;.rich</c>: the primary format with one marker text in each kind of
/// content the product makes (<see cref="RichMarkers"/>), in the PDF a picture and in the presentation
/// a table and a chart.</item>
/// <item><c>markdown</c> and <c>text</c>: source text that holds <see cref="SampleText"/>.</item>
/// <item><c>certificate.pfx</c>: a self-signed signing certificate protected by <see cref="Password"/>.</item>
/// </list>
/// </summary>
public static class ScenarioFixtures
{
    /// <summary>Text every built document contains, for searches.</summary>
    public const string SampleText = "Hello invariant world";

    /// <summary>
    /// The password of every protected fixture: the encrypted documents and <c>certificate.pfx</c>.
    /// It is a secret, so the CLI must never print it.
    /// </summary>
    public const string Password = "Invariant-Secret-5f1c9e7a";

    private const string PasswordVariable = "FIXTURE_PASSWORD";

    private static readonly Dictionary<string, string> PrimaryFormats = new(StringComparer.Ordinal)
    {
        ["cells"] = "xlsx",
        ["pdf"] = "pdf",
        ["slides"] = "pptx",
        ["words"] = "docx",
    };

    private static readonly Dictionary<string, string> Operations = new(StringComparer.Ordinal)
    {
        ["cells"] = """{"ops":[{"set":"sheet/Sheet1/cell/D1","props":{"value":"edited"}}]}""",
        ["pdf"] = """{"ops":[{"op":"set_properties","title":"Edited"}]}""",
        ["slides"] = """{"ops":[{"set":"presentation","props":{"title":"Edited"}}]}""",
        ["words"] = """{"ops":[{"set":"document","props":{"builtInDocumentProperties.title":"Edited"}}]}""",
    };

    private static readonly string Markdown =
        $"# {SampleText}\n\nThe first paragraph of the seed document.\n\n## Second section\n\n- One point\n- Another point\n";

    private static readonly string Text = $"{SampleText}\n\nThe second paragraph of the seed document.\n";

    /// <summary>The text every marker of a rich fixture starts with, and no other text of it holds.</summary>
    public const string MarkerPrefix = "Zqx";

    /// <summary>
    /// Every kind of content a rich fixture can hold a marker in, with the tokens by which a
    /// warning names it: the one table from which the fixtures take their markers and C-R42 the
    /// warnings it expects. Each marker is five letters, the most Aspose.Slides
    /// evaluation mode reads of a text without truncating it, and the whole text of the
    /// paragraph, shape, note or title that holds it.
    /// </summary>
    public static IReadOnlyList<RichKind> Kinds { get; } =
    [
        new("body", MarkerPrefix + "Bd", Content: null),
        new("header", MarkerPrefix + "Hd", Content: "headers-footers"),
        new("footer", MarkerPrefix + "Ft", Content: "headers-footers"),
        new("comment", MarkerPrefix + "Cm", Content: "comments"),
        new("speaker notes", MarkerPrefix + "Nt", Content: "speaker-notes"),
        new("chart title", MarkerPrefix + "Ct", Content: "charts"),
        new("shape text", MarkerPrefix + "Sh", Content: "shapes"),
        new("form field value", MarkerPrefix + "Fv", Content: "form-values"),
    ];

    private static readonly Dictionary<string, RichKind> KindNamed = Kinds.ToDictionary(static kind => kind.Name, StringComparer.Ordinal);

    /// <summary>
    /// The kinds of content each product's rich fixture holds a marker in: each kind the product's
    /// own commands make and its <c>inspect</c>, <c>read</c> or <c>search</c> report.
    /// </summary>
    private static readonly Dictionary<string, string[]> RichKinds = new(StringComparer.Ordinal)
    {
        ["cells"] = ["body", "header", "footer", "comment", "chart title"],
        ["pdf"] = ["body", "header", "footer", "form field value"],
        ["slides"] = ["body", "footer", "speaker notes", "chart title", "shape text"],
        ["words"] = ["body", "header", "footer", "comment"],
    };

    private static readonly ConcurrentDictionary<string, Lazy<byte[]>> Built = new(StringComparer.Ordinal);

    /// <summary>The products that have document fixtures.</summary>
    public static IReadOnlyCollection<string> Products => PrimaryFormats.Keys;

    /// <summary>The format of a product's own fixture document, such as <c>xlsx</c>.</summary>
    public static string PrimaryFormat(string product) => PrimaryFormats[product];

    /// <summary>
    /// The kinds of content a product's <c>&lt;product&gt;.rich</c> fixture holds a marker in,
    /// such as <c>speaker notes</c> with <c>ZqxNt</c>, in a stable order.
    /// </summary>
    public static IReadOnlyList<RichKind> RichMarkers(string product) => [.. RichKinds[product].Select(static kind => KindNamed[kind])];

    /// <summary>The bytes of a named fixture.</summary>
    /// <exception cref="ArgumentException">The name is not a fixture.</exception>
    public static byte[] Read(string name) =>
        Built.GetOrAdd(name, static key => new Lazy<byte[]>(() => Build(key))).Value;

    private static byte[] Build(string name)
    {
        switch (name)
        {
            case "markdown":
                return Encoding.UTF8.GetBytes(Markdown);
            case "text":
                return Encoding.UTF8.GetBytes(Text);
            case "certificate.pfx":
                return Certificate();
        }
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        string product = dot > 0 ? name[..dot] : string.Empty;
        string format = dot > 0 ? name[(dot + 1)..] : string.Empty;
        if (!PrimaryFormats.TryGetValue(product, out string? primary) || format.Length == 0)
        {
            throw new ArgumentException($"'{name}' is not a scenario fixture.", nameof(name));
        }
        return format switch
        {
            "ops" => Encoding.UTF8.GetBytes(Operations[product]),
            "encrypted" => Encrypt(product, primary),
            "media" when product == "slides" => SlidesWithMedia(primary),
            "media" when product == "pdf" => PdfWithMedia(primary),
            "rich" => Rich(product, primary),
            _ when format == primary => CreateDocument(product, primary),
            _ => Convert(product, primary, format),
        };
    }

    private static byte[] CreateDocument(string product, string format)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        File.WriteAllText(workspace.File("source.md"), Markdown);
        File.WriteAllText(workspace.File("source.txt"), Text);
        string file = "seed." + format;
        switch (product)
        {
            case "cells":
                Require(workspace.Run("cells", "create", file, "--output-format", "json"));
                File.WriteAllText(workspace.File("ops.json"),
                    $$$"""{"ops":[{"set":"sheet/Sheet1/range/A1:B3","props":{"value":[["Item","Amount"],["{{{SampleText}}}",1],["Second row",2]]}}]}""");
                Require(workspace.Run("cells", "edit", file, "--ops", "ops.json", "--in-place", "--output-format", "json"));
                break;
            case "pdf":
                Require(workspace.Run("pdf", "create", file, "--from-text", "source.txt", "--output-format", "json"));
                break;
            case "slides":
                (string first, string second) = SlidesDeck(workspace, file);
                File.WriteAllText(workspace.File("ops.json"), $$$"""
                    {"ops":[{"set":"{{{first}}}/placeholder/CenteredTitle","props":{"text":"{{{SampleText}}}"}},
                    {"set":"{{{first}}}/placeholder/Subtitle","props":{"text":"The first paragraph of the seed document."}},
                    {"set":"{{{second}}}/placeholder/Title","props":{"text":"Second section"}},
                    {"set":"{{{second}}}/placeholder/Object","props":{"text":"One point\rAnother point"}}]}
                    """);
                Require(workspace.Run("slides", "edit", file, "--ops", "ops.json", "--in-place", "--output-format", "json"));
                break;
            case "words":
                Require(workspace.Run("words", "convert", "source.md", "--to", format, "--encoding", "utf-8", "--out", file, "--output-format", "json"));
                break;
        }
        return File.ReadAllBytes(workspace.File(file));
    }

    /// <summary>
    /// The engine's new presentation made into two slides with placeholders: one of the layout
    /// whose type is <c>Title</c> (a centered title and a subtitle) and one of <c>TitleAndObject</c>
    /// (a title and an object placeholder), the engine's own blank slide deleted. Returns the
    /// addresses the edit reports for the two slides.
    /// </summary>
    private static (string First, string Second) SlidesDeck(TempWorkspace workspace, string file)
    {
        Require(workspace.Run("slides", "create", file, "--output-format", "json"));
        JsonArray objects = Require(workspace.Run("slides", "read", file, "--kind", "layout", "--kind", "slide", "--output-format", "json"))["objects"]!.AsArray();
        string Layout(string type) => objects
            .First(item => item!["kind"]!.GetValue<string>() == "layout" && item["props"]!["layoutType"]!.GetValue<string>() == type)!["props"]!["slideId"]!.ToJsonString();
        string blank = objects.First(static item => item!["kind"]!.GetValue<string>() == "slide")!["at"]!.GetValue<string>();
        File.WriteAllText(workspace.File("deck.ops.json"),
            $$$"""{"ops":[{"add":"slide","args":{"index":0,"layout":{{{Layout("Title")}}}}},{"add":"slide","args":{"index":1,"layout":{{{Layout("TitleAndObject")}}}}},{"delete":"{{{blank}}}"}]}""");
        JsonArray added = Require(workspace.Run("slides", "edit", file, "--ops", "deck.ops.json", "--in-place", "--output-format", "json"))["ops"]!.AsArray();
        return (added[0]!["at"]!.GetValue<string>(), added[1]!["at"]!.GetValue<string>());
    }

    /// <summary>
    /// The primary presentation with a picture and speaker notes on its first slide, so that
    /// extracting media writes a file: the picture is the slide itself, rendered small.
    /// </summary>
    private static byte[] SlidesWithMedia(string primary)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string source = "seed." + primary;
        File.WriteAllBytes(workspace.File(source), Read($"slides.{primary}"));
        Require(workspace.Run("slides", "render", source, "--slides", "1", "--width", "64", "--out", "picture.png", "--output-format", "json"));
        string first = Require(workspace.Run("slides", "read", source, "--kind", "slide", "--output-format", "json"))["objects"]![0]!["at"]!.GetValue<string>();
        File.WriteAllText(workspace.File("ops.json"), $$$"""
            {"ops":[{"add":"pictureFrame","in":"{{{first}}}","args":{"image":"picture.png","shapeType":"Rectangle","x":10,"y":10,"width":64,"height":36}},
            {"add":"notes","in":"{{{first}}}","props":{"text":"Speaker notes of the seed slide."}}]}
            """);
        Require(workspace.Run("slides", "edit", source, "--ops", "ops.json", "--in-place", "--output-format", "json"));
        return File.ReadAllBytes(workspace.File(source));
    }

    /// <summary>
    /// A PDF with an image and a ruled table on its page and an attached text file, so that
    /// extracting images, tables, attachments, text or form data each writes a file: the image is
    /// the primary PDF's first page, rendered small.
    /// </summary>
    private static byte[] PdfWithMedia(string primary)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string source = "seed." + primary;
        File.WriteAllBytes(workspace.File(source), Read($"pdf.{primary}"));
        Require(workspace.Run("pdf", "render", source, "--pages", "1", "--dpi", "36", "--out", "picture.png", "--output-format", "json"));
        File.WriteAllText(workspace.File("media.html"),
            """<html><body><img src="picture.png" width="60"><table style="border-collapse:collapse"><tr><td style="border:1px solid black">Item</td><td style="border:1px solid black">Amount</td></tr><tr><td style="border:1px solid black">First</td><td style="border:1px solid black">1</td></tr></table></body></html>""");
        string target = "media." + primary;
        Require(workspace.Run("pdf", "create", target, "--from-html", "media.html", "--output-format", "json"));
        File.WriteAllText(workspace.File("note.txt"), SampleText);
        File.WriteAllText(workspace.File("ops.json"), """{"ops":[{"op":"add_attachment","path":"note.txt"}]}""");
        Require(workspace.Run("pdf", "edit", target, "--ops", "ops.json", "--in-place", "--output-format", "json"));
        return File.ReadAllBytes(workspace.File(target));
    }

    /// <summary>
    /// A 40-pixel PNG square that the PDF rich fixture shows, so that every output written from it
    /// has a picture to carry or name: no read reports a picture, so it holds no marker.
    /// </summary>
    private const string RichPicture =
        "iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAIAAAADnC86AAAAMElEQVR4nO3NMQ0AAAgDsCmZfynIwgQJT5P+zbQvIhaLxWKxWCwWi8VisVgsFvcsXhHvWVsvWSOBAAAAAElFTkSuQmCC";

    /// <summary>
    /// A document of the product's primary format with each marker of <see cref="RichMarkers"/> in
    /// the kind of content it names, built with the product's own create and edit: the body text,
    /// and a header, footer, comment, chart title, form field value, slide footer, speaker notes or
    /// shape text where the product makes one. The PDF also shows a picture (<see cref="RichPicture"/>), and
    /// the presentation holds an empty table and a chart, so that every kind of object its edit
    /// addresses has one in the fixture.
    /// </summary>
    private static byte[] Rich(string product, string primary)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string file = "rich." + primary;
        static string Marker(string kind) => KindNamed[kind].Marker;
        string operations;
        switch (product)
        {
            case "cells":
                // The chart's series is added at the address its add reports; the conditional
                // formatting and the pivot table are the first of their sheet.
                Require(workspace.Run("cells", "create", file, "--output-format", "json"));
                File.WriteAllText(workspace.File("content.ops.json"), $$$"""
                    {"ops":[{"set":"sheet/Sheet1/range/A1:B3","props":{"value":[["Item","Amount"],["{{{Marker("body")}}}",1],["Second row",2]]}},
                    {"add":"comment","in":"sheet/Sheet1","args":{"cellName":"B2"},"props":{"note":"{{{Marker("comment")}}}"}},
                    {"set":"sheet/Sheet1/pageSetup","props":{"header.center":"{{{Marker("header")}}}","footer.center":"{{{Marker("footer")}}}"}},
                    {"add":"chart","in":"sheet/Sheet1","args":{"type":"Column","area":"D2:I12"},"props":{"title.text":"{{{Marker("chart title")}}}"}},
                    {"add":"conditionalFormatting","in":"sheet/Sheet1"},
                    {"do":"add_conditional_format_area","args":{"conditionalFormatting":"sheet/Sheet1/conditionalFormatting/0","area":"B2:B3"}},
                    {"add":"rule","in":"sheet/Sheet1/conditionalFormatting/0","args":{"type":"CellValue","operatorType":"GreaterThan","formula1":"1"}},
                    {"add":"pivot","in":"sheet/Sheet1","args":{"sourceData":"=Sheet1!A1:B3","destCellName":"K2","tableName":"Pivot"}},
                    {"do":"add_pivot_field","args":{"pivot":"sheet/Sheet1/pivot/0","fieldType":"Row","fieldName":"Item"}},
                    {"do":"add_pivot_field","args":{"pivot":"sheet/Sheet1/pivot/0","fieldType":"Data","fieldName":"Amount"}},
                    {"do":"calculate_pivot","args":{"pivot":"sheet/Sheet1/pivot/0"}}]}
                    """);
                string sheetChart = Require(workspace.Run("cells", "edit", file, "--ops", "content.ops.json", "--in-place", "--output-format", "json"))["ops"]![3]!["at"]!.GetValue<string>();
                operations = $$$"""
                    [{"add":"series","in":"{{{sheetChart}}}","args":{"area":"Sheet1!B2:B3","isVertical":true}},
                    {"set":"{{{sheetChart}}}","props":{"nSeries.categoryData":"Sheet1!A2:A3"}}]
                    """;
                break;
            case "pdf":
                File.WriteAllBytes(workspace.File("picture.png"), System.Convert.FromBase64String(RichPicture));
                File.WriteAllText(workspace.File("rich.html"),
                    $"""<html><body><h1>Rich seed</h1><p>{Marker("body")}</p><img src="picture.png" width="40"><form><input type="text" name="field" value="seed"></form></body></html>""");
                Require(workspace.Run("pdf", "create", file, "--from-html", "rich.html", "--output-format", "json"));
                operations = $$"""
                    [{"op":"set_form_field","name":"field","value":"{{Marker("form field value")}}"},
                    {"op":"add_header_text","text":"{{Marker("header")}}"},
                    {"op":"add_footer_text","text":"{{Marker("footer")}}"}]
                    """;
                break;
            case "slides":
                // The footer is made visible before its text is set, as showing it copies the
                // layout's placeholder again; the chart's title is set at the address its add reports.
                (string first, string second) = SlidesDeck(workspace, file);
                File.WriteAllText(workspace.File("content.ops.json"), $$$"""
                    {"ops":[{"set":"{{{first}}}/placeholder/CenteredTitle","props":{"text":"Rich seed"}},
                    {"set":"{{{first}}}/placeholder/Subtitle","props":{"text":"{{{Marker("body")}}}"}},
                    {"set":"{{{second}}}/placeholder/Title","props":{"text":"Second slide"}},
                    {"set":"{{{second}}}/placeholder/Object","props":{"text":"One point"}},
                    {"add":"notes","in":"{{{first}}}","props":{"text":"{{{Marker("speaker notes")}}}"}},
                    {"set":"{{{first}}}","props":{"footerVisible":true}},
                    {"set":"{{{first}}}/placeholder/Footer","props":{"text":"{{{Marker("footer")}}}"}},
                    {"add":"autoShape","in":"{{{second}}}","args":{"shapeType":"Rectangle","x":40,"y":300,"width":220,"height":60},"props":{"text":"{{{Marker("shape text")}}}"}},
                    {"add":"table","in":"{{{second}}}","args":{"x":40,"y":380,"columnWidths":[60,60],"rowHeights":[20,20]}},
                    {"add":"chart","in":"{{{second}}}","args":{"type":"ClusteredColumn","x":300,"y":250,"width":320,"height":200},"props":{"hasTitle":true}}]}
                    """);
                string chart = Require(workspace.Run("slides", "edit", file, "--ops", "content.ops.json", "--in-place", "--output-format", "json"))["ops"]![9]!["at"]!.GetValue<string>();
                operations = $$$"""[{"set":"{{{chart}}}/title","props":{"text":"{{{Marker("chart title")}}}"}}]""";
                break;
            case "words":
                // The first-page header and footer, which no evaluation mode writes itself; the
                // comment on the paragraph search finds; a table of one cell and a list, so every
                // kind of the table has an object to hold its children.
                File.WriteAllText(workspace.File("rich.md"), $"# Rich seed\n\n{Marker("body")}\n\nSecond paragraph.\n");
                Require(workspace.Run("words", "convert", "rich.md", "--to", "docx", "--encoding", "utf-8", "--out", file, "--output-format", "json"));
                string anchor = Require(workspace.Run("words", "search", file, "--pattern", "Second paragraph", "--output-format", "json"))["hits"]![0]!["at"]!.GetValue<string>();
                operations = $$$"""
                    [{"set":"section/0","props":{"pageSetup.differentFirstPageHeaderFooter":true}},
                    {"add":"headerFooter","in":"section/0","args":{"headerFooterType":"HeaderFirst"}},
                    {"add":"paragraph","in":"section/0/headerFooter/HeaderFirst"},
                    {"add":"run","in":"section/0/headerFooter/HeaderFirst/paragraph/0","args":{"text":"{{{Marker("header")}}}"}},
                    {"add":"headerFooter","in":"section/0","args":{"headerFooterType":"FooterFirst"}},
                    {"add":"paragraph","in":"section/0/headerFooter/FooterFirst"},
                    {"add":"run","in":"section/0/headerFooter/FooterFirst/paragraph/0","args":{"text":"{{{Marker("footer")}}}"}},
                    {"add":"comment","args":{"paragraph":"{{{anchor}}}","author":"Invariant","initial":"I","dateTime":"2026-01-01T00:00:00"}},
                    {"add":"paragraph","in":"comment/0"},
                    {"add":"run","in":"comment/0/paragraph/0","args":{"text":"{{{Marker("comment")}}}"}},
                    {"add":"table","in":"section/0/body"},
                    {"add":"row","in":"section/0/body/table/0"},
                    {"add":"cell","in":"section/0/body/table/0/row/0"},
                    {"add":"paragraph","in":"section/0/body/table/0/row/0/cell/0"},
                    {"add":"list","args":{"listTemplate":"NumberDefault"}}]
                    """;
                break;
            default:
                throw new ArgumentException($"'{product}' has no rich fixture.", nameof(product));
        }
        File.WriteAllText(workspace.File("ops.json"), $$"""{"ops":{{operations}}}""");
        Require(workspace.Run(product, "edit", file, "--ops", "ops.json", "--in-place", "--output-format", "json"));
        return File.ReadAllBytes(workspace.File(file));
    }

    private static byte[] Convert(string product, string primary, string format)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string source = "seed." + primary;
        File.WriteAllBytes(workspace.File(source), Read($"{product}.{primary}"));
        JsonNode result = Require(workspace.Run(product, "convert", source, "--to", format, "--output-format", "json"));
        string path = (result["output"]?["path"] ?? result["outputs"]![0]!["path"])!.GetValue<string>();
        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// The primary document encrypted with <see cref="Password"/>: a PDF by its <c>encrypt</c>
    /// operation, any other format by <c>convert --encrypt-env</c> to its own format.
    /// </summary>
    private static byte[] Encrypt(string product, string primary)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        string source = "seed." + primary;
        string target = "encrypted." + primary;
        File.WriteAllBytes(workspace.File(source), Read($"{product}.{primary}"));
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal) { [PasswordVariable] = Password };
        string[] args = product == "pdf"
            ? ["pdf", "edit", source, "--ops", "ops.json", "--out", target, "--output-format", "json"]
            : [product, "convert", source, "--to", primary, "--out", target, "--encrypt-env", PasswordVariable, "--output-format", "json"];
        if (product == "pdf")
        {
            File.WriteAllText(workspace.File("ops.json"),
                $$"""{"ops":[{"op":"encrypt_document","userPasswordEnv":"{{PasswordVariable}}","ownerPasswordEnv":"{{PasswordVariable}}"}]}""");
        }
        Require(workspace.RunWithEnv(environment, args));
        return File.ReadAllBytes(workspace.File(target));
    }

    private static JsonNode Require(CliResult result) =>
        result.ExitCode == 0
            ? JsonNode.Parse(result.StdOut)!
            : throw new InvalidOperationException($"A scenario fixture could not be built: exit {result.ExitCode}: {result.StdErr}");

    private static byte[] Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Aspose CLI scenario fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.Export(X509ContentType.Pfx, Password);
    }
}

/// <summary>
/// A kind of content a rich fixture holds a marker in (<see cref="ScenarioFixtures.Kinds"/>), and
/// how a warning names it: <paramref name="Content"/> is the <c>details.content</c> token of a
/// <c>LOSSY_CONVERSION</c> that names it left out of an output (C-R42). A null token is a stated
/// absence, not a gap: no warning names that kind, so losing it is never excused.
/// </summary>
/// <param name="Name">The kind, as violations name it, such as <c>speaker notes</c>.</param>
/// <param name="Marker">The marker text, which starts with <see cref="ScenarioFixtures.MarkerPrefix"/>.</param>
/// <param name="Content">The <c>details.content</c> token that names the kind lost, or null.</param>
public sealed record RichKind(string Name, string Marker, string? Content);
