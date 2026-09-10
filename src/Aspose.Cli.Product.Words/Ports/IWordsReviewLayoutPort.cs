namespace Aspose.Cli.Product.Words.Ports;

/// <summary>Product-internal fixed-layout facts used by the Words review gate.</summary>
internal interface IWordsReviewLayoutPort
{
    WordsReviewLayout InspectReviewLayout(
        string filePath,
        string? password,
        int maxPages);
}
