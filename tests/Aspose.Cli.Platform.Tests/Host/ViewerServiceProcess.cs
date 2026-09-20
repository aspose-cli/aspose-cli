using System.Diagnostics;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// A real viewer service for tests that host the App in process. The App
/// renders nothing itself, so it needs the service that does; in process the
/// service cannot start itself, because this process is not the CLI.
/// </summary>
internal sealed class ViewerServiceProcess : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private readonly Process _process;

    public ViewerServiceProcess(string configDirectory)
    {
        var start = new ProcessStartInfo(CliRunner.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("__viewer-service");
        start.ArgumentList.Add("--quiet");
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add("json");
        start.Environment["ASPOSE_CLI_CONFIG_DIR"] = configDirectory;
        _process = Process.Start(start)
            ?? throw new InvalidOperationException("The viewer service could not be started.");
        string marker = Path.Combine(configDirectory, "viewer", "service.json");
        var waited = Stopwatch.StartNew();
        while (!File.Exists(marker))
        {
            Assert.False(_process.HasExited, "The viewer service exited before publishing itself.");
            Assert.True(waited.Elapsed < Timeout, "The viewer service did not publish itself in time.");
            Thread.Sleep(50);
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5_000);
            }
        }
        catch (InvalidOperationException)
        {
            // The service had already gone.
        }
        finally
        {
            _process.Dispose();
        }
    }
}
