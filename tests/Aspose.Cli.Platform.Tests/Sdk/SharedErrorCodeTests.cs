using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// A shared code of <see cref="ErrorCodes"/> is built only by an SDK error factory, such as
/// <see cref="CliErrors"/>, so each situation has one message, hint and details shape; a product
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

    [Fact]
    public void Constructor_BuildsAProductCode()
    {
        var code = new ErrorCode("RANGE_INVALID", ExitCode.ValidationError);

        Assert.Same(code, new CliException(code, "Invalid range 'A0'.").Code);
    }

    [Fact]
    public void FromRemote_RestatesTheErrorAnotherProcessReported()
    {
        CliException error = CliErrors.FromRemote("FILE_CORRUPT", ExitCode.InputError, "Damaged.", "Check it.");

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.Equal(("Damaged.", "Check it."), (error.Message, error.Hint));
    }

    [Fact]
    public void InputUnreadable_AlwaysNamesTheFullPath()
    {
        string path = Path.Combine(Path.GetTempPath(), "missing", "deck.pptx");

        CliException error = CliErrors.InputUnreadable(path, "supported presentation", "detected format is Unknown", "Check it.");

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.Equal(path, error.Details!["path"]!.GetValue<string>());
        Assert.Equal($"Input is not a valid supported presentation: {path} (detected format is Unknown).", error.Message);
    }
}
