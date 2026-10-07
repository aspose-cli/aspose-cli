using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Data validation: dropdown lists and input constraints.</summary>
internal static class ValidationOps
{
    public static long? SetValidation(Worksheet sheet, SetValidationOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;
        CellArea area = CellArea.CreateCellArea(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column);

        Validation validation = sheet.Validations[sheet.Validations.Add(area)];
        validation.IgnoreBlank = op.AllowBlank;

        switch (op.Type)
        {
            case ValidationTypes.List:
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

            case ValidationTypes.Custom:
                validation.Type = ValidationType.Custom;
                validation.Formula1 = op.Value1;
                break;

            default:
                validation.Type = ToType(op.Type);
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

        return range.CellCount;
    }

    public static long? ClearValidation(Worksheet sheet, ClearValidationOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;
        sheet.Validations.RemoveArea(CellArea.CreateCellArea(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column));
        return range.CellCount;
    }

    private static ValidationType ToType(string type) => type switch
    {
        ValidationTypes.WholeNumber => ValidationType.WholeNumber,
        ValidationTypes.Decimal => ValidationType.Decimal,
        ValidationTypes.Date => ValidationType.Date,
        ValidationTypes.TextLength => ValidationType.TextLength,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Validation type is missing from the engine mapper."),
    };

    internal static OperatorType ToOperator(string op) => op switch
    {
        ValidationOperators.Between => OperatorType.Between,
        ValidationOperators.NotBetween => OperatorType.NotBetween,
        ValidationOperators.Equal => OperatorType.Equal,
        ValidationOperators.NotEqual => OperatorType.NotEqual,
        ValidationOperators.GreaterThan => OperatorType.GreaterThan,
        ValidationOperators.LessThan => OperatorType.LessThan,
        ValidationOperators.GreaterOrEqual => OperatorType.GreaterOrEqual,
        ValidationOperators.LessOrEqual => OperatorType.LessOrEqual,
        _ => throw new ArgumentOutOfRangeException(
            nameof(op), op, "Validation operator is missing from the engine mapper."),
    };
}
