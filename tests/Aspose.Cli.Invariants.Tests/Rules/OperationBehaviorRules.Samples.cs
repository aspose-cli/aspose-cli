using System.Text.Json.Nodes;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// One operation that succeeds on the product's fixture document (<c>ScenarioFixtures</c>), for the
/// rules that run operations: C-R19 (a <c>set_</c> operation applied twice leaves what applying it
/// once leaves, an <c>add_</c> operation applied twice adds twice), C-R21 (the state fields
/// read paths the ops schemas declare read back) and C-R30 (its outcome tells the truth). The schemas say what an operation accepts, not
/// what the fixture holds, so the samples are written by hand against the fixtures: Sheet1 with
/// A1:B3 filled, one PDF page, two slides whose second has a title (shape 2) and a body (shape 3),
/// and five Word blocks. Operations that read a file name <c>other.&lt;format&gt;</c> (the fixture
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
/// <param name="Shape">The type of the shape the setup added on the operation's slide, which the
/// operation addresses: its <c>shapeId</c> is read after the setup, because an evaluation run adds
/// a shape of its own to every slide it saves. Slide texts stay within the five characters an
/// evaluation run reads back.</param>
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
    string? Shape = null,
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
    /// <summary>
    /// Operations of the catalog that have no sample, each with the reason; an entry whose
    /// operation has a sample, or is gone, fails the coverage case.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Exempt { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>One sample per operation of every product, and a labeled one for each case a rule names.</summary>
    public static IReadOnlyList<OperationSample> All { get; } =
    [
        new("cells", """{"op":"add_comment","sheet":"Sheet1","cell":"A1","text":"Note","author":"Tester"}"""),
        new("cells", """{"op":"add_conditional_format","sheet":"Sheet1","range":"B2:B3","rule":{"kind":"cellValue","operator":"greaterThan","value1":"1"},"style":{"bold":true}}"""),
        new("cells", """{"op":"add_sheet","name":"Extra","position":0}""",
            Unrepeatable: "A second sheet of the same name is refused."),
        new("cells", """{"op":"add_sparkline","sheet":"Sheet1","dataRange":"Sheet1!B2:B3","location":"C2"}"""),
        new("cells", """{"op":"clear_conditional_formats","sheet":"Sheet1","range":"B2:B3"}""", Setup: """[{"op":"add_conditional_format","sheet":"Sheet1","range":"B2:B3","rule":{"kind":"cellValue","operator":"greaterThan","value1":"1"},"style":{"bold":true}}]"""),
        new("cells", """{"op":"clear_range","sheet":"Sheet1","range":"A2:B2"}"""),
        new("cells", """{"op":"clear_range","sheet":"Sheet1","range":"H1:K1"}""", Label: "an empty range"),
        new("cells", """{"op":"clear_validation","sheet":"Sheet1","range":"B2:B3"}""", Setup: """[{"op":"set_validation","sheet":"Sheet1","range":"B2:B3","type":"wholeNumber","operator":"between","value1":"0","value2":"10"}]"""),
        new("cells", """{"op":"clear_validation","sheet":"Sheet1","range":"H1:K1"}""", Label: "an empty range"),
        new("cells", """{"op":"copy_range","sheet":"Sheet1","from":"A1:B1","to":"D1"}"""),
        new("cells", """{"op":"create_chart","sheet":"Sheet1","type":"column","dataRange":"A1:B3","at":"D2:H12","title":"Totals"}"""),
        new("cells", """{"op":"create_pivot","sheet":"Sheet1","sourceRange":"Sheet1!A1:B3","at":"E2","name":"Pivot1","rows":["Item"],"values":[{"field":"Amount"}]}"""),
        new("cells", """{"op":"create_table","sheet":"Sheet1","range":"A1:B3","name":"Items"}"""),
        new("cells", """{"op":"define_name","name":"Total","refersTo":"=Sheet1!$B$2:$B$3"}"""),
        new("cells", """{"op":"delete_chart","sheet":"Sheet1","index":0}""", Setup: """[{"op":"create_chart","sheet":"Sheet1","type":"column","dataRange":"A1:B3","at":"D2:H12"}]"""),
        new("cells", """{"op":"delete_columns","sheet":"Sheet1","at":"B"}"""),
        new("cells", """{"op":"delete_comment","sheet":"Sheet1","cell":"A1"}""", Setup: """[{"op":"add_comment","sheet":"Sheet1","cell":"A1","text":"Note"}]"""),
        new("cells", """{"op":"delete_name","name":"Total"}""", Setup: """[{"op":"define_name","name":"Total","refersTo":"=Sheet1!$B$2:$B$3"}]"""),
        new("cells", """{"op":"delete_rows","sheet":"Sheet1","at":3}"""),
        new("cells", """{"op":"delete_sheet","sheet":"Extra"}""", Setup: """[{"op":"add_sheet","name":"Extra"}]"""),
        new("cells", """{"op":"edit_comment","sheet":"Sheet1","cell":"A1","text":"Changed","author":"Editor"}""", Setup: """[{"op":"add_comment","sheet":"Sheet1","cell":"A1","text":"Note","author":"Tester"}]"""),
        new("cells", """{"op":"format_range","sheet":"Sheet1","range":"A1:B1","style":{"font":"Calibri","size":14,"bold":true,"italic":true,"color":"#C00000","bg":"#FFFF00","numberFormat":"0.00","hAlign":"left","vAlign":"top","wrap":true,"underline":true,"strikethrough":true,"indent":1,"locked":false}}"""),
        new("cells", """{"op":"freeze_panes","sheet":"Sheet1","cell":"A2"}"""),
        new("cells", """{"op":"group_columns","sheet":"Sheet1","from":"A","to":"B","collapse":true}"""),
        new("cells", """{"op":"group_rows","sheet":"Sheet1","from":2,"to":3,"collapse":true}"""),
        new("cells", """{"op":"import_range","sheet":"Sheet1","path":"other.xlsx","from":"Sheet1!A1:B3","to":"D1"}"""),
        new("cells", """{"op":"import_sheet","path":"other.xlsx","name":"Imported","position":0}"""),
        new("cells", """{"op":"insert_columns","sheet":"Sheet1","at":"B"}"""),
        new("cells", """{"op":"insert_image","sheet":"Sheet1","path":"image.png","at":"D2"}"""),
        new("cells", """{"op":"insert_rows","sheet":"Sheet1","at":2}"""),
        new("cells", """{"op":"merge_cells","sheet":"Sheet1","range":"D1:E2"}"""),
        new("cells", """{"op":"move_sheet","sheet":"Extra","position":0}""", Setup: """[{"op":"add_sheet","name":"Extra"}]"""),
        new("cells", """{"op":"protect_sheet","sheet":"Sheet1","passwordEnv":"INVARIANT_SECRET"}"""),
        new("cells", """{"op":"protect_workbook","passwordEnv":"INVARIANT_SECRET"}"""),
        new("cells", """{"op":"refresh_pivot","sheet":"Sheet1","name":"Pivot1"}""", Setup: """[{"op":"create_pivot","sheet":"Sheet1","sourceRange":"Sheet1!A1:B3","at":"E2","name":"Pivot1","rows":["Item"],"values":[{"field":"Amount"}]}]"""),
        new("cells", """{"op":"remove_duplicates","sheet":"Sheet1","range":"A1:B4","hasHeader":true}""", Setup: """[{"op":"set_values","sheet":"Sheet1","range":"A4","values":[["Second row",2]]}]"""),
        new("cells", """{"op":"remove_hyperlink","sheet":"Sheet1","cell":"A1"}""", Setup: """[{"op":"set_hyperlink","sheet":"Sheet1","cell":"A1","url":"https://example.com/"}]"""),
        new("cells", """{"op":"rename_sheet","sheet":"Sheet1","to":"Data"}"""),
        new("cells", """{"op":"resize_columns","sheet":"Sheet1","from":"A","width":30}"""),
        new("cells", """{"op":"resize_rows","sheet":"Sheet1","from":1,"height":30}"""),
        new("cells", """{"op":"set_active_sheet","sheet":"Extra"}""", Setup: """[{"op":"add_sheet","name":"Extra"}]"""),
        new("cells", """{"op":"set_autofilter","sheet":"Sheet1","range":"A1:B3"}"""),
        new("cells", """{"op":"set_borders","sheet":"Sheet1","range":"A1:B3","edges":["all"],"style":"thin"}"""),
        new("cells", """{"op":"set_default_font","font":"Calibri","size":12}"""),
        new("cells", """{"op":"set_formula","sheet":"Sheet1","range":"C2","formula":"=B2*2"}"""),
        new("cells", """{"op":"set_hyperlink","sheet":"Sheet1","cell":"A1","url":"https://example.com/"}"""),
        new("cells", """{"op":"set_page_setup","sheet":"Sheet1","orientation":"landscape","paperSize":"a4","margins":{"top":1,"bottom":1,"left":0.5,"right":0.5},"fitToWidth":1,"fitToHeight":2,"header":"Head","footer":"Foot"}"""),
        new("cells", """{"op":"set_page_setup","sheet":"Sheet1","scale":80}""", Label: "a scale"),
        new("cells", """{"op":"set_print_area","sheet":"Sheet1","range":"A1:B3","titleRows":"1:1","titleColumns":"A:A"}"""),
        new("cells", """{"op":"set_sheet_view","sheet":"Sheet1","zoom":150,"gridlines":false}"""),
        new("cells", """{"op":"set_sheet_visibility","sheet":"Extra","hidden":true}""", Setup: """[{"op":"add_sheet","name":"Extra"}]"""),
        new("cells", """{"op":"set_tab_color","sheet":"Sheet1","color":"#FF0000"}"""),
        new("cells", """{"op":"set_validation","sheet":"Sheet1","range":"B2:B3","type":"wholeNumber","operator":"between","value1":"0","value2":"10"}"""),
        new("cells", """{"op":"set_values","sheet":"Sheet1","range":"D1","values":[["edited"]]}"""),
        new("cells", """{"op":"sort_range","sheet":"Sheet1","range":"A1:B3","by":[{"column":"B","order":"desc"}],"hasHeader":true}"""),
        new("cells", """{"op":"ungroup_columns","sheet":"Sheet1","from":"A","to":"B"}""", Setup: """[{"op":"group_columns","sheet":"Sheet1","from":"A","to":"B"}]"""),
        new("cells", """{"op":"ungroup_rows","sheet":"Sheet1","from":2,"to":3}""", Setup: """[{"op":"group_rows","sheet":"Sheet1","from":2,"to":3}]"""),
        new("cells", """{"op":"unmerge_cells","sheet":"Sheet1","range":"D1:E2"}""", Setup: """[{"op":"merge_cells","sheet":"Sheet1","range":"D1:E2"}]"""),
        new("cells", """{"op":"unprotect_sheet","sheet":"Sheet1","passwordEnv":"INVARIANT_SECRET"}""", Setup: """[{"op":"protect_sheet","sheet":"Sheet1","passwordEnv":"INVARIANT_SECRET"}]"""),
        new("cells", """{"op":"unprotect_workbook","passwordEnv":"INVARIANT_SECRET"}""", Setup: """[{"op":"protect_workbook","passwordEnv":"INVARIANT_SECRET"}]"""),
        new("cells", """{"op":"update_chart","sheet":"Sheet1","index":0,"title":"Renamed","type":"line"}""", Setup: """[{"op":"create_chart","sheet":"Sheet1","type":"column","dataRange":"A1:B3","at":"D2:H12","title":"Totals"}]"""),
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
        new("pdf", """{"op":"set_page_labels","ranges":[{"startPage":1,"numberStyle":"roman-lower","prefix":"A-","startingValue":3}]}"""),
        new("pdf", """{"op":"set_page_size","pages":"1","pageSize":{"preset":"letter"}}"""),
        new("slides", """{"op":"insert_section","name":"Intro","at":1}""",
            Unrepeatable: "A second section of the same name is refused."),
        new("slides", """{"op":"insert_slide","layout":"Title Only"}"""),
        new("slides", """{"op":"import_slides","path":"other.pptx"}"""),
        new("slides", """{"op":"set_layout","slides":"2","layout":"Title Only"}"""),
        new("slides", """{"op":"delete_shape","slide":2,"shapeId":3}"""),
        new("slides", """{"op":"delete_slides","slides":"2"}"""),
        new("slides", """{"op":"copy_slide","slide":1}"""),
        new("slides", """{"op":"insert_chart","slide":2,"kind":"column","rect":{"x":72,"y":72,"width":144,"height":72},"categories":["A","B"],"series":[{"name":"Sales","values":[1,2]}],"title":"Plot"}"""),
        new("slides", """{"op":"insert_image","slide":2,"path":"image.png","rect":{"x":72,"y":72,"width":144,"height":72},"altText":"Logo"}"""),
        new("slides", """{"op":"insert_shape","slide":2,"geometry":"rectangle","rect":{"x":72,"y":72,"width":144,"height":72},"text":"Box","style":{"fillColor":"#FF0000","line":"#00FF00","bold":true,"color":"#123456","eastAsianFont":"SimSun","font":"Arial","latinFont":"Arial","fontSize":20}}"""),
        new("slides", """{"op":"insert_table","slide":2,"rect":{"x":72,"y":72,"width":144,"height":72},"rowCount":2,"columnCount":2,"values":[["a","b"],["c","d"]]}"""),
        new("slides", """{"op":"move_slide","slide":2,"to":1}"""),
        new("slides", """{"op":"replace_text","find":"Second","replace":"Next"}"""),
        new("slides", """{"op":"set_background","slides":"1","color":"#336699"}"""),
        new("slides", """{"op":"set_body","slide":2,"paragraphs":[{"text":"First"},{"text":"Inner","list":{"kind":"number","level":2}}]}"""),
        new("slides", """{"op":"set_footer","slides":"1-2","text":"Foot","showNumber":true,"showDate":true}"""),
        new("slides", """{"op":"set_notes","slide":1,"text":"Notes"}"""),
        new("slides", """{"op":"set_properties","title":"Edited","author":"Tester","subject":"Subj","keywords":"Keys","company":"Corp"}"""),
        new("slides", """{"op":"set_shape_bounds","slide":2,"shapeId":3,"x":50,"y":60,"width":600,"height":200}"""),
        new("slides", """{"op":"format_shape","slide":2,"shapeId":3,"style":{"fillColor":"#FF0000","line":"#00FF00","bold":true,"color":"#123456","eastAsianFont":"SimSun","font":"Arial","latinFont":"Arial","fontSize":22}}"""),
        new("slides", """{"op":"set_slide_hidden","slides":"2","hidden":true}"""),
        new("slides", """{"op":"set_slide_size","slideSize":{"preset":"4x3"}}"""),
        new("slides", """{"op":"set_table_cell","slide":2,"shapeId":4,"row":1,"column":1,"text":"X"}""", Setup: """[{"op":"insert_table","slide":2,"rect":{"x":72,"y":72,"width":144,"height":72},"rowCount":2,"columnCount":2}]""", Shape: "table"),
        new("slides", """{"op":"set_text","slide":2,"shapeId":3,"text":"Done"}"""),
        new("slides", """{"op":"set_title","slide":1,"text":"Head"}"""),
        new("slides", """{"op":"set_transition","slides":"1","kind":"fade","durationMs":500}"""),
        new("slides", """{"op":"set_chart_data","slide":2,"shapeId":4,"categories":["X","Y"],"series":[{"name":"Sales","values":[3,4]}]}""", Setup: """[{"op":"insert_chart","slide":2,"kind":"column","rect":{"x":72,"y":72,"width":144,"height":72},"categories":["A","B"],"series":[{"name":"Sales","values":[1,2]}]}]""", Shape: "chart"),
        new("words", """{"op":"accept_revisions"}""", Setup: """[{"op":"set_text","target":{"block":2},"text":"Revised"}]""", SetupArgs: ["--track-changes", "--author", "Tester"]),
        new("words", """{"op":"add_comment","target":{"block":2},"author":"Tester","text":"Note"}"""),
        new("words", """{"op":"insert_section","at":1,"position":"after","pageSetup":{"pageSize":{"preset":"letter"},"orientation":"landscape","margins":{"top":36,"left":54}}}"""),
        new("words", """{"op":"set_watermark","text":"DRAFT"}"""),
        new("words", """{"op":"import_sections","path":"other.docx"}"""),
        new("words", """{"op":"set_list","target":{"block":2},"kind":"number"}"""),
        new("words", """{"op":"define_style","name":"Callout","basedOn":"Normal","fontSize":14,"bold":true}"""),
        new("words", """{"op":"delete_blocks","target":{"block":2}}"""),
        new("words", """{"op":"delete_section","section":2}""", Setup: """[{"op":"insert_section","at":1,"position":"after"}]"""),
        new("words", """{"op":"set_table_pagination","target":{"block":3},"headerRowCount":1}""", Setup: """[{"op":"insert_table","at":{"block":2},"position":"after","rowCount":2,"columnCount":2}]"""),
        new("words", """{"op":"format_text","target":{"block":2},"underline":true,"highlight":"#FFFF00","bold":true,"italic":true,"fontSize":14.5,"color":"#C00000","font":"Arial","latinFont":"Arial","eastAsianFont":"SimSun"}"""),
        new("words", """{"op":"insert_bookmark","at":{"block":2},"name":"Mark"}"""),
        new("words", """{"op":"insert_break","at":{"block":2},"position":"after","kind":"page"}"""),
        new("words", """{"op":"insert_field","at":{"block":2},"position":"after","code":"PAGE"}"""),
        new("words", """{"op":"insert_hyperlink","at":{"block":2},"position":"after","text":"Link","url":"https://example.com/"}"""),
        new("words", """{"op":"insert_image","at":{"block":2},"position":"after","path":"image.png","width":50,"height":40}"""),
        new("words", """{"op":"insert_markdown","at":{"block":2},"position":"after","markdown":"Some **bold** text"}"""),
        new("words", """{"op":"insert_paragraphs","at":{"block":2},"position":"after","paragraphs":[{"text":"Added","style":"Quote"}]}"""),
        new("words", """{"op":"insert_table","at":{"block":2},"position":"after","rowCount":2,"columnCount":2,"values":[["a","b"],["c","d"]],"tableStyle":"Table Normal"}"""),
        new("words", """{"op":"insert_toc","at":{"block":1},"position":"before"}"""),
        new("words", """{"op":"mail_merge","items":[{"Name":"Ann"}]}""", Setup: """[{"op":"insert_field","at":{"block":2},"position":"after","code":"MERGEFIELD Name"}]"""),
        new("words", """{"op":"protect_document","protection":"read-only","passwordEnv":"INVARIANT_SECRET"}"""),
        new("words", """{"op":"reject_revisions"}""", Setup: """[{"op":"set_text","target":{"block":2},"text":"Revised"}]""", SetupArgs: ["--track-changes", "--author", "Tester"]),
        new("words", """{"op":"delete_comments"}""", Setup: """[{"op":"add_comment","target":{"block":2},"author":"Tester","text":"Note"}]"""),
        new("words", """{"op":"delete_watermark"}""", Setup: """[{"op":"set_watermark","text":"DRAFT"}]"""),
        new("words", """{"op":"delete_watermark"}""", Label: "a document without a watermark"),
        new("words", """{"op":"repeat_table_row","at":{"block":3},"row":1,"items":[{"Name":"Ann"},{"Name":"Bob"}]}""", Setup: """[{"op":"insert_table","at":{"block":2},"position":"after","rowCount":1,"columnCount":1,"values":[["{{Name}}"]]}]"""),
        new("words", """{"op":"replace_text","find":"point","replace":"item"}"""),
        new("words", """{"op":"set_default_font","font":"Arial","fontSize":12}"""),
        new("words", """{"op":"set_footer","kind":"primary","paragraphs":["Footer"]}"""),
        new("words", """{"op":"set_header","kind":"primary","paragraphs":["Header"]}"""),
        new("words", """{"op":"set_page_numbers","location":"header","alignment":"right"}"""),
        new("words", """{"op":"set_page_setup","pageSize":{"preset":"letter"},"orientation":"landscape","margins":{"top":36,"left":54}}"""),
        new("words", """{"op":"set_properties","title":"Edited","author":"Tester","subject":"Checks","keywords":"invariant"}"""),
        new("words", """{"op":"set_style","target":{"block":2},"style":"Quote"}"""),
        new("words", """{"op":"set_table_cell","target":{"block":3},"row":1,"column":1,"text":"X"}""", Setup: """[{"op":"insert_table","at":{"block":2},"position":"after","rowCount":2,"columnCount":2}]"""),
        new("words", """{"op":"set_text","target":{"block":2},"text":"Changed"}"""),
        new("words", """{"op":"unprotect_document","passwordEnv":"INVARIANT_SECRET"}""", Setup: """[{"op":"protect_document","protection":"read-only","passwordEnv":"INVARIANT_SECRET"}]"""),
        new("words", """{"op":"refresh_fields"}"""),
    ];
}
