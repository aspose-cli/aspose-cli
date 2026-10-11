using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// C-R21 (D9): a state an operation writes reads back under the same name and value domain. Which
// op fields are state is a human decision the schemas cannot make, so it is kept here by hand:
// every op field is a state field with its read path, a state field no read reports (null), a
// state read back by position rather than by name, or not state (an address, a source, a
// selector or a parameter of the action). A field in none of these is reported unclassified.
internal static partial class ContractRules
{
    private const string CellsInfo = "v2/cells/workbook-info";
    private const string CellsRead = "v2/cells/workbook-read";
    private const string PdfInfo = "v2/pdf/pdf-info";
    private const string SlidesInfo = "v2/slides/presentation-info";
    private const string SlidesRead = "v2/slides/presentation-read";
    private const string WordsInfo = "v2/words/document-info";
    private const string WordsRead = "v2/words/document-read";

    private static readonly string[] CellsStyleNames =
        ["font", "size", "bold", "italic", "color", "bg", "numberFormat", "hAlign", "vAlign", "wrap", "underline", "strikethrough", "indent", "locked"];

    private static readonly string[] SlidesShapeStyleRead = ["bold", "color", "eastAsianFont", "font", "size"];
    private static readonly string[] SlidesShapeStyleUnread = ["fill", "latinFont", "line"];

    /// <summary>
    /// State fields by <c>&lt;product&gt; &lt;op&gt;.&lt;field&gt;[.&lt;field&gt;]</c>, each with
    /// its read path <c>&lt;schema id&gt;#&lt;definition&gt;/&lt;property&gt;</c> (a deeper property
    /// after another slash, an empty definition for the root), or null when no read reports it. A
    /// nested key classifies the fields of an object or list field one by one.
    /// </summary>
    private static readonly Dictionary<string, string?> StateFields = BuildStateFields();

    /// <summary>Op fields that are not state: selectors, sources and parameters of the action.</summary>
    private static readonly HashSet<string> NotStateFields = BuildNotStateFields();

    /// <summary>
    /// State that reads back by position rather than under a field of its own name, which the rule
    /// accepts as reading back; not judged further.
    /// </summary>
    private static readonly Dictionary<string, string> PositionalReadBack = new(StringComparer.Ordinal)
    {
        ["cells set_formula.formula"] = "reads back as the f of the compact cells (kept on purpose)",
        ["cells set_values.values"] = "reads back as the v of the compact cells (kept on purpose)",
        ["words insert_bookmark.name"] = "reads back as an entry of the document's bookmarks list",
        ["words set_table_cell.text"] = "reads back as cells[row][col] of the table block",
    };

    /// <summary>The names that address a target, an anchor or a source in each product's ops, not state, unless the table above classifies the field.</summary>
    private static readonly Dictionary<string, HashSet<string>> AddressNames = new(StringComparer.Ordinal)
    {
        ["cells"] = new(StringComparer.Ordinal) { "sheet", "range", "cell", "at", "from", "to", "index", "path", "passwordEnv" },
        ["pdf"] = new(StringComparer.Ordinal) { "page", "pages", "at", "path", "passwordEnv" },
        ["slides"] = new(StringComparer.Ordinal) { "slide", "slideId", "slides", "shapeId", "shapeName", "placeholder", "at", "path" },
        ["words"] = new(StringComparer.Ordinal) { "at", "position", "target", "section", "path", "passwordEnv" },
    };

    private static Dictionary<string, string?> BuildStateFields()
    {
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal);

        // Fields that read back under the same name in one read definition.
        void Read(string op, string definition, params string[] names)
        {
            foreach (string name in names)
            {
                fields.Add($"{op}.{name}", $"{definition}/{name}");
            }
        }

        void ReadAs(string field, string path) => fields.Add(field, path);

        void Unread(string op, params string[] names)
        {
            foreach (string name in names)
            {
                fields.Add($"{op}.{name}", null);
            }
        }

