namespace Aspose.Cli.Invariants.Tests;

// C-R20: a field measured in a unit other than points names its unit (widthChars, scalePercent,
// durationMs, dpi). A schema does not say which number is a measure, so the measured op fields and
// their units are listed here by hand; a field outside the list is not judged.
internal static partial class ContractRules
{
    private const string Points = "points";

    /// <summary>The name ending each non-point unit requires.</summary>
    private static readonly Dictionary<string, string> UnitSuffixes = new(StringComparer.Ordinal)
    {
        ["characters"] = "Chars",
        ["dpi"] = "Dpi",
        ["inches"] = "Inches",
        ["milliseconds"] = "Ms",
        ["percent"] = "Percent",
        ["pixels"] = "Pixels",
    };

    /// <summary>Measured op fields by ops schema, definition and field, with their unit as the field's description or the engine states it.</summary>
    private static readonly Dictionary<(string Schema, string Definition, string Field), string> MeasuredFields = new()
    {
        [("v2/cells/ops", "insert_image", "height")] = "pixels",
        [("v2/cells/ops", "insert_image", "width")] = "pixels",
        [("v2/cells/ops", "margins", "bottom")] = "inches",
        [("v2/cells/ops", "margins", "footer")] = "inches",
        [("v2/cells/ops", "margins", "header")] = "inches",
        [("v2/cells/ops", "margins", "left")] = "inches",
        [("v2/cells/ops", "margins", "right")] = "inches",
        [("v2/cells/ops", "margins", "top")] = "inches",
        [("v2/cells/ops", "resize_columns", "width")] = "characters",
        [("v2/cells/ops", "resize_rows", "height")] = Points,
        [("v2/cells/ops", "set_default_font", "size")] = Points,
        [("v2/cells/ops", "set_page_setup", "scale")] = "percent",
        [("v2/cells/ops", "set_sheet_view", "zoom")] = "percent",
        [("v2/cells/ops", "styleData", "size")] = Points,

        [("v2/pdf/ops", "add_watermark_text", "fontSize")] = Points,
        [("v2/pdf/ops", "optimize_document", "downsampleImagesDpi")] = "dpi",
        [("v2/pdf/ops", "pdfRectInput", "height")] = Points,
        [("v2/pdf/ops", "pdfRectInput", "width")] = Points,
        [("v2/pdf/ops", "pdfRectInput", "x")] = Points,
        [("v2/pdf/ops", "pdfRectInput", "y")] = Points,

        [("v2/slides/ops", "set_shape_bounds", "height")] = Points,
        [("v2/slides/ops", "set_shape_bounds", "width")] = Points,
        [("v2/slides/ops", "set_shape_bounds", "x")] = Points,
        [("v2/slides/ops", "set_shape_bounds", "y")] = Points,
        [("v2/slides/ops", "set_transition", "durationMs")] = "milliseconds",
        [("v2/slides/ops", "slidesRectInput", "height")] = Points,
        [("v2/slides/ops", "slidesRectInput", "width")] = Points,
        [("v2/slides/ops", "slidesRectInput", "x")] = Points,
        [("v2/slides/ops", "slidesRectInput", "y")] = Points,
        [("v2/slides/ops", "slidesShapeStyleInput", "fontSize")] = Points,
        [("v2/slides/ops", "slidesSlideSizeInput", "height")] = Points,
        [("v2/slides/ops", "slidesSlideSizeInput", "width")] = Points,

        [("v2/words/ops", "define_style", "size")] = Points,
        [("v2/words/ops", "define_style", "spaceAfter")] = Points,
        [("v2/words/ops", "define_style", "spaceBefore")] = Points,
        [("v2/words/ops", "format_text", "size")] = Points,
        [("v2/words/ops", "insert_image", "height")] = Points,
        [("v2/words/ops", "insert_image", "width")] = Points,
        [("v2/words/ops", "marginInput", "bottom")] = Points,
        [("v2/words/ops", "marginInput", "left")] = Points,
        [("v2/words/ops", "marginInput", "right")] = Points,
        [("v2/words/ops", "marginInput", "top")] = Points,
        [("v2/words/ops", "set_default_font", "size")] = Points,
    };

    /// <summary>The measured fields of one ops definition that do not name their non-point unit, and listed fields it no longer has.</summary>
    private static IEnumerable<string> UnitNames(string schemaId, string definition)
    {
        var declared = PublishedSchema.Definition(schemaId, definition)["properties"]?.AsObject();
        foreach (var ((schema, owner, field), unit) in MeasuredFields.OrderBy(static pair => pair.Key.Field, StringComparer.Ordinal))
        {
            if (schema != schemaId || owner != definition)
            {
                continue;
            }
            if (declared?[field] is null)
            {
                yield return $"{field}: listed as measured in {unit} but no longer declared; update the list";
                continue;
            }
            if (unit != Points && !field.EndsWith(UnitSuffixes[unit], StringComparison.Ordinal)
                && !string.Equals(field, UnitSuffixes[unit], StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{field}: measured in {unit}; name it *{UnitSuffixes[unit]}";
            }
        }
    }
}
