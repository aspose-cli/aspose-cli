using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Aspose.Words.Notes;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// <c>words inspect</c> names the story that holds each field and image in <c>scope</c>. The real
/// engine always reports it, in every story, so the <c>document-info</c> schema requires it and
/// refuses null, as it always has; a record that types it as optional must not loosen the schema.
/// </summary>
public sealed class WordsStoryScopeContractTests : IDisposable
{
    private const string Id = "v2/words/document-info";
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [LicensedFact]
    public void Inspect_FieldAndImageScope_IsAlwaysReportedAndRequiredByTheSchema()
    {
        using var fixture = new WordsFixture();
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Body ");
        builder.InsertField("MERGEFIELD Name");
        builder.InsertImage(png);
        Footnote footnote = builder.InsertFootnote(FootnoteType.Footnote, "Note ");
        builder.MoveTo(footnote.FirstParagraph);
        builder.InsertField("MERGEFIELD Note");
        var comment = new Comment(document, "Reviewer", "R", DateTime.UnixEpoch);
        document.FirstSection.Body.FirstParagraph.AppendChild(comment);
        comment.SetText("Comment ");
        builder.MoveTo(comment.FirstParagraph);
        builder.InsertField("MERGEFIELD Remark");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.InsertField("PAGE");
        builder.InsertImage(png);
        document.Save(_workspace.File("stories.docx"), SaveFormat.Docx);

        // The published schemas load while the document is inspected.
        CliResult inspected = null!;
        Parallel.Invoke(
            () => _ = PublishedSchemas.Ids,
            () => inspected = _workspace.Run(
                "words", "inspect", "stories.docx", "--detail", "fields", "--detail", "images",
                "--license", TestLicense.Path!, "--output", "json"));

        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        JsonObject result = JsonNode.Parse(inspected.StdOut)!.AsObject();
        Assert.Equal(Id, PublishedSchemas.IdOf(result["schema"]!.GetValue<string>()));
        var problems = new List<string>();
        CheckScopes(result, "fields", ["body", "comments", "footnotes", "headersFooters"], problems);
        CheckScopes(result, "images", ["body", "headersFooters"], problems);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static void CheckScopes(JsonObject result, string list, string[] stories, List<string> problems)
    {
        JsonArray items = result[list]!.AsArray();
        string?[] scopes = [.. items.Select(static item => item!["scope"]?.GetValue<string>())];
        if (scopes.Any(static scope => scope is null))
        {
            problems.Add($"{list}: an item has no scope: {items.ToJsonString()}");
        }

        string[] missing = [.. stories.Except(scopes.OfType<string>(), StringComparer.Ordinal)];
        if (missing.Length > 0)
        {
            problems.Add($"{list}: no item reports the stories {string.Join(", ", missing)}: {items.ToJsonString()}");
        }

        for (int index = 0; index < items.Count; index++)
        {
            if (IsValid(result, list, index, scope: null, remove: true))
            {
                problems.Add($"{Id} accepts {list}[{index}] without the scope every real item reports");
            }

            if (IsValid(result, list, index, scope: null, remove: false))
            {
                problems.Add($"{Id} accepts a null {list}[{index}].scope, which the real engine never reports");
            }
        }
    }

    private static bool IsValid(JsonObject result, string list, int index, JsonNode? scope, bool remove)
    {
        var copy = result.DeepClone().AsObject();
        JsonObject item = copy[list]![index]!.AsObject();
        if (remove)
        {
            item.Remove("scope");
        }
        else
        {
            item["scope"] = scope;
        }

        using JsonDocument instance = JsonDocument.Parse(copy.ToJsonString());
        return PublishedSchemas.Schema(Id).Evaluate(instance.RootElement).IsValid;
    }
}
