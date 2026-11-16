using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Cells;

/// <summary>Worksheet image and interactive workbook views.</summary>
internal sealed class CellsViewAdapter : IProductViewAdapter<CellsSession>
{
    public IReadOnlyList<ProductView> Views { get; } =
    [
        new(CellsViews.Sheets, "Sheets", ViewPartKinds.Image),
        new(CellsViews.Workbook, "Workbook", ViewPartKinds.Html),
    ];

    public string LiveView => CellsViews.Workbook;

    public ViewManifest Render(
        CellsSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        CellsView.Render(session, filePath, request, artifacts);
}
