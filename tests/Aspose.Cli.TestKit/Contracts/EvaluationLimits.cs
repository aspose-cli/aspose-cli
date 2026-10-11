using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>
/// Runs engine calls the way the SDK runs a command handler, so a product test sees the
/// <c>EVALUATION_LIMIT</c> error the SDK writes for an evaluation limit its engine raises, and
/// checks that every product's error has the one shape the SDK gives it.
/// </summary>
public static partial class EvaluationLimits
{
    /// <summary>
    /// Runs <paramref name="run"/> as the SDK runs a command handler on <paramref name="outputs"/>,
    /// for a command whose selection option over a unit is <paramref name="selection"/>, if any.
    /// </summary>
    public static T Run<T>(OutputPipeline outputs, Func<T> run, Func<string, string?>? selection = null)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        return outputs.ReportingEvaluationLimits(run, selection);
    }

    /// <summary>A license gate that reports <paramref name="state"/>, whatever license the process applied.</summary>
    public static ILicenseGate Gate(LicenseState state) => new FixedGate(state);

    /// <summary>
    /// Asserts the one shape of an engine's evaluation limit: the engine names the license in the
    /// message and the hint, which offers other ways only where they work, and the details hold
    /// only the limit and its unit.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="hint">The error hint.</param>
    /// <param name="details">The names of the error's details.</param>
    /// <param name="engine">The engine, such as <c>Aspose.PDF</c>.</param>
    public static void AssertShape(string code, string message, string? hint, IEnumerable<string> details, string engine)
    {
        Assert.Equal(ErrorCodes.EvaluationLimit.Name, code);
        Match match = Message().Match(message);
        Assert.True(match.Success, $"An engine's evaluation limit reads '<engine> evaluation mode <limit>, and this command needs more, ...': {message}");
        Assert.Equal(engine, match.Groups["engine"].Value);
        Assert.StartsWith($"Apply an {engine} license", hint, StringComparison.Ordinal);
        Assert.Equal(["limit", "unit"], details.Order(StringComparer.Ordinal));
    }

    [GeneratedRegex(@"^(?<engine>Aspose\.[A-Za-z]+) evaluation mode .+, and this command needs more, so it stopped and wrote nothing\.$")]
    private static partial Regex Message();

    private sealed class FixedGate(LicenseState state) : ILicenseGate
    {
        public LicenseResolution Resolution => LicenseResolution.None;

        public string Identity => "fixed";

        public LicenseState EnsureApplied() => state;
    }
}
