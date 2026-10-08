using Aspose.Cli.Sdk.Licensing;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>Page images of a Words document show only the evaluation marks of their pages.</summary>
public sealed class WordsEvaluationProfileTests
{
    [Fact]
    public void PageImages_ShowTheBannerOnlyOnTheFirstPage()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln(WordsFixture.BannerText);
        builder.Writeln("First page.");
        builder.InsertBreak(BreakType.PageBreak);
        builder.Write("Second page.");
        IEvaluationProfile<Document> profile = new WordsEvaluationProfile();

        Assert.Contains(profile.Inspect(document, "png", [1]).Marks, static mark => mark.Contains("banner", StringComparison.Ordinal));
        Assert.Empty(profile.Inspect(document, "png", [2]).Marks);
    }
}
