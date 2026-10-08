using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class InputLoadingTests
{
    private sealed class PasswordFailure : Exception;

    private sealed class DamagedFailure : Exception
    {
        public DamagedFailure(string message) : base(message) { }
    }

    private static readonly InputLoading Loading = new("test document", "Check the test document.", static exception => exception switch
    {
        PasswordFailure => LoadFailureKind.Password,
        DamagedFailure => LoadFailureKind.Corrupt,
        _ => LoadFailureKind.Other,
    });

    [Fact]
    public void APasswordFailure_IsRequiredWithoutAPasswordAndInvalidWithOne()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.File("locked.doc");
        File.WriteAllText(path, "x");

        Assert.Equal(ErrorCodes.PasswordRequired, Fail(path, null, new PasswordFailure()).Code);
        Assert.Equal(ErrorCodes.PasswordInvalid, Fail(path, new Secret("wrong"), new PasswordFailure()).Code);
    }

    [Fact]
    public void ADamagedOrUnloadableInput_IsFileCorruptWithTheFirstLineOfTheEngineMessage()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.File("damaged.doc");
        File.WriteAllText(path, "x");

        CliException damaged = Fail(path, null, new DamagedFailure("The file format is not supported.\r\nAt offset 12."));
        CliException unloadable = Loading.Unloadable(path, "Docx");
        CliException truncated = Fail(path, null, new EndOfStreamException("Unexpected end."));

        Assert.All([damaged, unloadable, truncated], static error => Assert.Equal(ErrorCodes.FileCorrupt, error.Code));
        Assert.Equal("The file format is not supported.", damaged.Details!["reason"]!.GetValue<string>());
        Assert.Equal("Unexpected end.", truncated.Details!["reason"]!.GetValue<string>());
        Assert.Equal("its content is Docx, not a test document this command reads", unloadable.Details!["reason"]!.GetValue<string>());
        Assert.Equal("Check the test document.", damaged.Hint);
        Assert.IsType<DamagedFailure>(damaged.InnerException);
    }

    [Fact]
    public void AnAccessFailure_IsNamedByTheSdk()
    {
        using var workspace = new TempWorkspace();
        string present = workspace.File("present.doc");
        File.WriteAllText(present, "x");
        string missing = workspace.File("missing.doc");

        Assert.Equal(ErrorCodes.FileNotFound, Fail(missing, null, new FileNotFoundException()).Code);
        Assert.Equal(ErrorCodes.FileNotFound, Fail(missing, null, new IOException("gone")).Code);
        Assert.Equal(ErrorCodes.FileAccessDenied, Fail(present, null, new UnauthorizedAccessException()).Code);
        Assert.Equal(ErrorCodes.FileLocked, Fail(present, null, new IOException("locked", unchecked((int)0x80070020))).Code);
    }

    [Fact]
    public void AnInputHeldWithoutSharing_IsLocked()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.File("held.doc");
        File.WriteAllText(path, "x");
        using var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        CliException error = Assert.Throws<CliException>(() => Loading.Load(path, null, () => InputFiles.OpenRead(path)));

        Assert.Equal(ErrorCodes.FileLocked, error.Code);
    }

    [Fact]
    public void AnInMemoryInput_NeverReportsAFileAccessFailure()
    {
        CliException error = Loading.InMemoryFailure(new IOException("Unexpected end of the Markdown stream."), "inline Markdown");

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.Equal("Unexpected end of the Markdown stream.", error.Details!["reason"]!.GetValue<string>());
        Assert.Contains("inline Markdown", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnclassifiedFailure_PropagatesWithItsStack()
    {
        var original = new InvalidOperationException("engine defect");

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => Loading.Load<int>("any.doc", null, () => throw original));

        Assert.Same(original, thrown);
        Assert.Contains(nameof(AnUnclassifiedFailure_PropagatesWithItsStack), thrown.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void ACliErrorAndACancellation_PassUnchanged()
    {
        CliException own = CliErrors.FileTooLarge(2, 1);

        Assert.Same(own, Assert.Throws<CliException>(() => Loading.Load<int>("any.doc", null, () => throw own)));
        Assert.Throws<OperationCanceledException>(() => Loading.Load<int>("any.doc", null, () => throw new OperationCanceledException()));
    }

    [Fact]
    public void AnAuxiliaryRead_TranslatesOnlyAccessFailures()
    {
        using var workspace = new TempWorkspace();
        string missing = workspace.File("missing.png");
        string present = workspace.File("present.png");
        File.WriteAllText(present, "x");

        Assert.Equal(ErrorCodes.FileNotFound,
            Assert.Throws<CliException>(() => InputFailures.Read(missing, () => InputFiles.OpenRead(missing))).Code);
        Assert.Throws<EndOfStreamException>(() => InputFailures.Read<int>(present, () => throw new EndOfStreamException()));
    }

    private static CliException Fail(string path, Secret? password, Exception exception) =>
        Assert.Throws<CliException>(() => Loading.Load<int>(path, password, () => throw exception));
}
