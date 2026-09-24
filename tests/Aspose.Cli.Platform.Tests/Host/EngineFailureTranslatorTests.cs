using System.Reflection;
using System.Text.RegularExpressions;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Errors;
using Xunit;
using Xunit.Sdk;

namespace Aspose.Cli.Host.Tests;

public sealed class EngineFailureTranslatorTests
{
    // This test assembly plays a product; xunit.assert plays its third-party engine.
    private static readonly EngineFailureTranslator Translator = new(
        new HashSet<Assembly> { typeof(EngineFailureTranslatorTests).Assembly, typeof(CliException).Assembly },
        new Dictionary<Assembly, string> { [typeof(EngineFailureTranslatorTests).Assembly] = "Sample" });

    [Fact]
    public void FailureRaisedByAThirdPartyEngine_IsAnEngineFailure()
    {
        CliException? error = Translator.Translate(Caught(static () => Assert.Fail("engine choked")));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error?.Code);
        Assert.StartsWith("Sample failed inside its document engine", error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailureRaisedByCliCode_StaysInternal() =>
        Assert.Null(Translator.Translate(Caught(static () => throw new InvalidOperationException("our defect"))));

    [Fact]
    public void FileFailureInsideAProduct_IsAnUnwritableOutput() =>
        Assert.Equal(ErrorCodes.OutputUnwritable,
            Translator.Translate(Caught(static () => throw new IOException("disk full")))?.Code);

    [Fact]
    public void FailureOutsideEveryProduct_IsNotTranslated()
    {
        var translator = new EngineFailureTranslator(
            new HashSet<Assembly> { typeof(CliException).Assembly },
            new Dictionary<Assembly, string>());

        Assert.Null(translator.Translate(Caught(static () => Assert.Fail("anything"))));
    }

    [Fact]
    public void RegularExpressionTimeout_IsAnOperationTimeout() =>
        Assert.Equal(ErrorCodes.OperationTimeout,
            Translator.Translate(new RegexMatchTimeoutException("a+", "aaaa", TimeSpan.FromSeconds(1)))?.Code);

    private static Exception Caught(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }
        throw new XunitException("The action did not throw.");
    }
}
