using Aspose.Cli.Product.Pdf.Engine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// A command that knows why it needs a later page, such as merge, refuses the evaluation page
/// limit with its own cause and remedy. A guard that wraps every command in the same evaluation
/// check must keep that refusal rather than replace it with the generic one.
/// </summary>
public sealed class PdfEvaluationGuardTests
{
    private const string Cause = "merging reads every page of the inputs, and together they have more";
    private const string Remedy = "merge inputs that have at most 4 pages together";

    private static readonly ILicenseState Evaluation = new FixedLicenseState(LicenseState.Evaluation);

    [Fact]
    public void ACommandsOwnCauseAndRemedy_NameTheLimit()
    {
        CliException error = Assert.Throws<CliException>(() => Merge());

        AssertNamesMerge(error);
    }

    [Fact]
    public void AnOuterGuard_KeepsTheInnerCauseAndRemedy()
    {
        CliException error = Assert.Throws<CliException>(
            () => PdfEvaluation.Run(Evaluation, Merge));

        AssertNamesMerge(error);
    }

    [Fact]
    public void AnOuterGuard_StillRefusesALimitNoCommandExplained()
    {
        CliException error = Assert.Throws<CliException>(
            () => PdfEvaluation.Run(Evaluation, ReadPastTheLimit));

        Assert.Equal(ErrorCodes.EvaluationLimit, error.Code);
        Assert.Contains("this command needs a later page", error.Message, StringComparison.Ordinal);
    }

    private static int Merge() =>
        PdfEvaluation.Run(Evaluation, ReadPastTheLimit, cause: Cause, remedy: Remedy);

    /// <summary>What Aspose.PDF in evaluation mode throws when a command reads page 5.</summary>
    private static int ReadPastTheLimit() =>
        throw new IndexOutOfRangeException("Only 4 elements of any collection can be viewed in evaluation mode.");

    private static void AssertNamesMerge(CliException error)
    {
        Assert.Equal(ErrorCodes.EvaluationLimit, error.Code);
        Assert.Contains(Cause, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("this command needs a later page", error.Message, StringComparison.Ordinal);
        Assert.Contains(Remedy, error.Hint, StringComparison.Ordinal);
    }

    private sealed class FixedLicenseState(LicenseState state) : ILicenseState
    {
        public LicenseState License { get; } = state;
    }
}
