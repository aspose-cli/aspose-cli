using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.App;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;
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
        byte[] status = Encoding.UTF8.GetBytes(workspace.Run("license", "status", "--output-format", "json").StdOut);
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
    public void LicenseStatus_IsReadAfreshForARequesterWithAnotherLicense()
    {
        using var workspace = new TempWorkspace();
        byte[] status = Encoding.UTF8.GetBytes(workspace.Run("license", "status", "--output-format", "json").StdOut);
        int runs = 0;
        AppCliGateway gateway = Gateway(workspace, license: null, _ =>
        {
            Interlocked.Increment(ref runs);
            return new ChildProcessResult(0, status, []);
        });
        var first = new Dictionary<string, string> { ["ASPOSE_LICENSE_B64"] = "Zmlyc3Q=" };

        gateway.LicenseStatus();
        gateway.Use(Requester(workspace, license: null));
        gateway.LicenseStatus();
        Assert.Equal(1, runs);

        gateway.Use(Requester(workspace, license: null, first));
        gateway.LicenseStatus();
        gateway.Use(Requester(workspace, license: null, first));
        gateway.LicenseStatus();
        Assert.Equal(2, runs);

        gateway.Use(Requester(workspace, license: null, new Dictionary<string, string> { ["ASPOSE_LICENSE_B64"] = "c2Vjb25k" }));
        gateway.LicenseStatus();
        Assert.Equal(3, runs);
    }

    [Fact]
    public void Fonts_AreReadOnceAndAskedAfreshAfterTheLicenseChanges()
    {
        using var workspace = new TempWorkspace();
        string license = workspace.File("selected.lic");
        File.WriteAllText(license, "<License/>");
        byte[] status = Encoding.UTF8.GetBytes(workspace.Run("license", "status", "--output-format", "json").StdOut);
        byte[] error = Envelope(new ErrorPayload
        {
            Code = "LICENSE_INVALID", Message = "The license is invalid.", Hint = "Repair it.",
        });
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
        byte[] error = Envelope(new ErrorPayload { Code = "OPERATION_TIMEOUT", Message = "Timed out.", Hint = "Retry." });
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
    public void Fonts_AreAskedAgainOnceTheirAnswerIsOld()
    {
        using var workspace = new TempWorkspace();
        byte[] fonts = Encoding.UTF8.GetBytes(
            workspace.Run("fonts", "list", "--product", "words", "--output-format", "json").Succeeded().StdOut);
        int fontRuns = 0;
        var clock = new ManualClock();
        AppCliGateway gateway = Gateway(workspace, license: null, _ =>
        {
            Interlocked.Increment(ref fontRuns);
            return new ChildProcessResult(0, fonts, []);
        }, clock);

        gateway.Fonts("words");
        // The App keeps a font list for five minutes.
        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        gateway.Fonts("words");
        Assert.Equal(1, fontRuns);

        // A font installed while the App runs shows once the kept answer is this old.
        clock.Advance(TimeSpan.FromSeconds(1));
        gateway.Fonts("words");
        Assert.Equal(2, fontRuns);
    }

    [Fact]
    public void Fonts_ReadUnderAnOldLicenseAreNotKeptAfterItChanges()
    {
        using var workspace = new TempWorkspace();
        byte[] status = Encoding.UTF8.GetBytes(workspace.Run("license", "status", "--output-format", "json").StdOut);
        byte[] error = Envelope(new ErrorPayload
        {
            Code = "LICENSE_INVALID", Message = "The license is invalid.", Hint = "Repair it.",
        });
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
    public void ChildFailure_KeepsTheChildsWholeError()
    {
        using var workspace = new TempWorkspace();
        byte[] error = Envelope(new ErrorPayload
        {
            Code = "FILE_NOT_FOUND",
            Message = "File not found: missing.lic",
            Details = new JsonObject { ["path"] = @"C:\licenses\missing.lic" },
            Hint = "Check the path.",
            Docs = "licensing",
        });
        AppCliGateway gateway = Gateway(
            workspace, license: null, _ => new ChildProcessResult((int)ExitCode.InputError, [], error));

        CliException failure = Assert.Throws<CliException>(() => gateway.RemoveLicense(productId: null));

        Assert.Equal(ErrorCodes.FileNotFound.Name, failure.Code.Name);
        Assert.Equal(ExitCode.InputError, failure.ExitCode);
        Assert.Equal("Check the path.", failure.Hint);
        Assert.Equal(@"C:\licenses\missing.lic", failure.Details?["path"]?.GetValue<string>());
        Assert.Equal("licensing", failure.Docs);
    }

    [Fact]
    public void ChildFailure_KeepsSuggestions()
    {
        using var workspace = new TempWorkspace();
        byte[] error = Envelope(new ErrorPayload
        {
            Code = "PRODUCT_NOT_FOUND",
            Message = "No product 'wrods'.",
            Details = new JsonObject { ["requested"] = "wrods", ["suggestions"] = new JsonArray("words", "slides") },
        });
        AppCliGateway gateway = Gateway(
            workspace, license: null, _ => new ChildProcessResult((int)ExitCode.InputError, [], error));

        CliException failure = Assert.Throws<CliException>(() => gateway.RemoveLicense(productId: null));

        Assert.Equal("PRODUCT_NOT_FOUND", failure.Code.Name);
        Assert.Equal(["words", "slides"], failure.Details?["suggestions"]?.AsArray().Select(item => item!.GetValue<string>()));
    }

    /// <summary>The stderr of a failed child: the error envelope every command writes.</summary>
    private static byte[] Envelope(ErrorPayload error) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new ErrorEnvelope { Error = error }, SdkJsonContext.Default.ErrorEnvelope));

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
            Requester(workspace, license),
            workspace.ConfigDirectory,
            run,
            clock);

    private static AppLicenseContext Requester(
        TempWorkspace workspace, string? license, IReadOnlyDictionary<string, string>? variables = null) =>
        AppLicenseContext.Of(
            ActualCommandTree.Host.Catalog,
            new GlobalValues(OutputMode.Json, Quiet: true, Verbose: false, license, workspace.Path,
                TimeoutSeconds: null, MaxInputBytes: 1024 * 1024),
            name => variables?.GetValueOrDefault(name));

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
