using System.Text.Json.Nodes;

namespace Aspose.Cli.Tests;

/// <summary>
/// Every result and operation schema the CLI serves is generated from the records that state
/// the contract, so no hand-written copy can drift from them: no schema file is checked in under
/// <c>src</c>, and the <c>schema</c> command serves exactly the ids <c>capabilities</c>
/// advertises, each under its own URI.
/// </summary>
public sealed class GeneratedSchemaSourceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void NoSchemaFile_IsCheckedInUnderSource()
    {
        string source = Path.Combine(RepositoryPaths.Root, "src");
        string[] files =
        [
            .. Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(source, path).Replace('\\', '/'))
                .Where(static path => !IsBuildOutput(path))
                .Where(static path => path.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase)
                    || path.Split('/').SkipLast(1).Contains("Schemas", StringComparer.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            files.Length == 0,
            "Schemas are generated from the result and operation records, never checked in; delete these files under src:"
            + Environment.NewLine + string.Join(Environment.NewLine, files));
    }

    [Fact]
    public void SchemaCommand_ServesEveryListedSchemaUnderItsOwnUri()
    {
        CliResult listed = _workspace.Run("schema", "--output-format", "json");
        Assert.True(listed.ExitCode == 0, listed.StdErr);
        string[] ids = [.. JsonNode.Parse(listed.StdOut)!["ids"]!.AsArray().Select(static id => id!.GetValue<string>())];
        IReadOnlyList<string> served = Aspose.Cli.TestKit.Scenarios.CliCatalog.Current.SchemaIds;

        Assert.NotEmpty(served);
        Assert.Equal(ids, Aspose.Cli.TestKit.Scenarios.CliCatalog.Current.Ids);
        foreach (string id in served)
        {
            Assert.Equal(
                PublishedSchemas.UriPrefix + id + PublishedSchemas.UriSuffix,
                PublishedSchemas.Document(id)["$id"]?.GetValue<string>());
        }
    }

    private static bool IsBuildOutput(string relativePath) =>
        relativePath.Split('/').Any(static segment => segment is "bin" or "obj");
}
