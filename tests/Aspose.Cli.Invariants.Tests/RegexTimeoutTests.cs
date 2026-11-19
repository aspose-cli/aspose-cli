using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// A regular expression that backtracks past its execution budget is one fact, so every product
/// reports it with the same code and exit code (C-R28), from an edit or a search alike.
/// </summary>
public sealed class RegexTimeoutTests
{
    private const string Pattern = "(a+)+$";

    // Nested repetition over forty letters and a mismatch backtracks far beyond the one-second budget.
    private static readonly string Text = new string('a', 40) + "!";

    /// <summary>
    /// Every product of the catalog, each with its command that takes a regular expression over a
    /// document holding <see cref="Text"/>, so a new product fails until its command is placed here.
    /// </summary>
    public static TheoryData<string> Products() => [.. CliCatalog.Current.Products.Select(static product => product.Id)];

    [Theory]
    [MemberData(nameof(Products))]
    public void CatastrophicBacktracking_IsOperationTimeout(string product)
    {
        if (product == "slides")
        {
            TestLicense.Require("Aspose.Slides evaluation mode reads text longer than five characters cut short, so the pattern never meets the long text.");
        }

        (string document, JsonObject files, JsonArray[] setup, JsonArray run) = product switch
        {
            // Cells takes a regular expression in its search; its edit's replace action is the
            // engine's own. The text is the first cell of the engine's new workbook.
            "cells" => ("text.xlsx", new JsonObject(),
                new[] { new JsonArray("cells", "create", "text.xlsx"), new JsonArray("cells", "edit", "text.xlsx", "--in-place", "--ops", Cell()) },
                new JsonArray("cells", "search", "text.xlsx", "--regex", "--pattern", Pattern)),
            "pdf" => ("text.pdf", new JsonObject { ["text.txt"] = Source(Text + "\n") },
                new[] { new JsonArray("pdf", "create", "text.pdf", "--from-text", "text.txt") },
                Edit("pdf", "text.pdf", new JsonObject { ["op"] = "redact_text", ["find"] = Pattern, ["regex"] = true })),
            // Slides takes a regular expression in its search; its edit's replace_text matches text as
            // it is. The text is a shape's on the one slide of the engine's new presentation, slide 256.
            "slides" => ("text.pptx", new JsonObject(),
                new[] { new JsonArray("slides", "create", "text.pptx"), new JsonArray("slides", "edit", "text.pptx", "--in-place", "--ops", TextBox()) },
                new JsonArray("slides", "search", "text.pptx", "--regex", "--pattern", Pattern)),
            "words" => ("text.docx", new JsonObject { ["text.txt"] = Source(Text + "\n") },
                new[] { new JsonArray("words", "create", "text.docx", "--from-text", "text.txt") }, Edit("words", "text.docx", ReplaceText())),
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, null),
        };
        string output = Path.GetFileNameWithoutExtension(document) + ".out" + Path.GetExtension(document);
        var steps = new JsonArray();
        foreach (JsonArray args in setup)
        {
            steps.Add(new JsonObject { ["args"] = args, ["expect"] = new JsonObject { ["exitCode"] = 0 } });
        }

        steps.Add(new JsonObject
        {
            ["args"] = run,
            ["expect"] = new JsonObject
            {
                ["exitCode"] = 9,
                ["error"] = "OPERATION_TIMEOUT",
                ["json"] = new JsonArray(new JsonObject { ["path"] = "error.details.phase", ["equals"] = "regex" }),
                ["files"] = new JsonObject { ["absent"] = new JsonArray(output) },
            },
        });
        var scenario = new JsonObject
        {
            ["name"] = $"a {product} regular expression past its budget is OPERATION_TIMEOUT",
            ["files"] = files,
            ["steps"] = steps,
        };

        ScenarioRunner.Run(Scenario.Parse(scenario.ToJsonString())).AssertPassed();
    }

    private static JsonArray Edit(string product, string document, JsonObject op) =>
        new(product, "edit", document, "--ops", new JsonArray(op).ToJsonString());

    private static string Cell() =>
        new JsonObject
        {
            ["ops"] = new JsonArray(new JsonObject { ["set"] = "sheet/Sheet1/cell/A1", ["props"] = new JsonObject { ["value"] = Text } }),
        }.ToJsonString();

    private static string TextBox() =>
        new JsonObject
        {
            ["ops"] = new JsonArray(new JsonObject
            {
                ["add"] = "autoShape",
                ["in"] = "slide/256",
                ["args"] = new JsonObject { ["shapeType"] = "Rectangle", ["x"] = 36, ["y"] = 36, ["width"] = 600, ["height"] = 80 },
                ["props"] = new JsonObject { ["text"] = Text },
            }),
        }.ToJsonString();

    private static JsonObject Source(string text) => new() { ["text"] = text };

    private static JsonObject ReplaceText(string pattern = Pattern) =>
        new() { ["op"] = "replace_text", ["find"] = pattern, ["replace"] = "b", ["regex"] = true };
}
