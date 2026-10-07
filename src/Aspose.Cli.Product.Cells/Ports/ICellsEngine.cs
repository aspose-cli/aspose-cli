using Aspose.Cli.Sdk.Ports;

namespace Aspose.Cli.Product.Cells.Ports;

/// <summary>
/// The spreadsheet engine port. Everything that crosses this boundary is
/// either a primitive, a core value type or a public contract type — engine
/// SDK types never appear here. Implementations translate engine failures
/// into <see cref="Aspose.Cli.Sdk.Errors.CliException"/>s.
/// </summary>
/// <remarks>
/// The port is synchronous by design: the underlying engines are synchronous
/// and the CLI is a one-shot process, so async signatures would only add
/// ceremony without concurrency. The engine's font environment is a separate,
/// cross-product concern — see <see cref="IFontEnvironment"/>.
/// </remarks>
public interface ICellsEngine
{
    /// <summary>Structural summary of a workbook (projection ladder step one).</summary>
    WorkbookInfoResult GetInfo(string filePath, InfoRequest request);

    /// <summary>Windowed projection of one sheet's cell data (ladder step two).</summary>
    WorkbookReadResult Read(string filePath, ReadRequest request);

    /// <summary>Converts a workbook to another document format.</summary>
    ConvertResult Convert(string filePath, ConvertRequest request);

    /// <summary>Renders one sheet (or a range of it) to an image.</summary>
    RenderResult Render(string filePath, RenderRequest request);

    /// <summary>Renders the parts of one product view, opening the workbook once.</summary>
    Aspose.Cli.Sdk.Views.ViewManifest RenderView(
        string filePath,
        Aspose.Cli.Sdk.Views.ViewRenderRequest request,
        Aspose.Cli.Sdk.Views.IViewArtifactSink artifacts);

    /// <summary>Applies a validated ops batch atomically.</summary>
    EditResult ApplyOps(string filePath, CellsOpsBatch batch, EditRequest options);

    /// <summary>Creates a new workbook with the given sheets.</summary>
    CreateResult Create(NewWorkbookRequest request);

    /// <summary>Compares two workbooks structurally (values, and formulas in scope).</summary>
    DiffResult Diff(string leftPath, string rightPath, DiffRequest request);

    /// <summary>Finds cells whose value or formula matches a pattern (budgeted).</summary>
    SearchResult Search(string filePath, SearchRequest request);
}
