using System.Globalization;
using System.Text.Json;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cells.Pivot;
using Aspose.Cells.Tables;
using Aspose.Cli.Product.Cells.Engine.Editing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Reads the objects of <c>cells inspect</c>'s detail sections back in the fields of the
/// operations that create and change them (C-R21): what insert_chart, insert_pivot,
/// define_table, set_validation, add_conditional_format, add_sparkline, insert_image,
/// add_comment and set_hyperlink write is what these report, under the same names.
/// </summary>
internal static class ObjectProjection
{
    /// <summary>
    /// A stored reference such as <c>=Data!$A$1:$B$3</c> as the operations take it: without
    /// absolute markers, and naming the sheet only when it is not <paramref name="owner"/>.
    /// Null when the text is no single range, such as a literal array.
    /// </summary>
    public static string? Reference(Worksheet owner, string? stored) =>
        Parse(stored) is { } parsed ? Reference(owner, parsed.Sheet, parsed.Range) : null;

    private static string Reference(Worksheet owner, string? sheet, RangeRef range) =>
        sheet is null || string.Equals(sheet, owner.Name, StringComparison.OrdinalIgnoreCase)
            ? A1.FormatRange(range)
            : SheetPrefix(sheet) + "!" + A1.FormatRange(range);

    /// <summary>A sheet name as Excel writes it in a reference: quoted only when it must be.</summary>
    private static string SheetPrefix(string sheet)
    {
        bool plain = sheet.Length > 0
            && (char.IsLetter(sheet[0]) || sheet[0] == '_')
            && sheet.All(static c => char.IsLetterOrDigit(c) || c is '_' or '.')
            && !LooksLikeCell(sheet);
        return plain ? sheet : Sheets.QuotedName(sheet);

        static bool LooksLikeCell(string text)
        {
            int letters = text.TakeWhile(char.IsAsciiLetter).Count();
            return letters is >= 1 and <= 3 && letters < text.Length && text[letters..].All(char.IsAsciiDigit);
        }
    }

    private static (string? Sheet, RangeRef Range)? Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        string text = stored.TrimStart('=');
        if (text.Contains(',', StringComparison.Ordinal) || text.StartsWith('{'))
        {
            return null;
        }

