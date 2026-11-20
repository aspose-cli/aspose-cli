using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Aspose.PDF evaluation mode: a run without a license says so in its result's license mode, and
/// what the engine writes into a document or gives of its collections is read as it is. It
/// recognizes the engine's one evaluation limit, a collection element past the ones it shows,
/// which the SDK reports as <c>EVALUATION_LIMIT</c> when a command lets it through.
/// </summary>
internal sealed class PdfEvaluationProfile : IEvaluationProfile<Document>
{
    // The engine's message on the IndexOutOfRangeException of an element evaluation mode does not show.
    private const string LimitMessage = "can be viewed in evaluation mode";

    // How many elements of any collection evaluation mode shows.
    private const int ShownElements = 4;

    public string Engine => "Aspose.PDF";

    public EvaluationMarks Inspect(Document document) => EvaluationMarks.None;

    public EvaluationLimitFact? RecognizeLimit(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is IndexOutOfRangeException && current.Message.Contains(LimitMessage, StringComparison.Ordinal))
            {
                return new EvaluationLimitFact($"shows at most {ShownElements} elements of any collection", ShownElements, "elements");
            }
        }

        return null;
    }
}
