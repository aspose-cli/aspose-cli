using System.CommandLine;

namespace Aspose.Cli.Host.Invocation;

/// <summary>Help examples for commands owned by the executable host.</summary>
internal static class HostHelpMetadata
{
    public static void Attach(RootCommand root)
    {
        root.WithExamples(
            [
                "app",
                "capabilities --output json",
                "review <file> --max-items 50",
                "schema",
            ],
            [
                new("aspose-cli docs", "offline documentation bundled in this binary"),
                new("aspose-cli schema", "the available JSON Schema identifiers"),
                new("aspose-cli capabilities --output json", "verbs, formats and operations as data"),
            ]);
        Find(root, "app").WithExamples(
            [
                "app",
                "app --welcome",
                "app --foreground --port 4680",
                "app status --output json",
            ],
            [new("aspose-cli docs app", "welcome, files, preview, settings and lifecycle")]);
        Find(root, "preview").WithExamples(
            [
                "preview status --output json",
                "preview stop --all --output json",
            ]);
        Find(root, "review").WithExamples(
            [
                "review <file>",
                "review <file> --out evidence --max-items 50 --output json",
            ],
            [new("aspose-cli docs verification", "the delivery checklist, the review protocol and filtering by check code")]);
        FindOptional(root, "license")?.WithExamples(
            [
                "license status --output json",
                "license install Aspose.Total.lic",
            ]);
        Find(root, "doctor").WithExamples(
            ["doctor --output json"]);
        if (FindOptional(root, "fonts") is { } fonts)
        {
            fonts.WithExamples(
                ["fonts list --output json"]);
        }
        Find(root, "docs").WithExamples(["docs"]);
        Find(root, "schema").WithExamples(["schema"]);
        Find(root, "capabilities").WithExamples(
            [
                "capabilities --output json",
                "capabilities <product> --output json",
                "capabilities <product> <verb> --output json",
            ]);
        Find(root, "skill").WithExamples(
            [
                "skill list",
                "skill install <skill> --host codex --scope project",
            ]);
    }

    private static Command Find(Command root, string name) =>
        root.Subcommands.Single(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));

    private static Command? FindOptional(Command root, string name) =>
        root.Subcommands.SingleOrDefault(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));
}
