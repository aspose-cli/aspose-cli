using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>The bundled PDF example runs as written and produces what its README promises.</summary>
[Category(TestCategory.Slow)]
public sealed class PdfSkillExampleTests
{
    [Fact]
    public void AssembleReviewReport_ProducesAValidFilledAndStampedArchive()
    {
        using var workspace = new TempWorkspace();
        InstalledSkillExampleResult result = InstalledSkillExample.Run(workspace, "aspose-cli-pdf", "assemble-review-report");

        Assert.Contains(result.Outputs, output => output["profile"]?.GetValue<string>() == "pdfa-2b"
            && output["valid"]!.GetValue<bool>());
        using var archive = new Document(Path.Combine(result.Directory, "review.archive.pdf"));
        Assert.Equal(2, archive.Pages.Count);
        for (int number = 1; number <= 2; number++)
        {
            var absorber = new TextAbsorber();
            archive.Pages[number].Accept(absorber);
            Assert.Contains($"Page {number} of 2", absorber.Text, StringComparison.Ordinal);
            Assert.Contains("DRAFT", absorber.Text, StringComparison.Ordinal);
        }
        Assert.Equal("Quarterly Review", ((TextBoxField)archive.Form["ReportTitle"]).Value);
        Assert.Equal("Executive Committee", ((TextBoxField)archive.Form["PreparedFor"]).Value);
        Assert.True(File.Exists(Path.Combine(result.Directory, "review.archive.review/review.json")));
    }
}
