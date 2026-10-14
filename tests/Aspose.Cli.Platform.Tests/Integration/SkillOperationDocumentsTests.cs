using System.Text.Json;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// An operation document a Skill passes on standard input, <c>--ops -</c> with a shell heredoc or
/// a piped PowerShell here-string, is read like an inline one, so the schema judges it.
/// </summary>
public sealed class SkillOperationDocumentsTests
{
    private const string MissingValues = """{"ops":[{"op":"set_values","sheet":"Sheet1","range":"A1"}]}""";

    private const string UnknownOperation = """{"ops":[{"op":"set_valuez","sheet":"Sheet1"}]}""";

    [Fact]
    public void StandardInputDocuments_AreReadWithTheirProduct()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(directory.File("SKILL.md"), $$"""
            # Example

            ```bash
            aspose-cli cells edit book.xlsx --ops - --out edited.xlsx <<'EOF'
            {{MissingValues}}
            EOF
            ```

            ```powershell
            @'
            {{UnknownOperation}}
            '@ | aspose-cli cells edit book.xlsx --ops - --out edited.xlsx
            ```

            Pass `--ops -` to read the document from standard input.
            """);

        (IReadOnlyList<SkillOperationDocument> documents, IReadOnlyList<string> problems) = SkillOperationDocuments.Collect(directory.Path);

        Assert.Empty(problems);
        Assert.Equal([MissingValues, UnknownOperation], documents.Select(static document => document.Json.Trim()));
        Assert.Equal([5, 11], documents.Select(static document => document.Line));
        Assert.All(documents, static document => Assert.Equal("cells", document.Command));
        Json.Schema.JsonSchema schema = PublishedSchemas.Schema("v2/cells/ops");
        Assert.All(documents, document =>
        {
            using JsonDocument instance = JsonDocument.Parse(document.Json);
            Assert.False(schema.Evaluate(instance.RootElement).IsValid, document.Json);
        });
    }

    [Fact]
    public void UnterminatedHeredoc_IsAProblem()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(directory.File("SKILL.md"), """
            ```bash
            aspose-cli cells edit book.xlsx --ops - <<EOF
            {"ops":[]}
            ```
            """);

        (IReadOnlyList<SkillOperationDocument> documents, IReadOnlyList<string> problems) = SkillOperationDocuments.Collect(directory.Path);

        Assert.Empty(documents);
        Assert.Contains("heredoc is not terminated", Assert.Single(problems), StringComparison.Ordinal);
    }
}