        try
        {
            RangeSpec spec = A1.ParseRange(text);
            return (spec.SheetName, spec.Range);
        }
        catch (CliException)
        {
            return null;
        }
    }

    /// <summary>
    /// A bound or comparison value as an operation writes it: Excel stores a number as the
    /// formula <c>=10</c>, which reads as <c>10</c>; any other formula keeps its <c>=</c>.
    /// </summary>
    private static string? Bound(string? stored) =>
        string.IsNullOrEmpty(stored)
            ? null
            : stored.StartsWith('=') && double.TryParse(stored.AsSpan(1), NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                ? stored[1..]
                : stored;

    private static string? Text(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string Area(CellArea area) =>
        A1.FormatRange(new RangeRef(new CellRef(area.StartRow, area.StartColumn), new CellRef(area.EndRow, area.EndColumn)));

    // ----- tables, charts, pivots, validations ------------------------------------------------

    public static CellsTableInfo Table(Worksheet sheet, ListObject table) => new()
    {
        Sheet = sheet.Name,
        Name = table.DisplayName,
        Range = A1.FormatRange(new RangeRef(new CellRef(table.StartRow, table.StartColumn), new CellRef(table.EndRow, table.EndColumn))),
        TableStyle = table.TableStyleType switch
        {
            TableStyleType.None => null,
            TableStyleType.Custom => Text(table.TableStyleName),
            TableStyleType type => type.ToString(),
        },
        TotalsRow = table.ShowTotals,
    };

    public static CellsChartInfo Chart(Worksheet sheet, Chart chart, int index, string kind)
    {
        bool pie = ChartPivotOps.IsPieFamily(chart.Type);
        var series = chart.NSeries.Cast<Series>().ToList();
        var plotted = new List<(string? Sheet, RangeRef Range)>();
        foreach (Series item in series)
        {
            foreach (string? reference in new[] { item.Values, item.XValues, item.Name })
            {
                if (Parse(reference) is { } parsed)
                {
                    plotted.Add(parsed);
                }
            }
        }

        return new CellsChartInfo
        {
            Sheet = sheet.Name,
            Index = index,
            Name = chart.Name,
            Kind = kind,
            Title = chart.Title.IsAutoText ? null : Text(chart.Title.Text),
            DataRange = plotted.Count == 0
                ? null
                : Reference(sheet, plotted[0].Sheet, Cover(plotted.Select(static item => item.Range))),
            SeriesInRows = SeriesInRows(series),
            Legend = new CellsChartLegend
            {
                Visible = chart.ShowLegend,
                Position = chart.Legend.Position switch
                {
                    LegendPositionType.Right => CellsLegendPositions.Right,
                    LegendPositionType.Bottom => CellsLegendPositions.Bottom,
                    LegendPositionType.Top => CellsLegendPositions.Top,
                    LegendPositionType.Left => CellsLegendPositions.Left,
                    _ => null,
                },
            },
            AxisTitles = pie ? null : AxisTitles(chart),
            SeriesColors = series.Count == 0 ? null : SeriesColors(chart, series, pie),
            DataLabels = series.Count == 0 ? null : new CellsChartDataLabels
            {
                Visible = series[0].DataLabels.ShowValue,
                NumberFormat = series[0].DataLabels.NumberFormatLinked ? null : Text(series[0].DataLabels.NumberFormat),
            },
        };
    }

    /// <summary>
    /// Whether the series run along rows: a series whose values span columns of one row does, one
    /// spanning rows of one column does not, and single-cell series do when they sit in different rows.
    /// </summary>
    private static bool SeriesInRows(List<Series> series)
    {
        RangeRef[] values = [.. series.Select(static item => Parse(item.Values)).OfType<(string?, RangeRef Range)>().Select(static item => item.Range)];
        foreach (RangeRef range in values)
        {
            if (range.RowCount > 1 || range.ColumnCount > 1)
            {
                return range.RowCount == 1;
            }
        }

        return values.Select(static range => range.Start.Row).Distinct().Count() > 1;
    }

    private static CellsChartAxisTitles? AxisTitles(Chart chart)
    {
        string? category = chart.CategoryAxis.Title.IsAutoText ? null : Text(chart.CategoryAxis.Title.Text);
        string? value = chart.ValueAxis.Title.IsAutoText ? null : Text(chart.ValueAxis.Title.Text);
        return category is null && value is null ? null : new CellsChartAxisTitles { Category = category, Value = value };
    }

    /// <summary>The colors in the places <see cref="ChartPivotOps"/> writes them for the chart's type.</summary>
    private static IReadOnlyList<string>? SeriesColors(Chart chart, List<Series> series, bool pie)
    {
        bool line = chart.Type.ToString().StartsWith("Line", StringComparison.Ordinal);
        bool scatter = chart.Type.ToString().StartsWith("Scatter", StringComparison.Ordinal);
        IEnumerable<System.Drawing.Color> colors = pie
            ? series[0].Points.Cast<ChartPoint>().Select(static point => point.Area.ForegroundColor)
            : series.Select(item => line ? item.Border.Color : scatter ? item.Marker.Area.ForegroundColor : item.Area.ForegroundColor);
        string?[] hex = [.. colors.Select(StylePool.ToHex)];
        return hex.Length > 0 && hex.All(static color => color is not null) ? [.. hex.OfType<string>()] : null;
    }

    public static CellsPivotInfo Pivot(Worksheet sheet, PivotTable pivot, string range)
    {
        var values = new List<CellsPivotValueInfo>();
        foreach (PivotField field in pivot.DataFields)
        {
            values.Add(new CellsPivotValueInfo
            {
                Field = pivot.BaseFields[field.BaseIndex].Name,
                Function = ChartPivotOps.FromFunction(field.Function) ?? JsonNamingPolicy.CamelCase.ConvertName(field.Function.ToString()),
                NumberFormat = field.NumberFormat is { Length: > 0 } format && format != "General" ? format : null,
                Label = field.DisplayName,
            });
        }

        return new CellsPivotInfo
        {
            Sheet = sheet.Name,
            Name = pivot.Name,
            Range = range,
            SourceRange = pivot.DataSource is [{ } source, ..] ? Reference(sheet, source) : null,
            Rows = Names(pivot.RowFields),
            Columns = Names(pivot.ColumnFields),
            Values = values.Count == 0 ? null : values,
            Captions = pivot.RowHeaderCaption == "行标签" || pivot.GrandTotalName == "总计"
                ? CellsPivotCaptionLanguages.Chinese
                : CellsPivotCaptionLanguages.English,
        };

        static IReadOnlyList<string>? Names(PivotFieldCollection fields)
        {
            string[] names = [.. fields.Cast<PivotField>().Where(static field => !field.IsValuesField).Select(static field => field.Name)];
            return names.Length == 0 ? null : names;
        }
    }

    public static CellsValidationInfo Validation(Worksheet sheet, Validation validation, CellArea area, string kind)
    {
        bool list = validation.Type == ValidationType.List;
        bool compares = validation.Type is not (ValidationType.List or ValidationType.Custom or ValidationType.AnyValue);
        string? formula = Text(validation.Formula1);
        return new CellsValidationInfo
        {
            Sheet = sheet.Name,
            Range = Area(area),
            Kind = kind,
            Operator = compares ? ValidationOps.FromOperator(validation.Operator) : null,
            Value1 = list ? null : Bound(formula),
            Value2 = compares && validation.Operator is OperatorType.Between or OperatorType.NotBetween ? Bound(validation.Formula2) : null,
            ListItems = list && ListLiteral(formula) is { } items ? items.Split(',') : null,
            ListSource = list && formula is { } source && ListLiteral(source) is null ? Reference(sheet, source) ?? source.TrimStart('=') : null,
            InputMessage = Text(validation.InputMessage),
            ErrorMessage = Text(validation.ErrorMessage),
            AllowBlank = validation.IgnoreBlank,
        };
    }

    /// <summary>
    /// The items of a list validation given as items, or null for one that reads a range: the
    /// engine keeps the literal as it was set, ="a,b", until a save, and as a,b after one.
    /// </summary>
    private static string? ListLiteral(string? formula) => formula switch
    {
        null => null,
        ['=', '"', .. var inner, '"'] => inner,
        ['=', ..] => null,
        _ => formula,
    };

    // ----- the detail lists of their own ------------------------------------------------------

    public static IReadOnlyList<CellsConditionalFormatInfo> ConditionalFormats(ResourceBudgetLedger budgets, Workbook workbook)
    {
        var formats = new List<CellsConditionalFormatInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (FormatConditionCollection conditions in sheet.ConditionalFormattings)
            {
                budgets.Deadline.ThrowIfExpired("conditional-formats");
                string range = string.Join(",", Enumerable.Range(0, conditions.RangeCount).Select(i => Area(conditions.GetCellArea(i))));
                for (int i = 0; i < conditions.Count; i++)
                {
                    CellsConditionalRuleInfo rule = ConditionalRule(conditions[i]);
                    formats.Add(new CellsConditionalFormatInfo
                    {
                        Sheet = sheet.Name,
                        Range = range,
                        Rule = rule,
                        Style = conditions[i].Type is FormatConditionType.ColorScale or FormatConditionType.DataBar or FormatConditionType.IconSet
                            ? null
                            : ConditionalStyle(conditions[i].Style),
                    });
                }
            }
        }

        return formats;
    }

    private static CellsConditionalRuleInfo ConditionalRule(FormatCondition condition) => condition.Type switch
    {
        FormatConditionType.CellValue when ValidationOps.FromOperator(condition.Operator) is { } comparison => new CellsConditionalRuleInfo
        {
            Kind = CellsConditionalRuleKinds.CellValue,
            Operator = comparison,
            Value1 = Bound(condition.Formula1),
            Value2 = condition.Operator is OperatorType.Between or OperatorType.NotBetween ? Bound(condition.Formula2) : null,
        },
        FormatConditionType.DuplicateValues => new CellsConditionalRuleInfo { Kind = CellsConditionalRuleKinds.Duplicates },
        FormatConditionType.ColorScale => new CellsConditionalRuleInfo
        {
            Kind = CellsConditionalRuleKinds.ColorScale,
            MinColor = StylePool.ToHex(condition.ColorScale.MinColor),
            MidColor = condition.ColorScale.Is3ColorScale ? StylePool.ToHex(condition.ColorScale.MidColor) : null,
            MaxColor = StylePool.ToHex(condition.ColorScale.MaxColor),
        },
        FormatConditionType.DataBar => new CellsConditionalRuleInfo
        {
            Kind = CellsConditionalRuleKinds.DataBar,
            BarColor = StylePool.ToHex(condition.DataBar.Color),
        },
        FormatConditionType.Expression => new CellsConditionalRuleInfo
        {
            Kind = CellsConditionalRuleKinds.Formula,
            Value1 = Text(condition.Formula1),
        },
        FormatConditionType.Top10 => new CellsConditionalRuleInfo
        {
            Kind = CellsConditionalRuleKinds.TopBottom,
            Rank = condition.Top10.Rank,
            Percent = condition.Top10.IsPercent,
            Bottom = condition.Top10.IsBottom,
        },
        FormatConditionType.IconSet => new CellsConditionalRuleInfo
        {
            Kind = CellsConditionalRuleKinds.IconSet,
            IconSet = ConditionalFormatOps.FromIconSetType(condition.IconSet.Type)
                ?? JsonNamingPolicy.KebabCaseLower.ConvertName(condition.IconSet.Type.ToString()),
        },
        // A kind outside the vocabulary keeps its engine name; its fields are not read.
        _ => new CellsConditionalRuleInfo { Kind = JsonNamingPolicy.KebabCaseLower.ConvertName(condition.Type.ToString()) },
    };

    /// <summary>The fields a differential style sets, as add_conditional_format writes them.</summary>
    private static CellsConditionalStyle? ConditionalStyle(Style style)
    {
        var read = new CellsConditionalStyle
        {
            Bold = style.Font.IsBold ? true : null,
            Italic = style.Font.IsItalic ? true : null,
            Underline = style.Font.Underline == FontUnderlineType.None ? null : true,
            Strikethrough = style.Font.IsStrikeout ? true : null,
            Color = style.IsModified(StyleModifyFlag.FontColor) ? StylePool.ToHex(style.Font.Color) : null,
            FillColor = style.Pattern == BackgroundType.None ? null : StylePool.ToHex(style.ForegroundColor),
            NumberFormat = style.IsModified(StyleModifyFlag.NumberFormat) ? Text(style.Custom) : null,
        };
        return read == new CellsConditionalStyle() ? null : read;
    }

    public static IReadOnlyList<CellsSparklineInfo> Sparklines(ResourceBudgetLedger budgets, Workbook workbook)
    {
        var groups = new List<CellsSparklineInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (SparklineGroup group in sheet.SparklineGroups)
            {
                budgets.Deadline.ThrowIfExpired("sparklines");
                Sparkline[] sparklines = [.. group.Sparklines.Cast<Sparkline>()];
                var data = sparklines.Select(static sparkline => Parse(sparkline.DataRange)).OfType<(string? Sheet, RangeRef Range)>().ToList();
                if (data.Count == 0)
                {
                    continue;
                }

                string kind = SparklineOps.FromType(group.Type) ?? group.Type.ToString().ToLowerInvariant();

                groups.Add(new CellsSparklineInfo
                {
                    Sheet = sheet.Name,
                    Kind = kind,
                    Color = group.SeriesColor is { } color ? StylePool.ToHex(color.Color) : null,
                    DataRange = Covered([.. data.Select(static item => item.Range)]) is { } block
                        ? Reference(sheet, data[0].Sheet, block)
                        : string.Join(",", data.Select(item => Reference(sheet, item.Sheet, item.Range))),
                    Location = Covered([.. sparklines.Select(static sparkline =>
                        new RangeRef(new CellRef(sparkline.Row, sparkline.Column), new CellRef(sparkline.Row, sparkline.Column)))]) is { } strip
                        ? A1.FormatRange(strip)
                        : string.Join(",", sparklines.Select(static sparkline => A1.FormatCell(new CellRef(sparkline.Row, sparkline.Column)))),
                });
            }
        }

        return groups;
    }

    public static IReadOnlyList<CellsImageInfo> Images(Workbook workbook)
    {
        var images = new List<CellsImageInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            for (int i = 0; i < sheet.Pictures.Count; i++)
            {
                Picture picture = sheet.Pictures[i];
                images.Add(new CellsImageInfo
                {
                    Sheet = sheet.Name,
                    Index = i + 1,
                    At = A1.FormatCell(new CellRef(picture.UpperLeftRow, picture.UpperLeftColumn)),
                    // insert_image sizes in points; the engine keeps pixels at 96 per inch.
                    Width = Math.Round(picture.Width * 72d / 96, 2),
                    Height = Math.Round(picture.Height * 72d / 96, 2),
                });
            }
        }

        return images;
    }

    public static IReadOnlyList<CellsCommentInfo> Comments(ResourceBudgetLedger budgets, Workbook workbook)
    {
        var comments = new List<CellsCommentInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (Comment comment in sheet.Comments.Cast<Comment>().OrderBy(static item => item.Row).ThenBy(static item => item.Column))
            {
                if (comments.Count % 1024 == 0)
                {
                    budgets.Deadline.ThrowIfExpired("comments");
                }

                comments.Add(new CellsCommentInfo
                {
                    Sheet = sheet.Name,
                    Cell = A1.FormatCell(new CellRef(comment.Row, comment.Column)),
                    Text = comment.Note ?? string.Empty,
                    Author = Text(comment.Author),
                });
            }
        }

        return comments;
    }

    public static IReadOnlyList<CellsHyperlinkInfo> Hyperlinks(ResourceBudgetLedger budgets, Workbook workbook)
    {
        var links = new List<CellsHyperlinkInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (Hyperlink link in sheet.Hyperlinks)
            {
                if (links.Count % 1024 == 0)
                {
                    budgets.Deadline.ThrowIfExpired("hyperlinks");
                }

                bool internalLink = link.LinkType == TargetModeType.CellReference;
                links.Add(new CellsHyperlinkInfo
                {
                    Sheet = sheet.Name,
                    Range = Area(link.Area),
                    Url = internalLink ? null : Text(link.Address),
                    Target = internalLink ? Reference(sheet, link.Address) ?? Text(link.Address) : null,
                    Display = Text(link.TextToDisplay),
                });
            }
        }

        return links;
    }

    /// <summary>
    /// The one range the ranges fill exactly, without gaps or overlaps, or null when they do not:
    /// a group whose sparklines lost a row to an insertion is read as its parts, not as a block
    /// that claims the row.
    /// </summary>
    private static RangeRef? Covered(RangeRef[] ranges)
    {
        RangeRef cover = Cover(ranges);
        long cells = ranges.Sum(static range => range.CellCount);
        bool disjoint = ranges.Select(static range => range).Distinct().Count() == ranges.Length
            && !ranges.Where((range, i) => ranges.Skip(i + 1).Any(other => Overlaps(range, other))).Any();
        return disjoint && cells == cover.CellCount ? cover : null;

        static bool Overlaps(RangeRef left, RangeRef right) =>
            left.Start.Row <= right.End.Row && right.Start.Row <= left.End.Row
            && left.Start.Column <= right.End.Column && right.Start.Column <= left.End.Column;
    }

    /// <summary>The smallest range covering every range.</summary>
    private static RangeRef Cover(IEnumerable<RangeRef> ranges)
    {
        RangeRef[] all = [.. ranges];
        return new RangeRef(
            new CellRef(all.Min(static range => range.Start.Row), all.Min(static range => range.Start.Column)),
            new CellRef(all.Max(static range => range.End.Row), all.Max(static range => range.End.Column)));
    }
}
