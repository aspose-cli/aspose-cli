using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class FileWritePreconditionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Admit_DescribesTheInput()
    {
        string input = Source("admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);

        InputInfo admitted = precondition.Admit("txt", new InputProtection(Encrypted: true));

        Assert.Equal(Path.GetFullPath(input), admitted.Path);
        Assert.Equal("txt", admitted.Format);
        Assert.True(admitted.Encrypted);
    }

    [Fact]
    public void Admit_InputChangedWhileLoading_IsInputChanged()
    {
        string input = Source("admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);
        string admittedSha256 = precondition.Fingerprint.Sha256;
        File.WriteAllText(input, "changed while the engine loaded it");

        CliException error = Assert.Throws<CliException>(() =>
            precondition.Admit("txt", new InputProtection(Encrypted: false)));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.Equal(admittedSha256, error.Details!["expectedSha256"]!.GetValue<string>());
        Assert.Equal(FileFingerprints.Capture(input).Sha256, error.Details["actualSha256"]!.GetValue<string>());
    }

    [Fact]
    public void EnsureCopyMatches_ACopyOfOtherBytes_IsInputChanged()
    {
        string input = Source("admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);
        string same = _temp.File("same.txt");
        string other = _temp.File("other.txt");
        File.Copy(input, same);
        File.WriteAllText(other, "other bytes");

        precondition.EnsureCopyMatches(same);
        CliException error = Assert.Throws<CliException>(() => precondition.EnsureCopyMatches(other));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
    }

    private string Source(string text)
    {
        string path = _temp.File("input.txt");
        File.WriteAllText(path, text);
        return path;
    }
}
