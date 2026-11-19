using Aspose.Cells;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// How the object table opens and saves a workbook: the loader's open, and a save in the format
/// the workbook was read in, with the password protection decides. Every warning the engine
/// reports through its callback goes to the invocation's log as it was reported.
/// </summary>
internal sealed class CellsDocuments(CellsWorkbookLoader loader) : ITableDocuments<Workbook>
{
    /// <inheritdoc />
    public DocumentTable<Workbook> Table => CellsTable.Definition;

    /// <inheritdoc />
    public IReadOnlyList<FormatDescriptor> Formats => CellsFormats.Editable;

    /// <inheritdoc />
    public IOpenedDocument<Workbook> Open(string path, Secret? password, EngineWarningLog warnings) => loader.Open(path, password, warnings);

    /// <inheritdoc />
    public IOpenedDocument<Workbook> Reopen(string path, Secret? password, EngineWarningLog warnings) => loader.Reopen(path, password, warnings);

    /// <inheritdoc />
    public void Save(IOpenedDocument<Workbook> document, string path, string format, OutputProtection protection, EngineWarningLog warnings)
    {
        Save(document.Document, path, format, protection, warnings);
        ((LoadedWorkbook)document).Resources.ThrowIfFailed();
    }

    /// <summary>Saves <paramref name="workbook"/> in <paramref name="format"/>, with the password <paramref name="protection"/> gives it, or none.</summary>
    internal static void Save(Workbook workbook, string path, string format, OutputProtection protection, EngineWarningLog warnings)
    {
        workbook.Settings.Password = protection.Password?.Reveal() ?? string.Empty;
        workbook.Save(path, CellsEngineFormats.Options(format, warnings));
    }
}
