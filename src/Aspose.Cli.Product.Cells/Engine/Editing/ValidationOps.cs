using Aspose.Cells;
using static Aspose.Cli.Product.Cells.Engine.Mapping.ValidationVocabulary;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Data validation: dropdown lists and input constraints.</summary>
internal static class ValidationOps
{
    public static void SetValidation(Worksheet sheet, SetValidationOp op)
    {
        CellArea[] areas = Areas(op.Range);

        // set_ replaces: the range's earlier validation gives way, so a repeat leaves one, and
        // one validation covers every area of the range, as a read reports it.
        foreach (CellArea area in areas)
        {
            sheet.Validations.RemoveArea(area);
        }

        Validation validation = sheet.Validations[sheet.Validations.Add(areas[0])];
        foreach (CellArea area in areas.Skip(1))
        {
            validation.AddArea(area);
        }

        validation.IgnoreBlank = op.AllowBlank;

        switch (op.Kind)
        {
            case CellsValidationTypes.List:
                validation.Type = ValidationType.List;
                validation.InCellDropDown = true;
                // An explicit list is a comma-separated string literal, given as a
                // formula so the engine stores it as Excel's "a,b,c" (a bare quoted
                // string is stored with its quotes escaped into the items). A source
                // range is a formula reference. Callers sometimes already write
                // the leading '=' (it reads naturally next to other formula
                // fields) — doubling it produces "==Sheet!A1", which the engine
                // rejects as "Absent operand for '='", so normalize instead of
                // always prepending.
                validation.Formula1 = op.ListItems is { } items
                    ? "=\"" + string.Join(",", items) + "\""
                    : op.ListSource!.StartsWith('=') ? op.ListSource : "=" + op.ListSource;
                break;

            case CellsValidationTypes.Custom:
                validation.Type = ValidationType.Custom;
                validation.Formula1 = op.Value1;
                break;

            default:
                validation.Type = ToType(op.Kind);
                validation.Operator = ToOperator(op.Operator!); // required by the parser
                validation.Formula1 = op.Value1;
                if (op.Value2 is { } value2)
                {
                    validation.Formula2 = value2;
                }

                break;
        }

        if (op.InputMessage is { } input)
        {
            validation.ShowInput = true;
            validation.InputMessage = input;
        }

        if (op.ErrorMessage is { } error)
        {
            validation.ShowError = true;
            validation.ErrorMessage = error;
        }
    }

    public static void ClearValidation(Worksheet sheet, ClearValidationOp op)
    {
        foreach (CellArea area in Areas(op.Range))
        {
            sheet.Validations.RemoveArea(area);
        }
    }

    /// <summary>The engine areas of a range or a comma-separated list of them, in order.</summary>
    internal static CellArea[] Areas(string ranges) =>
        [.. A1.ParseRanges(ranges).Select(static range =>
            CellArea.CreateCellArea(range.Start.Row, range.Start.Column, range.End.Row, range.End.Column))];

    private static ValidationType ToType(string type) => type switch
    {
        CellsValidationTypes.WholeNumber => ValidationType.WholeNumber,
        CellsValidationTypes.Decimal => ValidationType.Decimal,
        CellsValidationTypes.Date => ValidationType.Date,
        CellsValidationTypes.TextLength => ValidationType.TextLength,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Validation type is missing from the engine mapper."),
    };
}
