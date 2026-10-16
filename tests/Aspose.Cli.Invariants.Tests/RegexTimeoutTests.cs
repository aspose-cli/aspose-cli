using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// A regular expression that backtracks past its execution budget is one fact, so every product
/// reports it with the same code and exit code (C-R28).
/// </summary>
public sealed class RegexTimeoutTests
{
    private const string Pattern = "(a+)+$";

    // Nested repetition over forty letters and a mismatch backtracks far beyond the one-second budget.
    private static readonly string Text = new string('a', 40) + "!";

    /// <summary>The products' text edits that take a regular expression, each over a document holding <see cref="Text"/>.</summary>
    public static TheoryData<string> Products() => ["cells", "pdf", "slides", "words"];

    [Theory]
    [MemberData(nameof(Products))]
    public void CatastrophicBacktracking_IsOperationTimeout(string product)
    {
        if (product == "slides")
        {
            TestLicense.Require("Aspose.Slides evaluation mode reads text longer than five characters cut short, so the pattern never meets the long text.");
        }

        (string document, JsonObject files, JsonArray create, JsonObject op) = product switch
        {
            "cells" => ("text.csv", new JsonObject { ["text.csv"] = Source(Text + "\n") }, null!, ReplaceText()),
            "pdf" => ("text.pdf", new JsonObject { ["text.txt"] = Source(Text + "\n") },
                new JsonArray("pdf", "create", "text.pdf", "--from-text", "text.txt"),
                new JsonObject { ["op"] = "redact_text", ["find"] = Pattern, ["regex"] = true }),
            "slides" => ("text.pptx", new JsonObject { ["title.md"] = Source("# " + Text + "\n") },
                new JsonArray("slides", "create", "text.pptx", "--from-markdown", "title.md"), ReplaceText()),
            "words" => ("text.docx", new JsonObject { ["text.txt"] = Source(Text + "\n") },
                new JsonArray("words", "create", "text.docx", "--from-text", "text.txt"), ReplaceText()),
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, null),
        };
        string output = Path.GetFileNameWithoutExtension(document) + ".out" + Path.GetExtension(document);
        var steps = new JsonArray();
        if (create is not null)
        {
            steps.Add(new JsonObject { ["args"] = create, ["expect"] = new JsonObject { ["exitCode"] = 0 } });
        }

        steps.Add(new JsonObject
        {
            ["args"] = new JsonArray(product, "edit", document, "--ops", new JsonArray(op).ToJsonString()),
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

    private static JsonObject Source(string text) => new() { ["text"] = text };

    private static JsonObject ReplaceText() =>
        new() { ["op"] = "replace_text", ["find"] = Pattern, ["replace"] = "b", ["regex"] = true };
}
