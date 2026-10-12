using Aspose.Cells;

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
        RangeRef range = A1.ParseRange(op.Range).Range;
        int cfIndex = sheet.ConditionalFormattings.Add();
        FormatConditionCollection conditions = sheet.ConditionalFormattings[cfIndex];
        conditions.AddArea(CellArea.CreateCellArea(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column));

        CellsConditionalRule rule = op.Rule;
        switch (rule.Kind)
        {
            case CellsConditionalRuleKinds.CellValue:
                {
                    int condition = conditions.AddCondition(
                        FormatConditionType.CellValue, ValidationOps.ToOperator(rule.Operator!), rule.Value1, rule.Value2);
                    ApplyConditionStyle(conditions[condition], op.Style!);
                    break;
                }

            case CellsConditionalRuleKinds.Duplicates:
                {
                    int condition = conditions.AddCondition(FormatConditionType.DuplicateValues);
                    ApplyConditionStyle(conditions[condition], op.Style!);
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
                    ApplyConditionStyle(conditions[condition], op.Style!);
                    break;
                }

            case CellsConditionalRuleKinds.TopBottom:
                {
                    int condition = conditions.AddCondition(FormatConditionType.Top10);
                    Top10 top10 = conditions[condition].Top10;
                    top10.Rank = rule.Rank!.Value; // parser-required
                    top10.IsPercent = rule.Percent;
                    top10.IsBottom = rule.Bottom;

                    ApplyConditionStyle(conditions[condition], op.Style!);
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
    /// Wire name to the engine's <see cref="IconSetType"/>. The engine enum
    /// carries digit suffixes and near-duplicates (<c>TrafficLights31</c> vs
    /// <c>TrafficLights32</c>, <c>Symbols3</c> vs <c>Symbols32</c>); the wire
    /// vocabulary picks the five canonical sets (probe-verified names).
    /// </summary>
    /// <summary>The wire name of an engine icon set, or null for one outside the vocabulary.</summary>
    internal static string? FromIconSetType(IconSetType type) =>
        IconSets.FirstOrDefault(name => ToIconSetType(name) == type);

    private static readonly string[] IconSets =
    [
        CellsIconSetNames.Arrows3, CellsIconSetNames.TrafficLights3, CellsIconSetNames.Symbols3,
        CellsIconSetNames.Rating4, CellsIconSetNames.Rating5,
    ];

    private static IconSetType ToIconSetType(string name) => name switch
    {
        CellsIconSetNames.Arrows3 => IconSetType.Arrows3,
        CellsIconSetNames.TrafficLights3 => IconSetType.TrafficLights31,
        CellsIconSetNames.Symbols3 => IconSetType.Symbols3,
        CellsIconSetNames.Rating4 => IconSetType.Rating4,
        CellsIconSetNames.Rating5 => IconSetType.Rating5,
        _ => throw new ArgumentOutOfRangeException(
            nameof(name), name, "Icon set is missing from the engine mapper."),
    };

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
        RangeRef range = A1.ParseRange(op.Range).Range;
        sheet.ConditionalFormattings.RemoveArea(
            range.Start.Row, range.Start.Column, range.RowCount, range.ColumnCount);
    }
}