        Read("cells add_sheet", $"{CellsInfo}#sheetInfo", "name", "position");
        Read("cells import_sheet", $"{CellsInfo}#sheetInfo", "name", "position");
        Read("cells move_sheet", $"{CellsInfo}#sheetInfo", "position");
        ReadAs("cells rename_sheet.to", $"{CellsInfo}#sheetInfo/name");
        Read("cells set_sheet_visibility", $"{CellsInfo}#sheetInfo", "hidden");
        ReadAs("cells freeze_panes.cell", $"{CellsInfo}#sheetLayoutInfo/freezePanes");
        ReadAs("cells set_autofilter.range", $"{CellsInfo}#sheetLayoutInfo/autoFilter");
        Read("cells set_page_setup", $"{CellsInfo}#sheetLayoutInfo", "orientation", "fitToWidth", "fitToHeight", "scale", "header", "footer");
        Unread("cells set_page_setup", "paperSize", "margins");
        ReadAs("cells set_print_area.range", $"{CellsInfo}#sheetLayoutInfo/printArea");
        Read("cells set_print_area", $"{CellsInfo}#sheetLayoutInfo", "titleRows", "titleColumns");
        ReadAs("cells group_rows.collapse", $"{CellsInfo}#rowGroupInfo/collapsed");
        ReadAs("cells group_columns.collapse", $"{CellsInfo}#columnGroupInfo/collapsed");
        Read("cells define_name", $"{CellsInfo}#definedNameInfo", "name", "refersTo");
        Read("cells create_table", $"{CellsInfo}#tableInfo", "name");
        Unread("cells create_table", "style", "totalsRow");
        Read("cells create_pivot", $"{CellsInfo}#pivotInfo", "name");
        Unread("cells create_pivot", "sourceRange", "rows", "columns", "values", "captions");
        ReadAs("cells create_chart.type", $"{CellsInfo}#chartInfo/type");
        Unread("cells create_chart", "dataRange", "title", "seriesInRows", "legend", "axisTitles", "seriesColors", "dataLabels");
        ReadAs("cells update_chart.type", $"{CellsInfo}#chartInfo/type");
        Unread("cells update_chart", "dataRange", "title", "seriesInRows", "legend", "axisTitles", "seriesColors", "dataLabels");
        ReadAs("cells set_validation.type", $"{CellsInfo}#validationInfo/type");
        Unread("cells set_validation", "operator", "value1", "value2", "listItems", "listSource", "inputMessage", "errorMessage", "allowBlank");
        Read("cells format_range.style", $"{CellsRead}#styleData", CellsStyleNames);
        Unread("cells add_comment", "text", "author");
        Unread("cells edit_comment", "text", "author");
        Unread("cells set_borders", "edges", "style", "color");
        Unread("cells set_sheet_view", "gridlines", "zoom", "headings");
        Unread("cells insert_image", "width", "height");
        Unread("cells resize_columns", "width");
        Unread("cells resize_rows", "height");
        Unread("cells set_hyperlink", "url", "target", "display");
        Unread("cells set_tab_color", "color");
        Unread("cells add_sparkline", "dataRange", "location", "type", "color");
        Unread("cells set_default_font", "font", "size");
        Unread("cells add_conditional_format", "rule", "style");
        Unread("cells protect_sheet", "allow");

        ReadAs("pdf insert_blank_page.size", $"{PdfInfo}#pdfPageInfo/widthPoints");
        ReadAs("pdf set_page_size.size", $"{PdfInfo}#pdfPageInfo/widthPoints");
        ReadAs("pdf rotate_pages.angle", $"{PdfInfo}#pdfPageInfo/rotation");
        ReadAs("pdf crop_pages.rect", $"{PdfInfo}#pdfPageInfo/cropBox");
        Read("pdf add_bookmark", $"{PdfInfo}#pdfOutlineItem", "title", "page");
        Read("pdf add_attachment", $"{PdfInfo}#pdfAttachmentInfo", "name", "mimeType");
        Unread("pdf add_attachment", "description");
        Read("pdf set_page_labels.ranges", $"{PdfInfo}#pdfPageLabelInfo", "startPage", "style", "prefix", "startingValue");
        Read("pdf encrypt.permissions", $"{PdfInfo}#pdfPermissionInfo",
            "print", "copy", "modify", "annotate", "fillForms", "extractAccessibility", "assemble", "printHighResolution");
        Read("pdf set_form_field", "v2/pdf/form-result#pdfFormField", "value");
        Unread("pdf add_watermark_text", "text", "font", "size", "color", "opacity", "rotation", "layer");
        Unread("pdf add_watermark_image", "opacity", "scale", "layer");
        Unread("pdf add_page_numbers", "format", "position", "start", "font");
        Unread("pdf add_header_text", "text", "position", "font");
        Unread("pdf add_footer_text", "text", "position", "font");
        Unread("pdf add_stamp_image", "rect");
        Unread("pdf add_link", "rect", "url");
        Unread("pdf set_metadata", "title", "author", "subject", "keywords", "custom");

