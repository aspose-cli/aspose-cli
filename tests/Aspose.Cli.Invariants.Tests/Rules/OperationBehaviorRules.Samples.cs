using System.Text.Json.Nodes;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// One operation that succeeds on the product's fixture document (<c>ScenarioFixtures</c>), for the
/// rules that run operations: C-R19 (a <c>set_</c> operation applied twice leaves what applying it
/// once leaves, an <c>add_</c> operation applied twice adds twice), C-R21 (the state fields
/// read paths the ops schemas declare read back) and C-R30 (its outcome tells the truth). The schemas say what an operation accepts, not
/// what the fixture holds, so the samples are written by hand against the fixtures: Sheet1 with
/// A1:B3 filled, one PDF page and five Word blocks. Operations that read a file name <c>other.&lt;format&gt;</c> (the fixture
/// again), <c>image.png</c> or <c>note.txt</c>; a secret field names <see cref="RuleWorkspace.SecretVariable"/>.
/// </summary>
/// <param name="Product">The product whose edit applies the operation.</param>
/// <param name="Json">The operation object.</param>
/// <param name="Setup">Operations that first build what the operation acts on, as a JSON array: an
/// edit applies them to the fixture, and the operation then edits that result.</param>
/// <param name="SetupArgs">Options of the setup edit, such as change tracking.</param>
/// <param name="Label">What sets this sample apart from another of the same operation, such as the
/// empty range on which it has nothing to do.</param>
/// <param name="Encrypted">Whether the setup or the operation encrypts the document, so every later
/// command opens it with the secret.</param>
/// <param name="Unseen">Why no comparison sees the operation's change, so C-R30 does not judge
/// whether its output differs from its input.</param>
/// <param name="Unrepeatable">Why the operation cannot be applied twice, so C-R19 does not judge it.</param>
/// <param name="FromHtml">The HTML the product's <c>create --from-html</c> turns into the document
/// the operation edits, instead of the fixture: a PDF with a form field.</param>
internal sealed record OperationSample(
    string Product,
    string Json,
    string? Setup = null,
    string[]? SetupArgs = null,
    string? Label = null,
    bool Encrypted = false,
    string? Unseen = null,
    string? Unrepeatable = null,
    string? FromHtml = null)
{
    /// <summary>The operation name.</summary>
    public string Op { get; } = JsonNode.Parse(Json)!["op"]!.GetValue<string>();

    /// <summary>Whether the operation edits the unedited fixture.</summary>
    public bool OnFixture => Setup is null && FromHtml is null && !Encrypted;

    /// <summary>The case subject: the product, the operation and its label.</summary>
    public string Subject => $"{Product} {Op}{(Label is null ? string.Empty : " on " + Label)}";
}

internal static class OperationSamples
{
    // rule-exemptions: begin
    /// <summary>
    /// Operations of the catalog that have no sample, each with the reason; an entry whose
    /// operation has a sample, or is gone, fails the coverage case.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Exempt { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    // rule-exemptions: end

