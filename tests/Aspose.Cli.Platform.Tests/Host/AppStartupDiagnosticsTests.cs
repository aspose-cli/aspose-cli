using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Aspose.Cli.Host.App;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class AppStartupDiagnosticsTests
{
    public static TheoryData<
        string,
        Exception,
        string> Stages => new()
    {
        {
            "Configuration",
            new JsonException("private configuration text"),
            "invalid-configuration"
        },
        {
            "Permissions",
            new UnauthorizedAccessException("private storage path"),
            "permission-denied"
        },
        {
            "Storage",
            new IOException("private storage path"),
            "io-failure"
        },
        {
            "Listener",
            new HttpListenerException(5),
            "permission-denied"
        },
        {
            "Control",
            new IOException("private pipe name"),
            "io-failure"
        },
        {
            "BrowserLaunch",
            new Win32Exception(2, "private browser command"),
            "platform-failure"
        },
    };

    [Theory]
    [MemberData(nameof(Stages))]
    public void Failure_UsesAnActionableBoundedStageContract(
        string stageName,
        Exception cause,
        string expectedReason)
    {
        AppStartupStage stage =
            Enum.Parse<AppStartupStage>(stageName);
        string expectedStage = stageName switch
        {
            "BrowserLaunch" => "browser-launch",
            _ => stageName.ToLowerInvariant(),
        };
        CliException error =
            AppStartupDiagnostics.Failure(stage, cause);

        Assert.Equal(ErrorCodes.AppStartupFailed, error.Code);
        Assert.Equal(ExitCode.OutputError, error.ExitCode);
        Assert.Equal(
            expectedStage,
            error.Details!["stage"]!.GetValue<string>());
        Assert.Equal(
            expectedReason,
            error.Details["reason"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(error.Hint));
        Assert.DoesNotContain(
            cause.Message,
            error.Message,
            StringComparison.Ordinal);
        Assert.True(
            AppStartupDiagnostics.TryReadStage(
                error.Message,
                out AppStartupStage parsed));
        Assert.Equal(stage, parsed);
    }

    [Fact]
    public void BrowserWarning_IsNonFatalAndRedactsPrivatePaths()
    {
        string warning = AppStartupDiagnostics.BrowserWarning(
            new Win32Exception(
                2,
                "C:\\Users\\Alice\\private-browser.exe failed"),
            "http://127.0.0.1:1234/home");

        Assert.Contains("browser-launch", warning, StringComparison.Ordinal);
        Assert.Contains(
            "http://127.0.0.1:1234/home",
            warning,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "C:\\Users\\Alice",
            warning,
            StringComparison.OrdinalIgnoreCase);
    }
}
