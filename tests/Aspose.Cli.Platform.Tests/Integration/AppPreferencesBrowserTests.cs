using System.Text.Json.Nodes;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppPreferencesBrowserTests(ITestOutputHelper output)
{
    [Fact]
    public Task Drafts_SurvivePollingNavigationAndProductChanges() =>
        BrowserApp.Run("preference-drafts", output, async ui =>
        {
            await ui.Settings();
            await ui.Page.Locator("#default-view").SelectOptionAsync("sheets");
            await ui.Page.Locator("#remember-recents").UncheckAsync();
            await ui.Poll();
            await ExpectDraft(ui, "sheets", false);
            await ui.Page.Locator("a[data-route='/preview']").ClickAsync();
            await ui.Settings();
            await ExpectDraft(ui, "sheets", false);

            File.WriteAllText(ui.App.Workspace.File("text.txt"), "Draft ownership");
            Assert.Equal(0, ui.App.Workspace.Run("words", "create", "draft.docx", "--text", "text.txt").ExitCode);
            ui.App.Open("draft.docx");
            await ui.Poll();
            await ExpectDraft(ui, "pages", false);
            Assert.True((await ui.Save())["ok"]!.GetValue<bool>());

            ui.App.Open("first.csv");
            await ui.Poll();
            await ExpectDraft(ui, "sheets", false);
            Assert.Equal("workbook", (await ui.App.Status())["defaultView"]!.GetValue<string>());
            Assert.True((await ui.Save())["ok"]!.GetValue<bool>());
            await ui.WaitForPreview("first.csv", "sheets");
            JsonNode settings = ReadSettings(ui);
            Assert.Equal("sheets", settings["previewViews"]!["cells"]!.GetValue<string>());
            Assert.Equal("pages", settings["previewViews"]!["words"]!.GetValue<string>());
            Assert.False(settings["rememberRecentFiles"]!.GetValue<bool>());
        });

    [Fact]
    public Task SaveAcknowledgement_PreservesLaterEditsAndPreventsConcurrentSubmissions() =>
        BrowserApp.Run("preference-acknowledgement", output, async ui =>
        {
            await ui.Settings();
            await ui.Page.Locator("#default-view").SelectOptionAsync("sheets");
            await ui.Page.Locator("#remember-recents").UncheckAsync();
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int requests = 0;
            await ui.Page.RouteAsync("**/api/preferences", async route =>
            {
                requests++;
                if (requests > 1) { await route.ContinueAsync(); return; }
                IAPIResponse response = await route.FetchAsync();
                captured.SetResult();
                await release.Task;
                await route.FulfillAsync(new() { Response = response });
            });
            try
            {
                Task<JsonNode> save = ui.Save();
                await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal("sheets", (await ui.App.Status())["defaultView"]!.GetValue<string>());
                await ui.Page.Locator("#default-view").SelectOptionAsync("workbook");
                await ui.Page.Locator("#remember-recents").CheckAsync();
                await ui.Poll();
                await ExpectDraft(ui, "workbook", true);
                await Assertions.Expect(ui.Page.Locator("#save-preferences")).ToBeDisabledAsync();
                await ui.Page.Locator("#save-preferences").DispatchEventAsync("click");
                Assert.Equal(1, requests);
                release.SetResult();
                Assert.True((await save)["ok"]!.GetValue<bool>());
                await ui.Poll();
                await ExpectDraft(ui, "workbook", true);
                Assert.True((await ui.Save())["ok"]!.GetValue<bool>());
                Assert.Equal(2, requests);
                Assert.Equal("workbook", (await ui.App.Status())["defaultView"]!.GetValue<string>());
                Assert.True(ReadSettings(ui)["rememberRecentFiles"]!.GetValue<bool>());
            }
            finally { release.TrySetResult(); }
        });

    [Fact]
    public Task PersistenceFailure_KeepsTheDraftAndPreviewUntilRetrySucceeds() =>
        BrowserApp.Run("preference-write-failure", output, async ui =>
        {
            await ui.Settings();
            string preferences = Path.Combine(ui.App.Workspace.ConfigDirectory, "app-settings.json");
            byte[] original = File.ReadAllBytes(preferences);
            string url = (await ui.App.Status())["previewUrl"]!.GetValue<string>();
            await ui.Page.Locator("#default-view").SelectOptionAsync("sheets");
            await ui.Page.Locator("#remember-recents").UncheckAsync();
            using (var held = new FileStream(preferences, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.False((await ui.Save())["ok"]!.GetValue<bool>());
                await ui.Poll();
                await ExpectDraft(ui, "sheets", false);
                Assert.Equal(original, File.ReadAllBytes(preferences));
                Assert.Equal(url, (await ui.App.Status())["previewUrl"]!.GetValue<string>());
            }
            Assert.True((await ui.Save())["ok"]!.GetValue<bool>());
            await ui.WaitForPreview("first.csv", "sheets");
            Assert.False(ReadSettings(ui)["rememberRecentFiles"]!.GetValue<bool>());
        });

    [Fact]
    public Task SavedButUnrefreshedFeedback_SurvivesPollingAndAllowsAnUnchangedRetry() =>
        BrowserApp.Run("preference-refresh-feedback", output, async ui =>
        {
            await ui.Settings();
            await ui.Page.Locator("#default-view").SelectOptionAsync("sheets");
            using (var held = new FileStream(ui.App.Workspace.File("first.csv"),
                FileMode.Open, FileAccess.Read, FileShare.None))
            {
                JsonNode saved = await ui.Save();
                Assert.Equal("PREVIEW_REFRESH_FAILED", saved["code"]!.GetValue<string>());
                await ui.Poll();
                await ExpectDraft(ui, "sheets", true);
                await Assertions.Expect(ui.Page.Locator("#preferences-feedback")).ToContainTextAsync("Preferences were saved");
                await Assertions.Expect(ui.Page.Locator("#save-preferences")).ToBeEnabledAsync();
                await ui.WaitForPreview("first.csv", "workbook");
            }
            JsonNode retry = await ui.Save();
            Assert.True(retry["ok"]!.GetValue<bool>());
            Assert.Null(retry["code"]);
            await ui.WaitForPreview("first.csv", "sheets");
            await Assertions.Expect(ui.Page.Locator("#preferences-feedback")).ToHaveTextAsync("Preferences saved.");
        });

    private static JsonNode ReadSettings(BrowserApp ui) => JsonNode.Parse(File.ReadAllText(
        Path.Combine(ui.App.Workspace.ConfigDirectory, "app-settings.json")))!;

    private static async Task ExpectDraft(BrowserApp ui, string view, bool remember)
    {
        await Assertions.Expect(ui.Page.Locator("#default-view")).ToHaveValueAsync(view);
        if (remember) { await Assertions.Expect(ui.Page.Locator("#remember-recents")).ToBeCheckedAsync(); }
        else { await Assertions.Expect(ui.Page.Locator("#remember-recents")).Not.ToBeCheckedAsync(); }
    }
}
