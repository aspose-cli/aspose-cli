using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Help examples for commands owned by the executable host.</summary>
internal static class HostHelpMetadata
{
    public static void Attach(RootCommand root)
    {
        root.WithExamples(
            [
                "aspose-cli app",
                "aspose-cli capabilities --output json",
                "aspose-cli review <file> --max-items 50",
                "aspose-cli schema",
            ],
            [
                new("aspose-cli docs", "offline documentation bundled in this binary"),
                new("aspose-cli schema", "the available JSON Schema identifiers"),
                new("aspose-cli capabilities --output json", "verbs, formats and operations as data"),
            ]);
        Find(root, "app").WithExamples(
            [
                "aspose-cli app",
                "aspose-cli app --welcome",
                "aspose-cli app --foreground --port 4680",
                "aspose-cli app status --output json",
            ],
            [new("aspose-cli docs app", "welcome, files, preview, settings and lifecycle")]);
        Find(root, "preview").WithExamples(
            [
                "aspose-cli preview status --output json",
                "aspose-cli preview stop --all --output json",
            ]);
        Find(root, "review").WithExamples(
            [
                "aspose-cli review <file>",
                "aspose-cli review <file> --out evidence --max-items 50 --output json",
            ],
            [new("aspose-cli docs verification", "the visual inspection and verification protocol")]);
        FindOptional(root, "license")?.WithExamples(
            [
                "aspose-cli license status --output json",
                "aspose-cli license install Aspose.Total.lic",
            ]);
        Find(root, "doctor").WithExamples(
            ["aspose-cli doctor --output json"]);
        if (FindOptional(root, "fonts") is { } fonts)
        {
            fonts.WithExamples(
                ["aspose-cli fonts list --output json"]);
        }
        Find(root, "docs").WithExamples(["aspose-cli docs"]);
        Find(root, "schema").WithExamples(["aspose-cli schema"]);
        Find(root, "capabilities").WithExamples(
            [
                "aspose-cli capabilities --output json",
                "aspose-cli capabilities <product> --output json",
                "aspose-cli capabilities <product> <verb> --output json",
            ]);
        Find(root, "skill").WithExamples(
            [
                "aspose-cli skill list",
                "aspose-cli skill install <skill> --host codex --scope project",
            ]);
    }

    private static Command Find(Command root, string name) =>
        root.Subcommands.Single(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));

    private static Command? FindOptional(Command root, string name) =>
        root.Subcommands.SingleOrDefault(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));
}