        Read("slides set_shape_style.style", $"{SlidesRead}#slideTextRunData", SlidesShapeStyleRead);
        Unread("slides set_shape_style.style", SlidesShapeStyleUnread);
        Read("slides insert_shape.style", $"{SlidesRead}#slideTextRunData", SlidesShapeStyleRead);
        Unread("slides insert_shape.style", SlidesShapeStyleUnread);
        Read("slides insert_shape", $"{SlidesRead}#slideShapeData", "rect", "text");
        Unread("slides insert_shape", "kind");
        Read("slides insert_table", $"{SlidesRead}#slideShapeData", "rect");
        Unread("slides insert_table", "rowCount", "columnCount", "data");
        Read("slides insert_image", $"{SlidesRead}#slideShapeData", "rect", "altText");
        Read("slides insert_chart", $"{SlidesRead}#slideShapeData", "rect");
        Unread("slides insert_chart", "kind", "categories", "series", "title");
        Read("slides set_text", $"{SlidesRead}#slideShapeData", "text");
        Read("slides set_body.paragraphs", $"{SlidesRead}#slideShapeData", "text");
        Unread("slides set_body.paragraphs", "level", "bullet");
        Read("slides set_shape_bounds", $"{SlidesRead}#slideRect", "x", "y", "width", "height");
        ReadAs("slides set_title.text", $"{SlidesInfo}#slideInfo/title");
        ReadAs("slides set_notes.text", $"{SlidesRead}#slideData/notes");
        Read("slides add_slide", $"{SlidesInfo}#slideInfo", "layout");
        Read("slides apply_layout", $"{SlidesInfo}#slideInfo", "layout");
        Read("slides set_slide_hidden", $"{SlidesInfo}#slideInfo", "hidden");
        Read("slides add_section", $"{SlidesInfo}#presentationSectionInfo", "name", "startSlide");
        ReadAs("slides set_slide_size.size", $"{SlidesInfo}#presentationSummary/widthPoints");
        Unread("slides set_table_cell", "text");
        Unread("slides set_transition", "kind", "durationMs");
        Unread("slides update_chart_data", "categories", "series");
        Unread("slides set_footer", "text", "showNumber", "showDate");
        Unread("slides set_background", "color", "imagePath");
        Unread("slides set_properties", "title", "author", "subject", "keywords", "company");

