using System.Text.RegularExpressions;
using Aspose.Cli.Host.App;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// When the child CLI answers the App with something it cannot read, the error names the command
/// the child ran, built with <c>ContinuationCommand</c> (D14), so the person at the prompt can run it
/// as written to see why: a license file on a path with a space is one argument.
/// </summary>
public sealed partial class AppCliGatewayHintTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("cells")]
    public void UnreadableAnswer_NamesTheCommandItRanAsWritten(string? product)
    {
        using var workspace = new TempWorkspace();
        Directory.CreateDirectory(workspace.File("my licenses"));
        string license = workspace.File(Path.Combine("my licenses", "Aspose Total.lic"));
        File.WriteAllText(license, "<License/>");
        AppCliGateway gateway = new(
            ActualCommandTree.Host.Catalog,
            AppLicenseContext.Of(
                ActualCommandTree.Host.Catalog,
                new GlobalValues(OutputMode.Json, Quiet: true, Verbose: false, (string?)null, workspace.Path,
                    TimeoutSeconds: null, MaxInputBytes: 1024 * 1024),
                static _ => null),
            workspace.ConfigDirectory,
            static _ => new ChildProcessResult(0, "{}"u8.ToArray(), []),
            clock: null);

        CliException failure = Assert.Throws<CliException>(() => gateway.InstallLicense(license, product));

        Match quoted = QuotedCommand().Match(failure.Hint ?? string.Empty);
        Assert.True(quoted.Success, failure.Hint);
        string command = quoted.Groups[1].Value;
        Assert.Contains($"license install \"{license}\"", command, StringComparison.Ordinal);
        CliResult run = workspace.RunCommandLine(command);
        Assert.NotEqual((int)ExitCode.Usage, run.ExitCode);
        Assert.DoesNotContain("USAGE_ERROR", run.StdErr, StringComparison.Ordinal);
    }

    [GeneratedRegex("'(aspose-cli [^']+)'")]
    private static partial Regex QuotedCommand();
}
