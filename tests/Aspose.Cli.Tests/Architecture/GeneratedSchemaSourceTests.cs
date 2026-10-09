using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Tests;

/// <summary>
/// Every result and operation schema the CLI serves is generated from the records that state
/// the contract, so no hand-written copy can drift from them: no schema file is checked in under
/// <c>src</c>, no product keeps a schema id table or hand-written result samples, and the
/// <c>schema</c> command still serves every id it served while the schemas were hand-written.
/// </summary>
public sealed partial class GeneratedSchemaSourceTests : IDisposable
{
    /// <summary>
    /// The ids <c>aspose-cli schema</c> served before the schemas were generated, pinned so that
    /// generating them drops none.
    /// </summary>
    private static readonly string[] ServedBeforeGeneration =
    [
        "v2/cells/convert-result",
        "v2/cells/create-result",
        "v2/cells/diff-result",
        "v2/cells/edit-result",
        "v2/cells/ops",
        "v2/cells/render-result",
        "v2/cells/search-result",
        "v2/cells/workbook-info",
        "v2/cells/workbook-read",
        "v2/common/app-result",
        "v2/common/app-status",
        "v2/common/backup",
        "v2/common/capabilities",
        "v2/common/capabilities-summary",
        "v2/common/diagnostic-details",
        "v2/common/doctor",
        "v2/common/error",
        "v2/common/file-fingerprint",
        "v2/common/font-check",
        "v2/common/font-list",
        "v2/common/license-status",
        "v2/common/not-found-details",
        "v2/common/operation-outcome",
        "v2/common/preview-session",
        "v2/common/preview-status",
        "v2/common/result-window",
        "v2/common/review",
        "v2/common/schema-list",
        "v2/common/skill-install",
        "v2/common/skill-list",
        "v2/common/update-result",
        "v2/common/verification-issue",
        "v2/common/version",
        "v2/common/view",
        "v2/pdf/convert-result",
        "v2/pdf/edit-result",
        "v2/pdf/extract-result",
        "v2/pdf/form-export-result",
        "v2/pdf/form-result",
        "v2/pdf/ops",
        "v2/pdf/pdf-info",
        "v2/pdf/pdf-read",
        "v2/pdf/render-result",
        "v2/pdf/search-result",
        "v2/pdf/sign-result",
        "v2/pdf/split-result",
        "v2/pdf/validate-result",
        "v2/pdf/write-result",
        "v2/slides/convert-result",
        "v2/slides/create-result",
        "v2/slides/edit-result",
        "v2/slides/extract-result",
        "v2/slides/ops",
        "v2/slides/presentation-info",
        "v2/slides/presentation-read",
        "v2/slides/render-result",
        "v2/slides/search-result",
        "v2/words/compare-result",
        "v2/words/convert-result",
        "v2/words/create-result",
        "v2/words/document-info",
        "v2/words/document-read",
        "v2/words/edit-result",
        "v2/words/extract-result",
        "v2/words/ops",
        "v2/words/render-result",
        "v2/words/search-result",
        "v2/words/split-result",
    ];

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
    public void NoProduct_DeclaresSchemaIdsOrContractSamples()
    {
        string[] forbidden =
        [
            .. CompiledProductCatalog.Instance.Products
                .Select(static product => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(product.Manifest.Id))
                .SelectMany(static product => new[] { product + "SchemaIds", product + "ContractSamples" }),
        ];
        Assert.Contains("PdfSchemaIds", forbidden);
        var declarations = new List<string>();
        foreach (string directory in new[] { "src", "tests" })
        {
            string root = Path.Combine(RepositoryPaths.Root, directory);
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(RepositoryPaths.Root, file).Replace('\\', '/');
                if (IsBuildOutput(relative))
                {
                    continue;
                }

                foreach (Match match in TypeDeclaration().Matches(File.ReadAllText(file)))
                {
                    if (forbidden.Contains(match.Groups["name"].Value, StringComparer.Ordinal))
                    {
                        declarations.Add($"{relative}: {match.Groups["name"].Value}");
                    }
                }
            }
        }

        Assert.True(
            declarations.Count == 0,
            "A result record states its own schema id and the generated schema replaces hand-written samples; delete:"
            + Environment.NewLine + string.Join(Environment.NewLine, declarations.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void SchemaCommand_ServesEveryIdItServedBeforeGeneration()
    {
        CliResult listed = _workspace.Run("schema", "--output", "json");
        Assert.True(listed.ExitCode == 0, listed.StdErr);
        string[] served = [.. JsonNode.Parse(listed.StdOut)!["schemas"]!.AsArray().Select(static id => id!.GetValue<string>())];
        CliResult capabilities = _workspace.Run("capabilities", "--output", "json");
        Assert.True(capabilities.ExitCode == 0, capabilities.StdErr);
        string[] advertised = [.. JsonNode.Parse(capabilities.StdOut)!["schemas"]!.AsArray().Select(static id => id!.GetValue<string>())];

        Assert.Empty(ServedBeforeGeneration.Except(served, StringComparer.Ordinal));
        Assert.Empty(ServedBeforeGeneration.Except(advertised, StringComparer.Ordinal));
        foreach (string id in ServedBeforeGeneration)
        {
            Assert.Equal(
                PublishedSchemas.UriPrefix + id + PublishedSchemas.UriSuffix,
                PublishedSchemas.Document(id)["$id"]?.GetValue<string>());
        }
    }

    private static bool IsBuildOutput(string relativePath) =>
        relativePath.Split('/').Any(static segment => segment is "bin" or "obj");

    [GeneratedRegex(@"\b(?:class|record|struct|interface|enum)\s+(?<name>\w+)")]
    private static partial Regex TypeDeclaration();
}
