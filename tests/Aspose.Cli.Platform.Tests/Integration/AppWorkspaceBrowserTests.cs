using System.Text.Json.Nodes;
using Microsoft.Playwright;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// What a person can do with several documents at once: keep them open side
/// by side, look at one in another view, and pick the theme everything is
/// drawn in.
/// </summary>
[Collection("Local service lifecycle")]
public sealed class AppWorkspaceBrowserTests(ITestOutputHelper output)
{
    [Fact]
    public Task OpenDocuments_KeepTheirTabsAndComeBackToWhatTheyShowed() =>
        BrowserApp.Run("document-tabs", output, async ui =>
        {
            ui.App.Open("second.csv");
            await ui.Poll();
            await ui.WaitForPreview("second.csv", "workbook");
            ILocator tabs = ui.Page.Locator(".document-tab");
            await Assertions.Expect(tabs).ToHaveCountAsync(2);
            await Assertions.Expect(ui.Page.Locator(".document-tab.active .tab-name"))
                .ToHaveTextAsync("second.csv");

            await tabs.Filter(new() { HasText = "first.csv" }).ClickAsync();
            await ui.WaitForPreview("first.csv", "workbook");
            await ui.ShowCellText("first", "FIRST_DOCUMENT");
            // Both are still rendering; only which one is framed changed.
            await Assertions.Expect(tabs).ToHaveCountAsync(2);

            await ui.Page.Locator(".document-tab.active .tab-close").ClickAsync();
            await ui.WaitForPreview("second.csv", "workbook");
            await Assertions.Expect(tabs).ToHaveCountAsync(1);
        });

    [Fact]
    public Task ViewChoice_ShowsTheSameFileAnotherWayWithoutLosingItsTab() =>
        BrowserApp.Run("document-view", output, async ui =>
        {
            await ui.Page.Locator("#document-view").SelectOptionAsync("sheets");
            await ui.WaitForPreview("first.csv", "sheets");
            // One rendered image per sheet, labelled with the sheet's name.
            await Assertions.Expect(
                ui.Preview.GetByRole(AriaRole.Img, new() { Name = "first", Exact = true }).First)
                .ToBeVisibleAsync();
            JsonNode status = await ui.App.Status();
            JsonNode document = Assert.Single(status["documents"]!.AsArray())!;
            Assert.Equal("first.csv", document["fileName"]!.GetValue<string>());
            Assert.Equal("sheets", document["view"]!.GetValue<string>());
            Assert.True(document["active"]!.GetValue<bool>());

            await ui.Page.Locator("#document-view").SelectOptionAsync("workbook");
            await ui.WaitForPreview("first.csv", "workbook");
            await ui.ShowCellText("first", "FIRST_DOCUMENT");
        });

    [Fact]
    public Task ThemeChoice_ReachesTheDocumentTheAppFrames() =>
        BrowserApp.Run("theme", output, async ui =>
        {
            ILocator shell = ui.Page.Locator("html");
            ILocator framed = ui.Preview.Locator("html");
            await Assertions.Expect(shell).Not.ToHaveAttributeAsync("data-theme", "dark");

            await ui.Page.Locator("#theme-toggle").ClickAsync();

            await Assertions.Expect(shell).ToHaveAttributeAsync("data-theme", "dark");
            // Same origin, one stored choice: the viewer follows without being told.
            await Assertions.Expect(framed).ToHaveAttributeAsync("data-theme", "dark");
            await Assertions.Expect(ui.Preview.Locator(".av-app")).ToBeVisibleAsync();
        });
}
