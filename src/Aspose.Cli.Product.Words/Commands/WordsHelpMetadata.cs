using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Commands;

internal static class WordsHelpMetadata
{
    public static void Attach(Command root)
    {
        root.WithExamples(
            [
                "aspose-cli words inspect contract.docx --detail outline sections --preview",
                "aspose-cli words query blocks contract.docx --blocks 1-30 --scope full",
            ],
            [
                new("aspose-cli docs words/editing", "the document block model and edit operations"),
                new("aspose-cli docs words/verification", "read-back, semantic and visual verification"),
                new("aspose-cli schema v2/words/ops", "the operation JSON schema"),
            ]);
        Find(root, "inspect").WithExamples(
            [
                "aspose-cli words inspect contract.docx --output json",
                "aspose-cli words inspect contract.docx --detail outline sections fields bookmarks --preview",
            ]);
        Command query = Find(root, "query");
        Find(query, "blocks").WithExamples(
            [
                "aspose-cli words query blocks contract.docx --blocks 1-30 --scope full --output json",
                "aspose-cli words query blocks contract.docx --section 2 --scope text",
            ]);
        Find(root, "convert").WithExamples(
            [
                "aspose-cli words convert contract.docx --to pdf",
                "aspose-cli words convert contract.docx --to pdf --pages 1-3 --out excerpt.pdf",
                "aspose-cli words convert contract.docx --to pdf --font-dir fonts",
            ]);
        Find(root, "render").WithExamples(
            [
                "aspose-cli words render contract.docx --pages 1-2 --out review.png",
                "aspose-cli words render contract.docx --all-pages --dpi 192 --out review.png",
            ]);
        Find(root, "create").WithExamples(
            [
                "aspose-cli words create report.docx --markdown report.md --template brand.docx --title \"Quarterly report\"",
                "aspose-cli words create letter.docx --template letter.dotx",
            ]);
        Find(root, "edit").WithExamples(
            [
                "aspose-cli words edit contract.docx --in-place --backup --verify --set \"bookmark:Client=Contoso\"",
                "aspose-cli words edit contract.docx --in-place --backup --ops ops.json --verify",
            ],
            [
                new("aspose-cli docs words/editing", "addressing and operation recipes"),
                new("aspose-cli schema v2/words/ops", "the exact edit-batch contract"),
            ]);
        Find(root, "compare").WithExamples(
            [
                "aspose-cli words compare original.docx changed.docx --output json",
                "aspose-cli words compare original.docx changed.docx --out redline.docx",
            ]);
        Find(query, "search").WithExamples(
            [
                "aspose-cli words query search contract.docx --pattern TODO --scope all",
                "aspose-cli words query search contract.docx --pattern \"Section\\s+\\d+\" --regex --max-hits 20",
            ]);
        Find(root, "split").WithExamples(
            [
                "aspose-cli words split report.docx --by heading1 --out-dir chapters",
                "aspose-cli words split report.docx --by pages --pages 1-3,8 --out-dir excerpts",
            ]);
        Find(root, "extract").WithExamples(
            [
                "aspose-cli words extract report.docx --what images --out-dir images",
                "aspose-cli words extract report.docx --what text --out-dir text",
            ]);
    }

    private static Command Find(Command root, string name) =>
        root.Subcommands.Single(command =>
            string.Equals(command.Name, name, StringComparison.Ordinal));
}
