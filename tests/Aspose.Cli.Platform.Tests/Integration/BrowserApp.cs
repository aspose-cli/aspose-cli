using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>Real Chromium/App fixture with failure evidence and no external request dependencies.</summary>
internal sealed class BrowserApp(AppTestSession app, IPage page)
{
    internal AppTestSession App => app;
    internal IPage Page => page;
    internal IFrameLocator Preview => page.FrameLocator("#preview-frame");

    internal static async Task Run(string name, ITestOutputHelper output, Func<BrowserApp, Task> test)
    {
        await using var app = await AppTestSession.Start();
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1280, Height = 900 },
        });
        await context.RouteAsync("**/*", route =>
            Uri.TryCreate(route.Request.Url, UriKind.Absolute, out Uri? url)
                && url.Host == "127.0.0.1" && url.Port == app.Url.Port
                ? route.ContinueAsync() : route.AbortAsync());
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        IPage page = await context.NewPageAsync();
        page.SetDefaultTimeout(12_000);
        await page.Clock.InstallAsync();
        var ui = new BrowserApp(app, page);
        try
        {
            await page.GotoAsync(new Uri(app.Client.BaseAddress!, "/preview").AbsoluteUri);
            await ui.WaitForPreview("first.csv", "workbook");
            await test(ui);
            await context.Tracing.StopAsync();
        }
        catch
        {
            string root = Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_ARTIFACTS")
                ?? Path.Combine(Path.GetTempPath(), "aspose-cli-browser-evidence");
            string evidence = Path.Combine(root, "browser", name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(evidence);
            output.WriteLine("Browser evidence: " + evidence);
            foreach (string log in Directory.EnumerateFiles(app.Workspace.ConfigDirectory, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)))
            {
                string target = Path.Combine(evidence, "logs", Path.GetRelativePath(app.Workspace.ConfigDirectory, log));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(log, target);
            }
            try
            {
                await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "page.png"), FullPage = true });
                await context.Tracing.StopAsync(new() { Path = Path.Combine(evidence, "trace.zip") });
                File.WriteAllText(Path.Combine(evidence, "status.json"), (await app.Status()).ToJsonString());
            }
            catch (Exception exception) { output.WriteLine("Evidence collection: " + exception.GetType().Name); }
            throw;
        }
    }

    internal async Task WaitForPreview(string file, string? view = null)
    {
        string url = (await app.Status())["previewUrl"]!.GetValue<string>();
        await page.WaitForFunctionAsync("""
            expected => {
              const frame = document.getElementById('preview-frame');
              const content = frame && frame.contentWindow;
              const meta = content && content.__asposePreview;
              return content && content.location.href === expected.url && meta
                && meta.file === expected.file && (!expected.view || meta.view === expected.view);
            }
            """, new { url, file, view });
    }

    internal async Task ShowCellText(string sheet, string text)
    {
        // Select the data sheet through its ordinary tab; evaluation sheets remain available.
        await Preview.Locator(".aspose-shell-tab").Filter(new() { HasTextRegex =
            new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(sheet) + "$") }).ClickAsync();
        await Preview.GetByText(text, new() { Exact = true }).WaitForAsync();
    }

    internal async Task Poll()
    {
        Task<IResponse> response = page.WaitForResponseAsync(
            response => response.Url.EndsWith("/api/status", StringComparison.Ordinal));
        await page.Clock.FastForwardAsync(16_000);
        await (await response).FinishedAsync();
    }

    internal async Task Settings()
    {
        Task<IResponse> response = page.WaitForResponseAsync(
            response => response.Url.EndsWith("/api/status", StringComparison.Ordinal));
        await page.Locator("a[data-route='/settings']").ClickAsync();
        await (await response).FinishedAsync();
        await page.Locator("#default-view").WaitForAsync();
    }

    internal async Task<System.Text.Json.Nodes.JsonNode> Save()
    {
        Task<IResponse> response = page.WaitForResponseAsync(
            response => response.Url.EndsWith("/api/preferences", StringComparison.Ordinal)
                && response.Request.Method == "POST");
        await page.Locator("#save-preferences").ClickAsync();
        IResponse saved = await response;
        return System.Text.Json.Nodes.JsonNode.Parse(await saved.TextAsync())!;
    }
}
