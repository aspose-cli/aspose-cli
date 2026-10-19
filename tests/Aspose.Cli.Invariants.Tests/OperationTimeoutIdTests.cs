using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// An operation that runs out of time is named like any other operation that fails: the error's
/// details carry its <c>id</c> and <c>op</c>, so a caller knows which operation of the batch to
/// narrow, while the code, exit code and phase stay those of the timeout (WRD-4).
/// </summary>
public sealed class OperationTimeoutIdTests
{
    private const string Pattern = "(a+)+$";

    // Words refuses a replace_text anchor, so its expression fails at a letter instead.
    private const string UnanchoredPattern = "(a+)+b";

    // Nested repetition over forty letters and a mismatch backtracks far beyond the one-second budget.
    private static readonly string Text = new string('a', 40) + "!";

    /// <summary>Every product of the catalog, so a new product fails until its text edit is placed here.</summary>
    public static TheoryData<string> Products() => [.. CliCatalog.Current.Products.Select(static product => product.Id)];

    [Theory]
    [MemberData(nameof(Products))]
    public void ARegexTimeout_NamesTheOperationThatRanOutOfTime(string product)
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
                new JsonArray("words", "create", "text.docx", "--from-text", "text.txt"), ReplaceText(UnanchoredPattern)),
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, null),
        };
        string name = op["op"]!.GetValue<string>();
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
                ["json"] = new JsonArray(
                    new JsonObject { ["path"] = "error.details.id", ["equals"] = "op-0001" },
                    new JsonObject { ["path"] = "error.details.op", ["equals"] = name },
                    new JsonObject { ["path"] = "error.details.phase", ["equals"] = "regex" }),
            },
        });
        var scenario = new JsonObject
        {
            ["name"] = $"a {product} regular expression past its budget names its operation",
            ["files"] = files,
            ["steps"] = steps,
        };

        ScenarioRunner.Run(Scenario.Parse(scenario.ToJsonString())).AssertPassed();
    }

    private static JsonObject Source(string text) => new() { ["text"] = text };

    private static JsonObject ReplaceText(string pattern = Pattern) =>
        new() { ["op"] = "replace_text", ["find"] = pattern, ["replace"] = "b", ["regex"] = true };
}
