using System.Reflection;
using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Publication;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Aspose.Cells evaluation mode refuses to open a file once its process has opened 100, which
/// only a long-lived process such as a preview's render worker reaches. The SDK reports that
/// refusal as the one <c>EVALUATION_LIMIT</c> every product shares; a licensed engine never
/// raises it, so with a license the call is not translated.
/// </summary>
public sealed class CellsEvaluationLimitTests
{
    /// <summary>
    /// What the engine throws on the 101st open in one evaluation process. Its constructor is not
    /// public, and reaching the limit for real would leave this test process unable to open any
    /// workbook, so the exception is built as the engine builds it.
    /// </summary>
    private static CellsException OpenLimit() => (CellsException)Activator.CreateInstance(
        typeof(CellsException),
        BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null,
        args: [ExceptionType.License, "You are using an evaluation copy and have opened files exceeding limitation."],
        culture: null)!;

    [Fact]
    public void TheEnginesOpenLimit_IsReportedByTheSdkInEvaluationMode()
    {
        CellsException refusal = OpenLimit();

        CliException error = Assert.Throws<CliException>(() => EvaluationLimits.Run<int>(Outputs(LicenseState.Evaluation), () => throw refusal));

        EvaluationLimits.AssertShape(error.Code.Name, error.Message, error.Hint, error.Details!.Select(static pair => pair.Key), "Aspose.Cells");
        Assert.Equal(100, error.Details!["limit"]!.GetValue<int>());
        Assert.Equal("files", error.Details["unit"]!.GetValue<string>());
        Assert.Same(refusal, error.InnerException);
    }

    [Fact]
    public void ALicensedEngineFailure_IsNotTranslated()
    {
        CellsException failure = OpenLimit();

        CellsException thrown = Assert.Throws<CellsException>(() => EvaluationLimits.Run<int>(Outputs(LicenseState.Licensed), () => throw failure));

        Assert.Same(failure, thrown);
    }

    [Theory]
    [InlineData(LicenseState.Evaluation, false)]
    [InlineData(LicenseState.Licensed, true)]
    public void TheLoader_LeavesTheOpenLimitToTheSdkOnlyInEvaluationMode(LicenseState state, bool loadFailure)
    {
        ResourceBudgetLedger budgets = ProductTestBudgets.Create<CellsModule>();
        var loader = new Engine.Mapping.CellsWorkbookLoader(budgets, Outputs(state));

        Assert.Equal(loadFailure, loader.IsLoadFailure(OpenLimit()));
        Assert.True(loader.IsLoadFailure(new InvalidDataException("damaged")));
    }

    private static OutputPipeline<Workbook> Outputs(LicenseState state)
    {
        ResourceBudgetLedger budgets = ProductTestBudgets.Create<CellsModule>();
        return new OutputPipeline<Workbook>(EvaluationLimits.Gate(state), new CellsEvaluationProfile(), new SafeFileWriter(budgets));
    }
}