        Read("words format_text", $"{WordsRead}#runData", "bold", "italic", "size", "color", "font", "latinFont", "eastAsianFont");
        Unread("words format_text", "underline", "highlight");
        ReadAs("words insert_hyperlink.text", $"{WordsRead}#runData/text");
        Unread("words insert_hyperlink", "url");
        Read("words set_text", $"{WordsRead}#blockData", "text");
        Read("words set_style", $"{WordsRead}#blockData", "style");
        Read("words insert_paragraphs.paragraphs", $"{WordsRead}#blockData", "text", "style");
        Unread("words insert_paragraphs.paragraphs", "listLevel");
        Read("words insert_table", $"{WordsRead}#blockData", "rowCount", "columnCount", "cells");
        ReadAs("words insert_table.style", $"{WordsInfo}#tableData/style");
        Read("words insert_image", $"{WordsRead}#blockImageData", "width", "height");
        Unread("words insert_image", "inline");
        ReadAs("words insert_break.kind", $"{WordsRead}#blockData/breakAfter");
        Read("words insert_field", $"{WordsInfo}#fieldData", "code");
        Read("words add_comment", $"{WordsInfo}#commentData", "author", "text");
        Read("words set_header", $"{WordsInfo}#headerFooterData", "kind", "paragraphs");
        Read("words set_footer", $"{WordsInfo}#headerFooterData", "kind", "paragraphs");
        ReadAs("words protect.mode", $"{WordsInfo}#documentSummary/protection");
        foreach (string op in new[] { "words set_page_setup.setup", "words add_section.pageSetup" })
        {
            ReadAs($"{op}.size", $"{WordsInfo}#sectionData/widthPoints");
            Read(op, $"{WordsInfo}#sectionData", "orientation", "margins");
            Unread(op, "columnCount");
        }
        Unread("words add_watermark", "text", "imagePath", "faded", "color", "font");
        Unread("words define_style", "basedOn", "font", "latinFont", "eastAsianFont", "size", "bold", "color", "spaceBefore", "spaceAfter");
        Unread("words insert_toc", "maxLevel");
        Unread("words apply_list", "kind", "level");
        Unread("words set_default_font", "font", "latinFont", "eastAsianFont", "size");
        Unread("words set_properties", "title", "author", "subject", "keywords", "custom");
        Unread("words format_table", "keepTogether", "allowRowBreakAcrossPages", "headerRowCount", "keepWithNext");
        Unread("words set_page_numbers", "location", "alignment", "format", "start");
        return fields;
    }

    private static HashSet<string> BuildNotStateFields()
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);

        void NotState(string op, params string[] names)
        {
            foreach (string name in names)
            {
                if (!fields.Add($"{op}.{name}"))
                {
                    throw new InvalidOperationException($"{op}.{name} is listed twice.");
                }
            }
        }

        NotState("cells set_autofilter", "off");
        NotState("cells insert_rows", "count");
        NotState("cells delete_rows", "count");
        NotState("cells insert_columns", "count");
        NotState("cells delete_columns", "count");
        NotState("cells delete_name", "name");
        NotState("cells delete_chart", "name");
        NotState("cells update_chart", "name");
        NotState("cells refresh_pivot", "name");
        NotState("cells sort_range", "by", "hasHeader");
        NotState("cells remove_duplicates", "columns", "hasHeader");
        NotState("cells import_range", "content");
        NotState("cells clear_range", "what");

        NotState("pdf delete_bookmarks", "indexes", "all");
        NotState("pdf set_page_size", "scaleContent");
        NotState("pdf optimize", "downsampleImagesDpi", "imageQuality", "unembedFonts", "removeUnusedObjects", "compressStreams");
        NotState("pdf set_form_field", "name");
        NotState("pdf redact_area", "rect", "fillColor");
        NotState("pdf redact_text", "pattern", "regex", "fillColor");
        NotState("pdf crop_pages", "box");
        NotState("pdf add_bookmark", "parent");
        NotState("pdf remove_metadata", "xmp", "documentInfo");
        NotState("pdf remove_attachment", "name");
        NotState("pdf move_pages", "to");
        NotState("pdf flatten_forms", "fields");
        NotState("pdf encrypt", "userPasswordEnv", "ownerPasswordEnv");

        NotState("slides append_presentation", "masterPolicy");
        NotState("slides replace_text", "find", "replace", "regex", "matchCase", "scope");
        NotState("slides set_table_cell", "row", "col");
        NotState("slides move_slide", "to");
        NotState("slides set_slide_size", "scaleContent");

        NotState("words define_style", "name");
        NotState("words add_section", "after");
        NotState("words accept_revisions", "author", "revisions");
        NotState("words reject_revisions", "author", "revisions");
        NotState("words append_document", "importFormatMode");
        NotState("words set_table_cell", "row", "col");
        NotState("words replace_text", "find", "replace", "regex", "matchCase", "wholeWord", "scope", "maxReplacementCount");
        NotState("words insert_markdown", "markdown");
        NotState("words set_header", "markdown");
        NotState("words set_footer", "markdown");
        NotState("words remove_comments", "author");
        NotState("words update_fields", "what");
        NotState("words mail_merge", "inline", "regions");
        NotState("words repeat_table_row", "row", "items");
        return fields;
    }

    private static IEnumerable<InvariantCase> R21Declared(CliCatalog catalog)
    {
        CliProduct[] products = [.. PublishedSchema.OpsProducts(catalog)];
        foreach (CliProduct product in products)
        {
            foreach (string op in product.Ops)
            {
                string subject = PublishedSchema.OpSubject(product, op).Name;
                yield return Check("C-R21", subject, () => ReadBack(product, op).Problems);
                yield return Check("C-R21", $"{subject} unclassified", () =>
                    ReadBack(product, op).Unclassified is { Count: > 0 } fields ? [$"unclassified: {string.Join(", ", fields)}"] : []);
            }
        }
        yield return Check("C-R21", "read-back table", () => OrphanEntries(products));
    }

    /// <summary>The read-back problems of one op's classified state fields, and the fields nobody has classified.</summary>
    private static (List<string> Problems, List<string> Unclassified) ReadBack(CliProduct product, string op)
    {
        string id = product.OpsSchemaId!;
        var unclassified = new List<string>();
        var problems = new List<string>();
        Classify(PublishedSchema.Definition(id, op), id, $"{product.Id} {op}.", string.Empty);
        return (problems, unclassified);

        void Classify(JsonObject owner, string schemaId, string prefix, string fieldPrefix)
        {
            if (owner["properties"] is not JsonObject properties)
            {
                return;
            }
            foreach ((string name, JsonNode? value) in properties)
            {
                string field = fieldPrefix + name;
                string key = prefix + name;
                if (name is "id" or "op" || NotStateFields.Contains(key) || PositionalReadBack.ContainsKey(key))
                {
                    continue;
                }
                if (StateFields.TryGetValue(key, out string? path))
                {
                    problems.AddRange(StateField(field, schemaId, (JsonObject)value!, path));
                    continue;
                }
                if (ClassifiedKeys.Any(other => other.StartsWith(key + ".", StringComparison.Ordinal)))
                {
                    (string nestedId, JsonObject nested) = PublishedSchema.Resolve(schemaId, (JsonObject)value!);
                    if (nested["items"] is JsonObject items)
                    {
                        (nestedId, nested) = PublishedSchema.Resolve(nestedId, items);
                    }
                    Classify(nested, nestedId, key + ".", field + ".");
                    continue;
                }
                if (fieldPrefix.Length == 0 && AddressNames.TryGetValue(product.Id, out HashSet<string>? addresses) && addresses.Contains(name))
                {
                    continue;
                }
                unclassified.Add(field);
            }
        }
    }

    /// <summary>The ways one state field fails to read back: no read path, a missing path, another name or another value domain.</summary>
    private static IEnumerable<string> StateField(string field, string schemaId, JsonObject schema, string? path)
    {
        if (path is null)
        {
            yield return $"{field}: no read reports it";
            yield break;
        }
        if (ReadField(path) is not { } read)
        {
            yield return $"{field}: read path {path} does not exist";
            yield break;
        }
        string name = field[(field.LastIndexOf('.') + 1)..];
        string readName = path[(path.LastIndexOfAny(['/', '#']) + 1)..];
        if (readName != name)
        {
            yield return $"{field}: read back as {readName}";
        }
        string written = PublishedSchema.Shape(schemaId, schema);
        string reported = PublishedSchema.Shape(read.SchemaId, read.Node);
        if (written != reported)
        {
            yield return $"{field}: writes {written}, reads {reported}";
        }
    }

    /// <summary>The schema of a read path, or null when the read schema has no such property.</summary>
    private static (string SchemaId, JsonObject Node)? ReadField(string path)
    {
        int hash = path.IndexOf('#', StringComparison.Ordinal);
        string schemaId = path[..hash];
        string[] segments = path[(hash + 1)..].Split('/');
        JsonObject? node = segments[0].Length == 0
            ? PublishedSchema.Document(schemaId)
            : PublishedSchema.Document(schemaId)["$defs"]?[segments[0]] as JsonObject;
        foreach (string segment in segments.Skip(1))
        {
            if (node is null)
            {
                return null;
            }
            (schemaId, node) = PublishedSchema.Resolve(schemaId, node);
            if (node["items"] is JsonObject items)
            {
                (schemaId, node) = PublishedSchema.Resolve(schemaId, items);
            }
            node = node["properties"]?[segment] as JsonObject;
        }
        return node is null ? null : (schemaId, node);
    }

    private static IEnumerable<string> ClassifiedKeys => StateFields.Keys.Concat(NotStateFields).Concat(PositionalReadBack.Keys);

    /// <summary>Table entries that name no op field any longer.</summary>
    private static IEnumerable<string> OrphanEntries(IReadOnlyList<CliProduct> products)
    {
        foreach (string key in ClassifiedKeys.Order(StringComparer.Ordinal))
        {
            string[] parts = key.Split(' ', 2);
            string[] path = parts[1].Split('.');
            CliProduct? product = products.FirstOrDefault(item => item.Id == parts[0]);
            if (product is null || !product.Ops.Contains(path[0]))
            {
                yield return $"{key}: no such op";
                continue;
            }
            string schemaId = product.OpsSchemaId!;
            JsonObject? node = PublishedSchema.Definition(schemaId, path[0]);
            foreach (string segment in path.Skip(1))
            {
                if (node is null)
                {
                    break;
                }
                (schemaId, node) = PublishedSchema.Resolve(schemaId, node);
                if (node["items"] is JsonObject items)
                {
                    (schemaId, node) = PublishedSchema.Resolve(schemaId, items);
                }
                node = node["properties"]?[segment] as JsonObject;
            }
            if (node is null)
            {
                yield return $"{key}: no such field";
            }
        }
    }
}
