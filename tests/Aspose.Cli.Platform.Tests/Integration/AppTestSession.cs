using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
            session.Client = new HttpClient(new HttpClientHandler { UseCookies = false })
            {
                BaseAddress = new Uri(session.Url.GetLeftPart(UriPartial.Authority)),
                Timeout = TimeSpan.FromSeconds(30),
            };
            string shell = await session.Client.GetStringAsync("/");
            string csrf = Regex.Match(shell, @"name=""aspose-csrf"" content=""([^""]+)""").Groups[1].Value;
            Assert.NotEmpty(csrf);
            session.Client.DefaultRequestHeaders.Add("Origin", session.Client.BaseAddress.GetLeftPart(UriPartial.Authority));
            session.Client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
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
        CliResult result = Workspace.Run("app", Workspace.File(file), "--no-open", "--output", "json");
        Assert.True(result.ExitCode == 0, result.StdErr);
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

    public ValueTask DisposeAsync()
    {
        try
        {
            if (_started) { _ = Workspace.Run("app", "stop", "--output", "json"); }
        }
        finally
        {
            Client?.Dispose();
            Workspace.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}
