using System.Text;
using Aspose.Cli.Host.App;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class AppCliGatewayTests
{
    [Fact]
    public void LicenseStatus_IsReadOnceUntilTheLicenseChanges()
    {
        using var workspace = new TempWorkspace();
        string license = workspace.File("selected.lic");
        File.WriteAllText(license, "<License/>");
        byte[] status = Encoding.UTF8.GetBytes(workspace.Run("license", "status", "--output", "json").StdOut);
        int runs = 0;
        AppCliGateway gateway = Gateway(workspace, license, _ =>
        {
            Interlocked.Increment(ref runs);
            Thread.Sleep(50);
            return new ChildProcessResult(0, status, []);
        });

        gateway.LicenseStatus();
        Parallel.For(0, 8, _ => gateway.LicenseStatus());
        Assert.Equal(1, runs);

        File.WriteAllText(license, "<License>replaced</License>");
        gateway.LicenseStatus();
        gateway.LicenseStatus();
        Assert.Equal(2, runs);
    }

    [Fact]
    public void Fonts_AreReadOnceAndAskedAfreshAfterTheLicenseChanges()
    {
        using var workspace = new TempWorkspace();
        string license = workspace.File("selected.lic");
        File.WriteAllText(license, "<License/>");
        byte[] status = Encoding.UTF8.GetBytes(workspace.Run("license", "status", "--output", "json").StdOut);
        byte[] error = Encoding.UTF8.GetBytes(
            """{"error":{"code":"LICENSE_INVALID","message":"The license is invalid.","hint":"Repair it."}}""");
        int fontRuns = 0;
        AppCliGateway gateway = Gateway(workspace, license, arguments =>
        {
            if (arguments[0] == "license") { return new ChildProcessResult(0, status, []); }
            Interlocked.Increment(ref fontRuns);
            return new ChildProcessResult((int)ExitCode.LicenseError, [], error);
        });

        gateway.LicenseStatus();
        // A failure is an answer too: the status polls do not start a child each time.
        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        gateway.LicenseStatus();
        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        Assert.Equal(1, fontRuns);

        // A license repaired at a prompt is noticed by the next status, and fonts follow it.
        File.WriteAllText(license, "<License>repaired</License>");
        gateway.LicenseStatus();
        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        Assert.Equal(2, fontRuns);
    }

    [Fact]
    public void FontFailure_IsAskedAgainAfterAShortInterval()
    {
        using var workspace = new TempWorkspace();
        byte[] error = Encoding.UTF8.GetBytes(
            """{"error":{"code":"OPERATION_TIMEOUT","message":"Timed out.","hint":"Retry."}}""");
        int fontRuns = 0;
        var clock = new ManualClock();
        AppCliGateway gateway = Gateway(workspace, license: null, _ =>
        {
            Interlocked.Increment(ref fontRuns);
            return new ChildProcessResult((int)ExitCode.OperationTimeout, [], error);
        }, clock);

        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        clock.Advance(AppCliGateway.FontFailureRetryInterval - TimeSpan.FromSeconds(1));
        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        Assert.Equal(1, fontRuns);

        // A transient failure is not the answer for the session: reopening Settings later asks again.
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        Assert.Equal(2, fontRuns);
    }

    [Fact]
    public void Fonts_ReadUnderAnOldLicenseAreNotKeptAfterItChanges()
    {
        using var workspace = new TempWorkspace();
        byte[] status = Encoding.UTF8.GetBytes(workspace.Run("license", "status", "--output", "json").StdOut);
        byte[] error = Encoding.UTF8.GetBytes(
            """{"error":{"code":"LICENSE_INVALID","message":"The license is invalid.","hint":"Repair it."}}""");
        int fontRuns = 0;
        AppCliGateway? gateway = null;
        gateway = Gateway(workspace, license: null, arguments =>
        {
            if (arguments[0] == "license") { return new ChildProcessResult(0, status, []); }
            if (Interlocked.Increment(ref fontRuns) == 1)
            {
                // The license changes while the first font read is still running.
                gateway!.RemoveLicense(productId: null);
            }
            return new ChildProcessResult((int)ExitCode.LicenseError, [], error);
        });

        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        Assert.Equal(2, fontRuns);
        Assert.Throws<CliException>(() => gateway.Fonts("words"));
        Assert.Equal(2, fontRuns);
    }

    [Fact]
    public void ChildFailure_KeepsTheChildsCodeAndExitCode()
    {
        using var workspace = new TempWorkspace();
        byte[] error = Encoding.UTF8.GetBytes(
            """{"error":{"code":"FILE_NOT_FOUND","message":"File not found: missing.lic","hint":"Check the path."}}""");
        AppCliGateway gateway = Gateway(
            workspace, license: null, _ => new ChildProcessResult((int)ExitCode.InputError, [], error));

        CliException failure = Assert.Throws<CliException>(() => gateway.RemoveLicense(productId: null));

        Assert.Equal(ErrorCodes.FileNotFound.Name, failure.Code.Name);
        Assert.Equal(ExitCode.InputError, failure.ExitCode);
    }

    [Fact]
    public void ErrorMessages_NameTheInstalledProductsAndMatchCodesByName()
    {
        var messages = new AppErrorMessages(ActualCommandTree.Host.Catalog);

        string unsupported = messages.For(ErrorCodes.FormatUnsupported);
        Assert.All(ActualCommandTree.Host.Catalog.Products, product =>
            Assert.Contains(product.Manifest.DisplayName, unsupported, StringComparison.Ordinal));
        Assert.Equal(
            messages.For(ErrorCodes.FileNotFound),
            messages.For(new ErrorCode(ErrorCodes.FileNotFound.Name, ExitCode.Internal)));
    }

    private static AppCliGateway Gateway(
        TempWorkspace workspace,
        string? license,
        Func<IReadOnlyList<string>, ChildProcessResult> run,
        TimeProvider? clock = null) =>
        new(
            ActualCommandTree.Host.Catalog,
            new GlobalValues(OutputMode.Json, Quiet: true, Verbose: false, license, workspace.Path,
                TimeoutSeconds: null, MaxInputBytes: 1024 * 1024),
            workspace.ConfigDirectory,
            run,
            clock);

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
