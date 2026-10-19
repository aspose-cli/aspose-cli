using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Publication;

namespace Aspose.Cli.Sdk.Tests;

internal static class TestBudgets
{
    public static ResourceBudgetLedger Create(
        IReadOnlyDictionary<string, long>? limits = null) =>
        new(OperationDeadline.Start(null), limits);

    public static SafeFileWriter Writer() => new(Create());
}
