using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The set_validation operators as the engine names them, which a read reports and the
/// validation and conditional-format operations write.
/// </summary>
internal static class ValidationVocabulary
{
    /// <summary>The set_validation operator of an engine comparison, or null for none.</summary>
    internal static string? FromOperator(OperatorType type) =>
        Operators.FirstOrDefault(name => ToOperator(name) == type);

    private static readonly string[] Operators =
    [
        CellsValidationOperators.Between, CellsValidationOperators.NotBetween, CellsValidationOperators.Equal,
        CellsValidationOperators.NotEqual, CellsValidationOperators.GreaterThan, CellsValidationOperators.LessThan,
        CellsValidationOperators.GreaterOrEqual, CellsValidationOperators.LessOrEqual,
    ];

    internal static OperatorType ToOperator(string op) => op switch
    {
        CellsValidationOperators.Between => OperatorType.Between,
        CellsValidationOperators.NotBetween => OperatorType.NotBetween,
        CellsValidationOperators.Equal => OperatorType.Equal,
        CellsValidationOperators.NotEqual => OperatorType.NotEqual,
        CellsValidationOperators.GreaterThan => OperatorType.GreaterThan,
        CellsValidationOperators.LessThan => OperatorType.LessThan,
        CellsValidationOperators.GreaterOrEqual => OperatorType.GreaterOrEqual,
        CellsValidationOperators.LessOrEqual => OperatorType.LessOrEqual,
        _ => throw new ArgumentOutOfRangeException(
            nameof(op), op, "Validation operator is missing from the engine mapper."),
    };
}
