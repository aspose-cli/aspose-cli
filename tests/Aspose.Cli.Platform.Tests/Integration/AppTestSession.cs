using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>One real App process and its isolated workspace, used only by the lifecycle test collection.</summary>
internal sealed class AppTestSession : IAsyncDisposable
{
    internal TempWorkspace Workspace { get; } = new();
    internal HttpClient Client { get; private set; } = null!;
    internal Uri Url { get; private set; } = null!;
    private bool _started;
    private readonly List<System.Diagnostics.Process> _processes = [];

    internal static async Task<AppTestSession> Start()
    {
        var session = new AppTestSession();
        try
        {
            File.WriteAllText(session.Workspace.File("first.csv"), "Label,Value\nFIRST_DOCUMENT,42\n");
            File.WriteAllText(session.Workspace.File("second.csv"), "Label,Value\nSECOND_DOCUMENT,99\n");
            CliResult started = session.Open("first.csv");
            session._started = true;
            session.Url = new Uri(JsonNode.Parse(started.StdOut)!["url"]!.GetValue<string>());
            // The client a browser would be: it entered once through the address the CLI returned.
            session.Client = await LocalServiceEntry.Client(session.Url.AbsoluteUri, TimeSpan.FromSeconds(30));
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    internal CliResult Open(string file)
    {
        CliResult result = Workspace.Run("app", "open", Workspace.File(file), "--no-open", "--output-format", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
        int pid = JsonNode.Parse(result.StdOut)!["pid"]!.GetValue<int>();
        if (!_processes.Any(process => process.Id == pid))
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            _ = process.Handle;
            _processes.Add(process);
        }
        return result;
    }

    internal async Task<JsonNode> Status() =>
        JsonNode.Parse(await Client.GetStringAsync("/api/status"))!;

    internal async Task<JsonNode> Preferences(string view, bool remember = true, string product = "cells")
    {
        string payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            product, defaultView = view, rememberRecentFiles = remember,
        });
        using HttpResponseMessage response = await Client.PostAsync("/api/preferences",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_started && Client is not null)
            {
                try { using var response = await Client.PostAsync("/api/stop", null).WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception exception) when (exception is HttpRequestException or TimeoutException or TaskCanceledException) { }
            }
            foreach (System.Diagnostics.Process process in _processes)
            {
                try
                {
                    try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (TimeoutException)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                }
                finally { process.Dispose(); }
            }
        }
        finally
        {
            Client?.Dispose();
            Workspace.Dispose();
        }
    }
}
