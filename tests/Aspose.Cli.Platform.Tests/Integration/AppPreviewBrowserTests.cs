using System.Text.Json.Nodes;
using Microsoft.Playwright;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

[Collection("Local service lifecycle")]
public sealed class AppPreviewBrowserTests(ITestOutputHelper output)
{
    [Fact]
    public Task ProductSwitch_ReplacesTheActualPreview() =>
        BrowserApp.Run("product-switch", output, async ui =>
        {
            File.WriteAllText(ui.App.Workspace.File("words.txt"), "WORD_DOCUMENT");
            var created = ui.App.Workspace.Run("words", "create", "words.docx",
                "--text", "words.txt", "--output", "json");
            Assert.True(created.ExitCode == 0, created.StdErr);
            ui.App.Open("words.docx");
            await ui.Poll();
            await ui.WaitForPreview("words.docx", "pages");
            Assert.Equal("words", (await ui.App.Status())["product"]!.GetValue<string>());
            ILocator first = ui.Preview.GetByRole(AriaRole.Img, new() { Name = "Page 1", Exact = true });
            await Assertions.Expect(first).ToBeVisibleAsync();
            Assert.True(
                await first.EvaluateAsync<bool>("image => image.complete && image.naturalWidth > 0"),
                "The first page must be painted, not merely present.");

            ui.App.Open("second.csv");
            ui.App.Open("first.csv");
            await ui.Poll();
            await ui.WaitForPreview("first.csv", "workbook");
            await ui.ShowCellText("first", "FIRST_DOCUMENT");
        });

    [Fact]
    public Task CommittedSessionChanges_ReplaceTheVisibleDocumentAndPreserveAnUnchangedFrame() =>
        BrowserApp.Run("session-identity", output, async ui =>
        {
            await ui.ShowCellText("first", "FIRST_DOCUMENT");
            // Marked inside the viewer: a frame that reloaded would lose it.
            await ui.Preview.Locator(".av-app").EvaluateAsync("app => app.dataset.probe = 'retained'");
            await ui.Poll();
            await Assertions.Expect(ui.Preview.Locator(".av-app[data-probe='retained']")).ToHaveCountAsync(1);
            string firstUrl = (await ui.App.Status())["previewUrl"]!.GetValue<string>();
            ui.App.Open("second.csv");
            Assert.NotEqual(firstUrl, (await ui.App.Status())["previewUrl"]!.GetValue<string>());
            await ui.Poll();
            await ui.WaitForPreview("second.csv", "workbook");
            await ui.ShowCellText("second", "SECOND_DOCUMENT");

            ui.App.Open("first.csv");
            await ui.Poll();
            await ui.WaitForPreview("first.csv", "workbook");
            await ui.ShowCellText("first", "FIRST_DOCUMENT");

            foreach (string directory in new[] { "left", "right" })
            {
                Directory.CreateDirectory(ui.App.Workspace.File(directory));
                string file = Path.Combine(directory, "same.csv");
                File.WriteAllText(ui.App.Workspace.File(file), $"Marker,Value\n{directory}_DOCUMENT,1\n");
                ui.App.Open(file);
                await ui.Poll();
                await ui.WaitForPreview("same.csv", "workbook");
                await ui.ShowCellText("same", directory + "_DOCUMENT");
            }
        });

    [Fact]
    public Task SavedViewWithFailedRefresh_RetainsTheVisiblePreviewAndRetriesTheSameValue() =>
        BrowserApp.Run("refresh-retry", output, async ui =>
        {
            string originalUrl = (await ui.App.Status())["previewUrl"]!.GetValue<string>();
            await ui.Settings();
            await ui.Page.Locator("#default-view").SelectOptionAsync("sheets");
            using (var locked = new FileStream(ui.App.Workspace.File("first.csv"),
                FileMode.Open, FileAccess.Read, FileShare.None))
            {
                JsonNode saved = await ui.Save();
                Assert.True(saved["ok"]!.GetValue<bool>());
                Assert.Equal("PREVIEW_REFRESH_FAILED", saved["code"]!.GetValue<string>());
                Assert.Equal("sheets", (await ui.App.Status())["defaultView"]!.GetValue<string>());
                Assert.Equal(originalUrl, (await ui.App.Status())["previewUrl"]!.GetValue<string>());
                await ui.WaitForPreview("first.csv", "workbook");
                await Assertions.Expect(ui.Page.Locator("#toast")).ToContainTextAsync("Preferences were saved");
            }
            JsonNode retry = await ui.Save();
            Assert.True(retry["ok"]!.GetValue<bool>());
            Assert.Null(retry["code"]);
            await ui.WaitForPreview("first.csv", "sheets");
            Assert.NotEqual(originalUrl, (await ui.App.Status())["previewUrl"]!.GetValue<string>());
        });

    [Fact]
    public Task StatusCapturedBeforeSaving_CannotUndoTheCommittedPreview() =>
        BrowserApp.Run("stale-status", output, async ui =>
        {
            await ui.Settings();
            await ui.Page.Clock.PauseAtAsync(DateTime.UtcNow.AddSeconds(2));
            var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool first = true;
            await ui.Page.RouteAsync("**/api/status", async route =>
            {
                if (!first) { await route.ContinueAsync(); return; }
                first = false;
                IAPIResponse response = await route.FetchAsync();
                captured.SetResult();
                await release.Task;
                await route.FulfillAsync(new() { Response = response });
            });
            try
            {
                Task poll = ui.Poll();
                await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await ui.Page.Locator("#default-view").SelectOptionAsync("sheets");
                JsonNode saved = await ui.Save();
                Assert.True(saved["ok"]!.GetValue<bool>());
                release.SetResult();
                await poll;
                await ui.WaitForPreview("first.csv", "sheets");
            }
            finally { release.TrySetResult(); }
        });
}
