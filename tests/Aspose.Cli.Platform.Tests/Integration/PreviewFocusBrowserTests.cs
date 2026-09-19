using System.Diagnostics;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The change spotlight is the preview's headline experience: after a real
/// in-place edit, the browser must emphasize exactly what the edit touched.
/// </summary>
[Collection("Local service lifecycle")]
public sealed class PreviewFocusBrowserTests(ITestOutputHelper output)
{
    [Fact]
    public Task CellsEdit_SpotlightsExactlyTheEditedCell() =>
        RunPreview("cells-focus", "book.xlsx", workspace =>
        {
            Succeed(workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data", "--output", "json"));
            var arguments = new List<string> { "cells", "edit", "book.xlsx", "--in-place" };
            for (int row = 1; row <= 6; row++)
            {
                for (char column = 'A'; column <= 'E'; column++)
                {
                    arguments.Add("--set");
                    arguments.Add($"Data!{column}{row}=R{row}{column}");
                }
            }
            arguments.AddRange(["--output", "json"]);
            Succeed(workspace.Run([.. arguments]));
        },
        async (workspace, browser) =>
        {
            // Evaluation saves add warning sheets that may be active; the
            // spotlight itself must bring the edited sheet into view.
            await browser.Locator("[sheetname='Data'] td[data-cell='C3']").WaitForAsync(new()
            {
                State = WaitForSelectorState.Attached,
            });
            Succeed(workspace.Run("cells", "edit", "book.xlsx", "--in-place",
                "--set", "Data!C3=EDITED_VALUE", "--output", "json"));

            await browser.WaitForFunctionAsync("""
                () => {
                  const cell = document.querySelector("[sheetname='Data'] td[data-cell='C3']");
                  const box = document.querySelector("[data-aspose-focus='range']");
                  if (!cell || !box || cell.textContent.trim() !== 'EDITED_VALUE') return false;
                  const expected = cell.getBoundingClientRect();
                  const actual = box.getBoundingClientRect();
                  return ['left', 'top', 'right', 'bottom']
                    .every(side => Math.abs(expected[side] - actual[side]) <= 3);
                }
                """);
            Assert.Equal(0, await browser.Locator("[data-aspose-focus='sheet']").CountAsync());
        });

    [Fact]
    public Task WordsEdit_EmphasizesTheEditedPage() =>
        RunPreview("words-focus", "doc.docx", workspace =>
        {
            File.WriteAllText(workspace.File("doc.md"), string.Join("\n\n",
                Enumerable.Range(1, 150).Select(static index => $"Paragraph {index} of the preview document.")));
            Succeed(workspace.Run("words", "create", "doc.docx", "--markdown", "doc.md", "--output", "json"));
        },
        async (workspace, browser) =>
        {
            await browser.Locator(".awpage").Nth(1).WaitForAsync();
            CliResult edited = Succeed(workspace.RunWithInput(
                """{"ops":[{"op":"set_text","at":{"block":140},"text":"EDITED_PARAGRAPH"}]}""",
                "words", "edit", "doc.docx", "--ops", "-", "--in-place", "--output", "json"));
            int pageNumber = JsonNode.Parse(edited.StdOut)!["pagesTouched"]!.AsArray()[0]!.GetValue<int>();
            Assert.True(pageNumber > 1, "The edited block must lie beyond the first page.");

            await browser.WaitForFunctionAsync("""
                expected => {
                  const pages = Array.from(document.querySelectorAll('.awpage'));
                  const emphasized = pages.filter(page => page.classList.contains('words-page-updated'));
                  return emphasized.length === 1 && pages.indexOf(emphasized[0]) + 1 === expected;
                }
                """, pageNumber);
        });

    private async Task RunPreview(
        string name,
        string file,
        Action<TempWorkspace> arrange,
        Func<TempWorkspace, IPage, Task> test)
    {
        using var workspace = new TempWorkspace();
        arrange(workspace);
        JsonNode session = JsonNode.Parse(
            Succeed(workspace.Run("preview", file, "--output", "json")).StdOut)!;
        var url = new Uri(session["url"]!.GetValue<string>());
        using Process process = Process.GetProcessById(session["pid"]!.GetValue<int>());
        _ = process.Handle;
        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            await using IBrowserContext context = await browser.NewContextAsync(new()
            {
                ViewportSize = new() { Width = 1280, Height = 900 },
            });
            await context.RouteAsync("**/*", route =>
                Uri.TryCreate(route.Request.Url, UriKind.Absolute, out Uri? target)
                    && target.Host == url.Host && target.Port == url.Port
                    ? route.ContinueAsync() : route.AbortAsync());
            IPage page = await context.NewPageAsync();
            page.SetDefaultTimeout(30_000);
            await page.GotoAsync(url.AbsoluteUri);
            try
            {
                await test(workspace, page);
            }
            catch
            {
                await CaptureEvidence(name, page);
                throw;
            }
        }
        finally
        {
            workspace.Run("preview", "stop", "--all", "--output", "json");
            if (!process.WaitForExit(10_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
            }
        }
    }

    private async Task CaptureEvidence(string name, IPage page)
    {
        string root = Environment.GetEnvironmentVariable("ASPOSE_CLI_TEST_ARTIFACTS")
            ?? Path.Combine(Path.GetTempPath(), "aspose-cli-browser-evidence");
        string evidence = Path.Combine(root, "browser", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        output.WriteLine("Browser evidence: " + evidence);
        try
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "page.png"), FullPage = true });
            File.WriteAllText(Path.Combine(evidence, "page.html"), await page.ContentAsync());
        }
        catch (Exception exception) { output.WriteLine("Evidence collection: " + exception.GetType().Name); }
    }

    private static CliResult Succeed(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return result;
    }
}
