using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Words.Commands;

/// <summary>Publishes document edit locations to a running Words preview.</summary>
internal static class WordsPreviewHintPublisher
{
    private const int MaxTargets = 12;

    public static void Publish(WordsEditResult result, string outputPath,
        Func<string, IReadOnlyList<ProductPreviewPayload>, bool> publish)
    {
        if (result.DryRun || result.Output is null || result.PagesTouched is not { Count: > 0 })
        {
            return;
        }

        try
        {
            ProductPreviewPayload[] targets = result.PagesTouched
                .Distinct()
                .Order()
                .Take(MaxTargets)
                .Select(static page =>
                    WordsPreviewPayloads.Hint(
                        new WordsPreviewHint(page)))
                .ToArray();
            publish(outputPath, targets);
        }
        catch (Exception)
        {
            // Preview emphasis is decorative; the completed edit always wins.
        }
    }
}
