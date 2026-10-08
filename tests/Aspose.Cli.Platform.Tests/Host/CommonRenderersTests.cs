using Aspose.Cli.Host.Output.Rendering;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>Pins the table and text output of the product-neutral Host renderers.</summary>
public sealed class CommonRenderersTests
{
    [Fact]
    public void Update_UpToDate_PrintsOnlyTheFieldsTheCheckReported()
    {
        var update = new UpdateResult
        {
            Status = "up-to-date",
            CurrentVersion = "1.4.0",
            SourceRevision = "0123456789abcdef",
            Feed = "https://example.test/release-manifest.json",
            ArchiveSha256 = "ab12",
        };

        string text = RenderedText.Of(surface => CommonRenderers.Render(update, surface));

        RenderedText.Equal(
            """
            status:    up-to-date
            current:   1.4.0
            source:    0123456789abcdef
            feed:      https://example.test/release-manifest.json
            sha256:    ab12
            """,
            text);
    }

    [Fact]
    public void Update_PendingInstall_PrintsTheAvailableVersionAndTheInstallerProcess()
    {
        var update = new UpdateResult
        {
            Status = "pending",
            CurrentVersion = "1.4.0",
            AvailableVersion = "1.5.0",
            SourceRevision = "fedcba9876543210",
            Feed = @"C:\feeds\release-manifest.json",
            ArchiveSha256 = "cd34",
            ProcessId = 4242,
        };

        string text = RenderedText.Of(surface => CommonRenderers.Render(update, surface));

        RenderedText.Equal(
            """
            status:    pending
            current:   1.4.0
            available: 1.5.0
            source:    fedcba9876543210
            feed:      C:\feeds\release-manifest.json
            sha256:    cd34
            installer: pid 4242
            """,
            text);
    }
}
