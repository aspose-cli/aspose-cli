using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class FileAccessProbeTests
{
    [Theory]
    [InlineData(unchecked((int)0x80070020))]
    [InlineData(unchecked((int)0x80070021))]
    public void NativeSharingAndLockFailures_AreRecognized(int hresult)
    {
        Assert.True(FileAccessProbe.IsSharingViolation(new IOException("Native file lock", hresult)));
    }

    [Fact]
    public void ParserIoFailures_DoNotBecomeFileLocks()
    {
        Assert.False(FileAccessProbe.IsSharingViolation(new EndOfStreamException("Truncated container")));
        Assert.False(FileAccessProbe.IsSharingViolation(new IOException("Invalid container")));
        Assert.False(FileAccessProbe.IsSharingViolation(new IOException(
            "Unrelated error with matching low bits", unchecked((int)0x80130020))));
    }

    [Fact]
    public void LockedFileGuidance_IsIndependentOfTheDocumentProduct()
    {
        CliException error = CliErrors.FileLocked("document.docx");
        Assert.Equal(ErrorCodes.FileLocked, error.Code);
        Assert.DoesNotContain("Excel", error.Hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("application", error.Hint!, StringComparison.OrdinalIgnoreCase);
    }
}
