using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// A shared code of <see cref="ErrorCodes"/> is built only by an SDK error factory, such as
/// <see cref="CliErrors"/>, so each situation has one message and details shape; a product
/// builds its own codes.
/// </summary>
public sealed class SharedErrorCodeTests
{
    public static TheoryData<string> SharedCodes() => [.. ErrorCodes.All.Select(static code => code.Name)];

    [Theory]
    [MemberData(nameof(SharedCodes))]
    public void Constructor_RefusesASharedCode(string name)
    {
        ErrorCode declared = ErrorCodes.All.Single(code => code.Name == name);

        Assert.Throws<ArgumentException>(() => new CliException(declared, "Built by hand."));
        // A code declared again under a shared name is the same code.
        Assert.Throws<ArgumentException>(() => new CliException(new ErrorCode(name, declared.ExitCode), "Built by hand."));
    }

    /// <summary>
    /// The free-text factories of <c>FEATURE_UNSUPPORTED</c> say which kind of refusal they are
    /// in <c>details.reason</c>, so a caller can tell them apart without reading the message.
    /// </summary>
    [Fact]
    public void FeatureUnsupported_FreeTextFactoriesStateTheirReason()
    {
        (CliException Error, string Reason)[] cases =
        [
            (CliErrors.ResourceRefused("The input names a network address."), "resource-refused"),
            (CliErrors.ContentUnsupported("The table is too large."), "content-too-large"),
            (CliErrors.EnvironmentUnsupported("The font registry is unreadable."), "environment"),
            (CliErrors.EngineDefect("The engine writes invalid JSON."), "engine-defect"),
        ];

        Assert.All(cases, static item =>
        {
            Assert.Equal(ErrorCodes.FeatureUnsupported, item.Error.Code);
            Assert.Equal(item.Reason, item.Error.Details!["reason"]!.GetValue<string>());
        });
    }

    [Fact]
    public void Constructor_BuildsAProductCode()
    {
        var code = new ErrorCode("RANGE_INVALID", ExitCode.ValidationError);

        Assert.Same(code, new CliException(code, "Invalid range 'A0'.").Code);
    }

    [Fact]
    public void InputUnreadable_AlwaysNamesTheFullPath()
    {
        string path = Path.Combine(Path.GetTempPath(), "missing", "deck.pptx");

        CliException error = CliErrors.InputUnreadable(path, "supported presentation", "detected format is Unknown");

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.Equal(path, error.Details!["path"]!.GetValue<string>());
        Assert.Equal($"Input is not a valid supported presentation: {path} (detected format is Unknown).", error.Message);
    }
}
