using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppPreferencesHttpTests
{
    [Fact]
    public async Task SavedPreferences_WithLockedInput_KeepTheOldPreviewAndRetryTheSameView()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        string input = workspace.File("data.csv");
        File.WriteAllText(input, "Heading,Value\nA,42\n");
        try
        {
            CliResult start = workspace.Run("app", "open", input, "--no-open", "--output", "json");
            Assert.True(start.ExitCode == 0, start.StdErr);
            var url = new Uri(JsonNode.Parse(start.StdOut)!["url"]!.GetValue<string>());
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false })
            {
                BaseAddress = new Uri(url.GetLeftPart(UriPartial.Authority)),
                Timeout = TimeSpan.FromSeconds(30),
            };
            string shell = await client.GetStringAsync("/");
            string csrf = Regex.Match(shell, @"name=""aspose-csrf"" content=""([^""]+)""").Groups[1].Value;
            Assert.NotEmpty(csrf);
            client.DefaultRequestHeaders.Add("Origin", client.BaseAddress.GetLeftPart(UriPartial.Authority));
            client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
            string oldPreview = await PreviewUrl(client);
            const string preferences = """{"product":"cells","defaultView":"sheets","rememberRecentFiles":true}""";
            using (var held = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using HttpResponseMessage response = await client.PostAsync("/api/preferences",
                    new StringContent(preferences, Encoding.UTF8, "application/json"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                JsonNode result = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                Assert.True(result["ok"]!.GetValue<bool>());
                Assert.Equal("PREVIEW_REFRESH_FAILED", result["code"]!.GetValue<string>());
                Assert.Contains("saved", result["message"]!.GetValue<string>(), StringComparison.Ordinal);
                Assert.Equal(oldPreview, await PreviewUrl(client));
                JsonNode saved = JsonNode.Parse(File.ReadAllText(
                    Path.Combine(workspace.ConfigDirectory, "app-settings.json")))!;
                Assert.Equal("sheets", saved["previewViews"]!["cells"]!.GetValue<string>());
            }
            using HttpResponseMessage retry = await client.PostAsync("/api/preferences",
                new StringContent(preferences, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            JsonNode retried = JsonNode.Parse(await retry.Content.ReadAsStringAsync())!;
            Assert.True(retried["ok"]!.GetValue<bool>());
            Assert.Null(retried["code"]);
            Assert.NotEqual(oldPreview, await PreviewUrl(client));
        }
        finally { _ = workspace.Run("app", "stop", "--output", "json"); }
    }

    /// <summary>Where the App says the open document is being served.</summary>
    private static async Task<string> PreviewUrl(HttpClient client) =>
        JsonNode.Parse(await client.GetStringAsync("/api/status"))!["previewUrl"]!.GetValue<string>();
}
