using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using static Aspose.Cli.Product.Cells.Engine.Mapping.ConditionalFormatVocabulary;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Conditional formatting — value rules, colour scales, data bars and duplicate
/// highlighting. The v2 rule vocabulary is translated to the engine's
/// FormatConditionType / OperatorType here.
/// </summary>
internal static class ConditionalFormatOps
{
    public static void AddConditionalFormat(Worksheet sheet, AddConditionalFormatOp op)
    {
        int cfIndex = sheet.ConditionalFormattings.Add();
        FormatConditionCollection conditions = sheet.ConditionalFormattings[cfIndex];
        // One rule over every area of the range, as a read reports it.
        foreach (CellArea area in ValidationOps.Areas(op.Range))
        {
            conditions.AddArea(area);
        }

        CellsConditionalRule rule = op.Rule;
        switch (rule.Kind)
        {
            case CellsConditionalRuleKinds.CellValue:
                {
                    int condition = conditions.AddCondition(
                        FormatConditionType.CellValue, ValidationVocabulary.ToOperator(rule.Operator!), rule.Value1, rule.Value2);
                    ApplyConditionStyle(conditions[condition], op.MatchStyle!);
                    break;
                }

            case CellsConditionalRuleKinds.Duplicates:
                {
                    int condition = conditions.AddCondition(FormatConditionType.DuplicateValues);
                    ApplyConditionStyle(conditions[condition], op.MatchStyle!);
                    break;
                }

            case CellsConditionalRuleKinds.ColorScale:
                {
                    int condition = conditions.AddCondition(FormatConditionType.ColorScale);
                    ColorScale scale = conditions[condition].ColorScale;
                    scale.Is3ColorScale = rule.MidColor is not null;
                    scale.MinColor = StyleWriter.ParseHex(rule.MinColor!);
                    scale.MaxColor = StyleWriter.ParseHex(rule.MaxColor!);
                    if (rule.MidColor is { } mid)
                    {
                        scale.MidColor = StyleWriter.ParseHex(mid);
                    }

                    break;
                }

            case CellsConditionalRuleKinds.DataBar:
                {
                    int condition = conditions.AddCondition(FormatConditionType.DataBar);
                    conditions[condition].DataBar.Color = StyleWriter.ParseHex(rule.BarColor!);
                    break;
                }

            case CellsConditionalRuleKinds.Formula:
                {
                    // Expression: the formula anchors at the area's top-left cell
                    // and relative references shift per cell (probe-verified). The
                    // parser guaranteed the leading '=' — without it the engine
                    // stores the text as a string literal that never matches.
                    int condition = conditions.AddCondition(FormatConditionType.Expression);
                    conditions[condition].Formula1 = rule.Value1;
                    ApplyConditionStyle(conditions[condition], op.MatchStyle!);
                    break;
                }

            case CellsConditionalRuleKinds.TopBottom:
                {
                    int condition = conditions.AddCondition(FormatConditionType.Top10);
                    Top10 top10 = conditions[condition].Top10;
                    top10.Rank = rule.Rank!.Value; // parser-required
                    top10.IsPercent = rule.Percent;
                    top10.IsBottom = rule.FromBottom;

                    ApplyConditionStyle(conditions[condition], op.MatchStyle!);
                    break;
                }

            case CellsConditionalRuleKinds.IconSet:
                {
                    // Icons only — thresholds are automatic and no dxf style is
                    // involved (the parser rejects one).
                    int condition = conditions.AddCondition(FormatConditionType.IconSet);
                    conditions[condition].IconSet.Type = ToIconSetType(rule.IconSet!);
                    break;
                }
        }
    }

    /// <summary>
    /// A conditional format is a DIFFERENTIAL format: its dxf must carry only
    /// the fields the caller asked for, because every field it does carry
    /// overrides that field on matching cells — in Excel as much as in a
    /// render. Two behaviours verified against 26.9.0 shape this:
    /// <list type="bullet">
    /// <item>The style must come from the condition itself. On a workbook
    /// opened from disk — always, for this CLI — <c>Workbook.CreateStyle</c>
    /// returns a style whose font, number format and alignment already count
    /// as set, so assigning one writes <c>Arial 10 / General / bottom</c> into
    /// the dxf and silently strips the number format and font of every
    /// matching cell. The condition's own style stays differential.</item>
    /// <item>A solid fill must also carry its colour as the BACKGROUND colour:
    /// the engine's sheet renderer only paints a conditional fill it finds
    /// there, so a fgColor-only dxf renders the font colour but no fill.</item>
    /// </list>
    /// </summary>
    private static void ApplyConditionStyle(FormatCondition condition, CellsConditionalStyle style)
    {
        Style differential = condition.Style;
        StyleWriter.Apply(differential, style.ToStyleData());
        if (differential.Pattern == BackgroundType.Solid)
        {
            differential.BackgroundColor = differential.ForegroundColor;
        }

        condition.Style = differential;
    }

    public static void ClearConditionalFormats(Worksheet sheet, ClearConditionalFormatsOp op)
    {
        foreach (RangeRef range in A1.ParseRanges(op.Range))
        {
            sheet.ConditionalFormattings.RemoveArea(
                range.Start.Row, range.Start.Column, range.RowCount, range.ColumnCount);
        }
    }
}