    /// <summary>One sample per operation of every product, and a labeled one for each case a rule names.</summary>
    public static IReadOnlyList<OperationSample> All { get; } =
    [
        new("pdf", """{"op":"add_attachment","path":"note.txt","name":"notes.txt","description":"Notes","mimeType":"text/plain"}"""),
        new("pdf", """{"op":"add_bookmark","title":"Start","page":1}"""),
        new("pdf", """{"op":"add_footer_text","text":"Footer text"}"""),
        new("pdf", """{"op":"add_header_text","text":"Header text"}"""),
        new("pdf", """{"op":"add_hyperlink","page":1,"rect":{"x":72,"y":72,"width":144,"height":72},"url":"https://example.com/"}""",
            Unseen: "A link annotation shows in no inspect or read result and draws nothing on the page."),
        new("pdf", """{"op":"add_page_numbers"}"""),
        new("pdf", """{"op":"add_stamp_image","page":1,"path":"image.png","rect":{"x":72,"y":72,"width":144,"height":72}}"""),
        new("pdf", """{"op":"add_watermark_image","path":"image.png"}"""),
        new("pdf", """{"op":"add_watermark_text","text":"DRAFT"}"""),
        new("pdf", """{"op":"crop_pages","pages":"1","rect":{"x":10,"y":10,"width":500,"height":700}}"""),
        new("pdf", """{"op":"decrypt_document"}""", Setup: """[{"op":"encrypt_document","userPasswordEnv":"INVARIANT_SECRET","ownerPasswordEnv":"INVARIANT_SECRET"}]""", Encrypted: true),
        new("pdf", """{"op":"delete_bookmarks"}""", Setup: """[{"op":"add_bookmark","title":"Start","page":1}]"""),
        new("pdf", """{"op":"delete_pages","pages":"2"}""", Setup: """[{"op":"insert_page","at":2}]"""),
        new("pdf", """{"op":"encrypt_document","userPasswordEnv":"INVARIANT_SECRET","ownerPasswordEnv":"INVARIANT_SECRET","permissions":{"print":true,"copy":false,"modify":false,"annotate":true,"fillForms":true,"extractAccessibility":true,"assemble":false,"printHighResolution":false}}""", Encrypted: true),
        new("pdf", """{"op":"flatten_forms"}""", Label: "a document without forms"),
        new("pdf", """{"op":"insert_page","at":2,"pageSize":{"preset":"letter"}}"""),
        new("pdf", """{"op":"import_pages","path":"other.pdf","at":2}"""),
        new("pdf", """{"op":"move_pages","pages":"2","to":1}""", Setup: """[{"op":"insert_page","at":2}]"""),
        new("pdf", """{"op":"optimize_document"}""",
            Unseen: "Optimizing changes how the file stores the same content, which no inspect or read result and no rendered page shows."),
        new("pdf", """{"op":"redact_area","page":1,"rect":{"x":72,"y":72,"width":144,"height":72}}"""),
        new("pdf", """{"op":"redact_text","find":"second"}"""),
        new("pdf", """{"op":"delete_attachment","name":"note.txt"}""", Setup: """[{"op":"add_attachment","path":"note.txt"}]"""),
        new("pdf", """{"op":"clear_properties"}"""),
        new("pdf", """{"op":"rotate_pages","pages":"1","rotation":90}"""),
        new("pdf", """{"op":"set_form_field","name":"name","value":"Filled"}""",
            FromHtml: "<html><body><form><input type=\"text\" name=\"name\" value=\"Empty\"></form></body></html>"),
        new("pdf", """{"op":"set_properties","title":"Edited","author":"Tester","subject":"Checks","keywords":"invariant","custom":{"Department":"Sales"}}"""),
        new("pdf", """{"op":"set_page_labels","ranges":[{"startPage":1,"numberStyle":"roman-lower","prefix":"A-","start":3}]}"""),
        new("pdf", """{"op":"set_page_size","pages":"1","pageSize":{"preset":"letter"}}"""),
        new("words", """{"op":"accept_revisions"}""", Setup: """[{"op":"set_text","target":{"block":2},"text":"Revised"}]""", SetupArgs: ["--track-changes", "--author", "Tester"]),
        new("words", """{"op":"add_comment","target":{"block":2},"author":"Tester","text":"Note"}"""),
        new("words", """{"op":"insert_section","at":1,"position":"after","pageSetup":{"pageSize":{"preset":"letter"},"orientation":"landscape","margins":{"top":36,"left":54},"columnCount":2}}"""),
        new("words", """{"op":"set_watermark","text":"DRAFT","faded":false,"color":"#C00000","font":"Arial"}"""),
        new("words", """{"op":"import_sections","path":"other.docx"}"""),
        new("words", """{"op":"set_list","target":{"block":2},"kind":"number","level":2}"""),
        new("words", """{"op":"set_list","target":{"block":2},"kind":"number","numberStyle":"roman-lower","start":3}""", Label: "a number style and a first number"),
        new("words", """{"op":"set_list","target":{"block":2},"kind":"number","numberStyle":"letters-lower","numberFormat":"(%1)"}""", Label: "a parenthesized number format", Setup: """[{"op":"set_list","target":{"block":2},"kind":"number"}]"""),
        new("words", """{"op":"clear_list","target":{"block":2}}""", Setup: """[{"op":"set_list","target":{"block":2},"kind":"bullet"}]"""),
        new("words", """{"op":"define_style","name":"Callout","basedOn":"Normal","font":"Arial","latinFont":"Arial","eastAsianFont":"SimSun","fontSize":14,"bold":true,"color":"#C00000","spaceBefore":6,"spaceAfter":12}"""),
        new("words", """{"op":"delete_blocks","target":{"block":2}}"""),
        new("words", """{"op":"delete_section","section":2}""", Setup: """[{"op":"insert_section","at":1,"position":"after"}]"""),
        new("words", """{"op":"set_table_pagination","target":{"block":3},"keepTogether":false,"allowRowBreakAcrossPages":false,"headerRowCount":1,"keepWithNext":true}""", Setup: """[{"op":"insert_table","at":{"block":2},"position":"after","rowCount":2,"columnCount":2}]"""),
        new("words", """{"op":"set_table_style","target":{"block":3},"tableStyle":"Grid Table 4"}""", Setup: """[{"op":"insert_table","at":{"block":2},"position":"after","rowCount":2,"columnCount":2}]"""),
        new("words", """{"op":"format_text","target":{"block":2},"underline":true,"highlight":"yellow","bold":true,"italic":true,"fontSize":14.5,"color":"#C00000","font":"Arial","latinFont":"Arial","eastAsianFont":"SimSun"}"""),
        new("words", """{"op":"insert_bookmark","target":{"block":2},"name":"Mark"}"""),
        new("words", """{"op":"insert_break","at":{"block":2},"position":"after","kind":"page"}"""),
        new("words", """{"op":"insert_field","at":{"block":2},"position":"after","code":"PAGE"}"""),
        new("words", """{"op":"insert_hyperlink","at":{"block":2},"position":"after","text":"Link","url":"https://example.com/"}"""),
        new("words", """{"op":"insert_image","at":{"block":2},"position":"after","path":"image.png","width":50,"height":40,"inline":false}"""),
        new("words", """{"op":"insert_markdown","at":{"block":2},"position":"after","markdown":"Some **bold** text"}"""),
        new("words", """{"op":"insert_paragraphs","at":{"block":2},"position":"after","paragraphs":[{"text":"Added","style":"Quote","list":{"kind":"number","level":2}}]}"""),
        new("words", """{"op":"insert_table","at":{"block":2},"position":"after","rowCount":2,"columnCount":2,"values":[["a","b"],["c","d"]],"tableStyle":"Table Normal"}"""),
        new("words", """{"op":"insert_toc","at":{"block":1},"position":"before","maxLevel":2}"""),
        new("words", """{"op":"mail_merge","items":[{"Name":"Ann"}]}""", Setup: """[{"op":"insert_field","at":{"block":2},"position":"after","code":"MERGEFIELD Name"}]"""),
        new("words", """{"op":"protect_document","protection":"read-only","passwordEnv":"INVARIANT_SECRET"}"""),
        new("words", """{"op":"reject_revisions"}""", Setup: """[{"op":"set_text","target":{"block":2},"text":"Revised"}]""", SetupArgs: ["--track-changes", "--author", "Tester"]),
        new("words", """{"op":"delete_comments"}""", Setup: """[{"op":"add_comment","target":{"block":2},"author":"Tester","text":"Note"}]"""),
        new("words", """{"op":"delete_watermark"}""", Setup: """[{"op":"set_watermark","text":"DRAFT"}]"""),
        new("words", """{"op":"delete_watermark"}""", Label: "a document without a watermark"),
        new("words", """{"op":"repeat_table_row","target":{"block":3},"row":1,"items":[{"Name":"Ann"},{"Name":"Bob"}]}""", Setup: """[{"op":"insert_table","at":{"block":2},"position":"after","rowCount":1,"columnCount":1,"values":[["{{Name}}"]]}]"""),
        new("words", """{"op":"replace_text","find":"point","replace":"item"}"""),
        new("words", """{"op":"set_default_font","font":"Arial","latinFont":"Arial","eastAsianFont":"SimSun","fontSize":12}"""),
        new("words", """{"op":"set_footer","kind":"primary","paragraphs":[{"text":"Footer","style":"Footer"}]}"""),
        new("words", """{"op":"set_header","kind":"primary","paragraphs":[{"text":"Header","style":"Header"}]}"""),
        new("words", """{"op":"set_page_numbers","location":"header","alignment":"right","text":"Page {n} of {N} pages","numberStyle":"roman-lower","start":3}"""),
        new("words", """{"op":"set_page_setup","pageSize":{"preset":"letter"},"orientation":"landscape","margins":{"top":36,"left":54},"columnCount":2}"""),
        new("words", """{"op":"set_properties","title":"Edited","author":"Tester","subject":"Checks","keywords":"invariant","custom":{"Status":"Final"}}"""),
        new("words", """{"op":"set_style","target":{"block":2},"style":"Quote"}"""),
        new("words", """{"op":"set_table_cell","target":{"block":3},"row":1,"column":1,"text":"X"}""", Setup: """[{"op":"insert_table","at":{"block":2},"position":"after","rowCount":2,"columnCount":2}]"""),
        new("words", """{"op":"set_text","target":{"block":2},"text":"Changed"}"""),
        new("words", """{"op":"unprotect_document","passwordEnv":"INVARIANT_SECRET"}""", Setup: """[{"op":"protect_document","protection":"read-only","passwordEnv":"INVARIANT_SECRET"}]"""),
        new("words", """{"op":"refresh_fields"}"""),
    ];
}
