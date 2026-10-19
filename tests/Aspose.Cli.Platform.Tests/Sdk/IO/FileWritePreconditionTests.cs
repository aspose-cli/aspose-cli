using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class FileWritePreconditionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Admit_MatchingIfMatch_DescribesTheInput()
    {
        string input = Source("admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);
        string sha256 = FileFingerprints.Capture(input).Sha256;

        InputInfo admitted = precondition.Admit("txt", new InputProtection(Encrypted: true), $" {sha256.ToUpperInvariant()} ");

        Assert.Equal(Path.GetFullPath(input), admitted.Path);
        Assert.Equal("txt", admitted.Format);
        Assert.True(admitted.Encrypted);
        Assert.Equal(sha256, admitted.Fingerprint!.Sha256);
    }

    [Fact]
    public void Admit_StaleIfMatch_IsInputChanged()
    {
        string input = Source("admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);
        string stale = new('0', 64);

        CliException error = Assert.Throws<CliException>(() =>
            precondition.Admit("txt", new InputProtection(Encrypted: false), stale));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.Equal(stale, error.Details!["expectedSha256"]!.GetValue<string>());
        Assert.Equal(FileFingerprints.Capture(input).Sha256, error.Details["observedSha256"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("not-a-sha256")]
    [InlineData("0123456789abcdef")]
    public void Admit_MalformedIfMatch_IsOptionInvalid(string ifMatch)
    {
        string input = Source("admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);

        CliException error = Assert.Throws<CliException>(() =>
            precondition.Admit("txt", new InputProtection(Encrypted: false), ifMatch));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
    }

    [Fact]
    public void Admit_InputChangedWhileLoading_IsInputChangedBeforeTheIfMatchIsJudged()
    {
        string input = Source("admitted");
        FileWritePrecondition precondition = FileWritePrecondition.Capture(input);
        string admittedSha256 = precondition.Fingerprint.Sha256;
        File.WriteAllText(input, "changed while the engine loaded it");

        // Even a malformed --if-match is not judged: the change during loading is reported first.
        CliException error = Assert.Throws<CliException>(() =>
            precondition.Admit("txt", new InputProtection(Encrypted: false), "not-a-sha256"));

        Assert.Equal(ErrorCodes.InputChanged, error.Code);
        Assert.Equal(admittedSha256, error.Details!["expectedSha256"]!.GetValue<string>());
        Assert.Equal(FileFingerprints.Capture(input).Sha256, error.Details["observedSha256"]!.GetValue<string>());
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
