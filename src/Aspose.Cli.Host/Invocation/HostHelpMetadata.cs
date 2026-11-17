using System.CommandLine;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Help examples and learning links for the commands owned by the executable host. Every
/// visible leaf command has at least two examples that run as written and a Learn more link.
/// </summary>
internal static class HostHelpMetadata
{
    private static readonly CommandHelpLink Overview = new("aspose-cli docs", "offline documentation bundled in this binary");
    private static readonly CommandHelpLink Licensing = new("aspose-cli docs licensing", "license sources, installation and evaluation mode");
    private static readonly CommandHelpLink Verification = new("aspose-cli docs verification", "the delivery checklist and font checks");
    private static readonly CommandHelpLink Troubleshooting = new("aspose-cli docs troubleshooting", "recovering from errors and environment problems");
    private static readonly CommandHelpLink Capabilities = new("aspose-cli capabilities --summary", "each product's commands, formats and operations");
    private static readonly CommandHelpLink Schemas = new("aspose-cli schema", "the available JSON Schema identifiers");

    public static void Attach(RootCommand root, ProductCatalog catalog)
    {
        string[] narrowCapabilities = NarrowCapabilities(root, catalog);
        root.WithExamples(
            [
                .. narrowCapabilities.TakeLast(1),
                "schema",
            ],
            [Overview, Schemas, Capabilities]);
        if (FindOptional(root, "license") is { } license)
        {
            license.WithExamples(
                [
                    "license status --output-format json",
                    "license install Aspose.Total.lic",
                ],
                [Licensing]);
            Find(license, "status").WithExamples(["license status", "license status --output-format json"], [Licensing]);
            Find(license, "install").WithExamples(["license install Aspose.Total.lic", "license install Aspose.Total.lic --dry-run"], [Licensing]);
            Find(license, "remove").WithExamples(["license remove --dry-run", "license remove"], [Licensing]);
        }

        Find(root, "doctor").WithExamples(["doctor", "doctor --output-format json"], [Troubleshooting]);
        if (FindOptional(root, "fonts") is { } fonts)
        {
            fonts.WithExamples(["fonts list --output-format json"], [Verification]);
            Find(fonts, "list").WithExamples(["fonts list", "fonts list --output-format json"], [Verification]);
            Find(fonts, "check").WithExamples(
                ["fonts check report.docx", "fonts check report.docx --font-dir fonts --output-format json"],
                [Verification]);
        }

        Find(root, "docs").WithExamples(["docs", "docs verification"], [Overview]);
        Find(root, "schema").WithExamples(["schema", "schema v2/common/error"], [Capabilities]);
        Find(root, "capabilities").WithExamples(
            [
                "capabilities --summary",
                .. narrowCapabilities,
            ],
            [Schemas, Overview]);
        Command skill = Find(root, "skill").WithExamples(
            [
                "skill list",
                "skill install aspose-cli-platform --host codex",
                "skill install aspose-cli-platform --host claude-code --user",
            ],
            [Overview]);
        Find(skill, "list").WithExamples(["skill list", "skill list --output-format json"], [Overview]);
        Find(skill, "install").WithExamples(
            ["skill install aspose-cli-platform --host codex", "skill install aspose-cli-platform --host claude-code --user --dry-run"],
            [Overview]);
        if (FindOptional(root, "mcp") is { } mcp)
        {
            Find(mcp, "serve").WithExamples(["mcp serve", "mcp serve --license-mode evaluation"], [Overview]);
        }
    }

    // Narrowing capabilities to one product, and to its edit command, keeps the answer small.
    private static string[] NarrowCapabilities(RootCommand root, ProductCatalog catalog)
    {
        if (catalog.Products.FirstOrDefault() is not { } product)
        {
            return [];
        }

        string id = product.Manifest.Id;
        string whole = $"capabilities {id} --output-format json";
        return FindOptional(Find(root, id), "edit") is null
            ? [whole]
            : [whole, $"capabilities {id} edit --output-format json"];
    }

    private static Command Find(Command parent, string name) =>
        parent.Subcommands.Single(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));

    private static Command? FindOptional(Command parent, string name) =>
        parent.Subcommands.SingleOrDefault(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));
}
