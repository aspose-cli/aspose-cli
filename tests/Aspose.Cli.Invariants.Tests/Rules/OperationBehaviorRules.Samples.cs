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
    ];
}
