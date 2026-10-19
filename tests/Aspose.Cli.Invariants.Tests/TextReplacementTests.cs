using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// replace_text takes the same fields in every product that has it, so one operation on one text
/// gives the same text back and the same itemsAffected in each: literal and regular-expression
/// matching, letter case, whole words, substitutions and the replacement limit.
/// </summary>
public sealed class TextReplacementTests
{
    // The replacements write this marker, so a search for it reads the edited text back.
    private const string Marker = "QQ";

    private static readonly Dictionary<string, Case> Cases = new()
    {
        ["literal-ignores-case"] = new("category cat cat_x Cat", Op("cat"), "QQegory QQ QQ_x QQ"),
        ["literal-case-sensitive"] = new("category cat cat_x Cat", Op("Cat", caseSensitive: true), "category cat cat_x QQ"),
        ["whole-word-literal"] = new("category cat cat_x Cat", Op("cat", wholeWord: true), "category QQ cat_x QQ"),
        ["whole-word-no-match"] = new("category", Op("cat", wholeWord: true), null),
        // Only a letter, decimal digit or underscore joins a word: not a superscript digit or a Roman numeral.
        ["whole-word-number-signs"] = new("x² xⅫ x1 x_ x", Op("x", wholeWord: true), "QQ² QQⅫ x1 x_ QQ"),
        // A whole-word expression keeps the matches that are whole words; it does not retry an alternative.
        ["whole-word-regex-alternation"] = new("category", Op("cat|category", regex: true, wholeWord: true), null),
        ["regex-substitution"] = new("pages 10-20 and 3-4", Op(@"(\d+)-(\d+)", Marker + "$2-$1", regex: true), "pages QQ20-10 and QQ4-3"),
        ["literal-dollar-verbatim"] = new("the price is", Op("price", Marker + "$1"), "the QQ$1 is"),
        ["maximum-count"] = new("z1 z2 z3", Op("z", maximum: 2), "QQ1 QQ2 z3"),
    };

    /// <summary>Every product with replace_text, each with every case.</summary>
    public static TheoryData<string, string> ProductCases()
    {
        var data = new TheoryData<string, string>();
        foreach (string product in ReplacingProducts())
        {
            foreach (string name in Cases.Keys)
            {
                data.Add(product, name);
            }
        }

        return data;
    }

    /// <summary>The products whose replace_text refuses an expression that matches an empty string, as search does: every product with replace_text.</summary>
    public static TheoryData<string> EmptyMatchProducts() => [.. ReplacingProducts()];

    /// <summary>
    /// The products whose operations the catalog lists replace_text in, so a new product with it
    /// fails until its document is placed in <see cref="Document"/>.
    /// </summary>
    private static IEnumerable<string> ReplacingProducts() =>
        CliCatalog.Current.Products.Where(static product => product.Ops.Contains("replace_text")).Select(static product => product.Id);

    [Theory]
    [MemberData(nameof(ProductCases))]
    public void ReplaceText_GivesTheSameTextAndCountInEveryProduct(string product, string name)
    {
        Case replacement = Cases[name];
        RequireFullText(product);
        (string document, JsonObject files, JsonArray? create) = Document(product, replacement.Text);
        string output = "out" + Path.GetExtension(document);
        var steps = new JsonArray();
        if (create is not null)
        {
            steps.Add(new JsonObject { ["args"] = create, ["expect"] = new JsonObject { ["exitCode"] = 0 } });
        }

        var edit = new JsonObject
        {
            ["exitCode"] = 0,
            ["json"] = new JsonArray(new JsonObject
            {
                ["path"] = "applied[0].itemsAffected",
                ["equals"] = replacement.Expected is null ? 0 : 1,
            }),
        };
        if (replacement.Expected is null)
        {
            edit["warnings"] = new JsonObject { ["present"] = new JsonArray("OP_NO_EFFECT") };
        }

        steps.Add(new JsonObject
        {
            ["args"] = new JsonArray(product, "edit", document, "--ops", new JsonArray(replacement.Op.DeepClone()).ToJsonString(), "--out", output),
            ["expect"] = edit,
        });
        if (replacement.Expected is { } expected)
        {
            steps.Add(new JsonObject
            {
                ["args"] = new JsonArray(product, "search", output, "--pattern", Marker, "--case-sensitive"),
                ["expect"] = new JsonObject
                {
                    ["exitCode"] = 0,
                    ["json"] = new JsonArray(new JsonObject { ["path"] = "hits[0].context", ["equals"] = expected }),
                },
            });
        }

        Run($"{product} replace_text {name}", files, steps);
    }

    [Theory]
    [MemberData(nameof(EmptyMatchProducts))]
    public void ReplaceText_RefusesAnExpressionThatMatchesAnEmptyString(string product)
    {
        RequireFullText(product);
        (string document, JsonObject files, JsonArray? create) = Document(product, "ab");
        var steps = new JsonArray();
        if (create is not null)
        {
            steps.Add(new JsonObject { ["args"] = create, ["expect"] = new JsonObject { ["exitCode"] = 0 } });
        }

        steps.Add(new JsonObject
        {
            ["args"] = new JsonArray(product, "edit", document, "--ops", new JsonArray(Op("x*", regex: true)).ToJsonString(), "--out", "out" + Path.GetExtension(document)),
            ["expect"] = new JsonObject { ["exitCode"] = 4, ["error"] = "OPS_INVALID" },
        });
        Run($"{product} replace_text refuses an empty match", files, steps);
    }

    private static void RequireFullText(string product)
    {
        if (product == "slides")
        {
            TestLicense.Require("Aspose.Slides evaluation mode reads text longer than five characters cut short.");
        }
    }

    private static (string Document, JsonObject Files, JsonArray? Create) Document(string product, string text) => product switch
    {
        "cells" => ("text.csv", new JsonObject { ["text.csv"] = Source(text + "\n") }, null),
        "slides" => ("text.pptx", new JsonObject { ["title.md"] = Source("# " + text + "\n") },
            new JsonArray("slides", "create", "text.pptx", "--from-markdown", "title.md")),
        "words" => ("text.docx", new JsonObject { ["text.txt"] = Source(text + "\n") },
            new JsonArray("words", "create", "text.docx", "--from-text", "text.txt")),
        _ => throw new ArgumentOutOfRangeException(nameof(product), product, null),
    };

    private static void Run(string name, JsonObject files, JsonArray steps) =>
        ScenarioRunner.Run(Scenario.Parse(new JsonObject
        {
            ["name"] = name,
            ["files"] = files,
            ["steps"] = steps,
        }.ToJsonString())).AssertPassed();

    private static JsonObject Source(string text) => new() { ["text"] = text };

    private static JsonObject Op(
        string find,
        string replace = Marker,
        bool regex = false,
        bool caseSensitive = false,
        bool wholeWord = false,
        int? maximum = null)
    {
        var op = new JsonObject { ["op"] = "replace_text", ["find"] = find, ["replace"] = replace };
        if (regex)
        {
            op["regex"] = true;
        }

        if (caseSensitive)
        {
            op["caseSensitive"] = true;
        }

        if (wholeWord)
        {
            op["wholeWord"] = true;
        }

        if (maximum is { } count)
        {
            op["maxReplacementCount"] = count;
        }

        return op;
    }

    private sealed record Case(string Text, JsonObject Op, string? Expected);
}
