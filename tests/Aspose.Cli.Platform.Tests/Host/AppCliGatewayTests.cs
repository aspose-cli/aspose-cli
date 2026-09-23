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
        Func<IReadOnlyList<string>, ChildProcessResult> run) =>
        new(
            ActualCommandTree.Host.Catalog,
            new GlobalValues(OutputMode.Json, Quiet: true, Verbose: false, license, workspace.Path,
                TimeoutSeconds: null, MaxInputBytes: 1024 * 1024),
            workspace.ConfigDirectory,
            run);
}
