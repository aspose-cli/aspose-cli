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
    public void FillAndStamp_ProducesAFilledAndStampedReport()
    {
        PdfEngineFixture.RequireLicense();
        using var workspace = new TempWorkspace();
        InstalledSkillExampleResult result = InstalledSkillExample.Run(workspace, "aspose-cli-pdf", "fill-and-stamp");

        Assert.NotEmpty(result.Outputs);
        using var report = new Document(Path.Combine(result.Directory, "report.filled.pdf"));
        Assert.Equal(2, report.Pages.Count);
        for (int number = 1; number <= 2; number++)
        {
            var absorber = new TextAbsorber();
            report.Pages[number].Accept(absorber);
            Assert.Contains($"Page {number} of 2", absorber.Text, StringComparison.Ordinal);
            Assert.Contains("DRAFT", absorber.Text, StringComparison.Ordinal);
        }

        Assert.Equal("Quarterly Review", ((TextBoxField)report.Form["ReportTitle"]).Value);
        Assert.Equal("Executive Committee", ((TextBoxField)report.Form["PreparedFor"]).Value);
    }
}
