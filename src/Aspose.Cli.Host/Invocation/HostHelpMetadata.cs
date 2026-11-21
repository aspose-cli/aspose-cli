using System.CommandLine;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Help examples and learning links for the commands owned by the executable host. Every
/// visible leaf command has at least two examples that run as written and a Learn more link.
/// </summary>
internal static class HostHelpMetadata
{
    private static readonly CommandHelpLink Catalog = new($"{DistributionInfo.CommandName} schema", "the products, commands, formats and document ids");
    private static readonly CommandHelpLink Skills = new($"{DistributionInfo.CommandName} skill list", "the Agent Skills bundled with this build");

    public static void Attach(RootCommand root, ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        root.WithExamples(["schema", "license status"], [Catalog, Skills]);
        if (FindOptional(root, "license") is { } license)
        {
            CommandHelpLink status = Result("license-status", "the fields of the license status");
            license.WithExamples(
                [
                    "license status --output-format json",
                    "license install Aspose.Total.lic",
                ],
                [status]);
            Find(license, "status").WithExamples(["license status", "license status --output-format json"], [status]);
            Find(license, "install").WithExamples(["license install Aspose.Total.lic", "license install Aspose.Total.lic --product cells"], [status]);
            Find(license, "remove").WithExamples(["license remove", "license remove --product pdf"], [status]);
        }

        if (FindOptional(root, "fonts") is { } fonts)
        {
            CommandHelpLink list = Result("font-list", "the fields of the font list");
            fonts.WithExamples(["fonts list --output-format json"], [list]);
            Find(fonts, "list").WithExamples(["fonts list", "fonts list --output-format json"], [list]);
        }

        Find(root, "schema").WithExamples(
            ["schema", $"schema {ResultEnvelope.CatalogId("common", "diagnostics")}"],
            [Result("schema-catalog", "the fields of the catalog"), Skills]);
        Command skill = Find(root, "skill").WithExamples(
            [
                "skill list",
                "skill install aspose-cli-platform --host codex",
                "skill install aspose-cli-platform --host claude-code --user",
            ],
            [Skills]);
        Find(skill, "list").WithExamples(["skill list", "skill list --output-format json"], [Result("skill-list", "the fields of the skill list")]);
        Find(skill, "install").WithExamples(
            ["skill install aspose-cli-platform --host codex", "skill install aspose-cli-platform --host claude-code --user"],
            [Skills]);
    }

    /// <summary>A link to the JSON Schema of a result the host publishes, such as <c>license-status</c>.</summary>
    private static CommandHelpLink Result(string id, string description) =>
        new($"{DistributionInfo.CommandName} schema {ResultEnvelope.CatalogId("common", id)}", description);

    private static Command Find(Command parent, string name) =>
        parent.Subcommands.Single(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));

    private static Command? FindOptional(Command parent, string name) =>
        parent.Subcommands.SingleOrDefault(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));
}
