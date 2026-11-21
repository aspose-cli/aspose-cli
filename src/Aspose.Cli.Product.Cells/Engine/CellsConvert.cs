using Aspose.Cells;
using Aspose.Cells.Rendering;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// <c>cells convert</c>: a workbook, or CSV or TSV text, saved in another format with the engine's
/// options for it, the ones the caller stated set: the sheet a one-sheet format writes, the sheets
/// a PDF writes, and how a text output writes its values.
/// </summary>
internal static class CellsConvert
{
    internal static CellsConvertResult Run(CellsSession session, CellsConvertRequest request)
    {
        LicenseState state = session.Outputs.License;
        CellValueFormatStrategy? strategy = request.FormatStrategy is { } name ? CellsEngineFormats.FormatStrategy(name) : null;
        var log = new EngineWarningLog();
        using LoadedWorkbook loaded = request.TextInput is { } text
            ? session.Loader.OpenText(request.Input, text.Format, text, log)
            : session.Loader.OpenForConvert(request.Input, request.Password, log);
        Workbook workbook = loaded.Workbook;
        string format = request.Output.Format.Id;
        SaveOptions options = CellsEngineFormats.Options(format, log);
        if (request.Sheets is { } names)
        {
            int[] sheets = [.. names.Select(name => CellsSheets.Find(workbook, name).Index)];
            switch (options)
            {
                // A delimited text output is the workbook's active sheet.
                case TxtSaveOptions:
                    workbook.Worksheets.ActiveSheetIndex = sheets[0];
                    break;
                case MarkdownSaveOptions markdown:
                    markdown.SheetSet = new SheetSet(sheets);
                    break;
                case PdfSaveOptions pdf:
                    pdf.SheetSet = new SheetSet(sheets);
                    break;
            }
        }

        if (strategy is { } value)
        {
            switch (options)
            {
                case TxtSaveOptions txt:
                    txt.FormatStrategy = value;
                    break;
                case MarkdownSaveOptions markdown:
                    markdown.FormatStrategy = value;
                    break;
            }
        }

        // --bom: the engine writes UTF-8 with its mark, which the publication then keeps.
        if (request.ByteOrderMark && options is TxtSaveOptions marked)
        {
            marked.Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, loaded.Protection, request.Password);
        PublishedFile written = session.Outputs.Write(
            request.Output.Path,
            request.Output.Format,
            request.Output.Overwrite,
            temp =>
            {
                log.During = "save";
                workbook.Settings.Password = protection.Password?.Reveal() ?? string.Empty;
                workbook.Save(temp, options);
                loaded.Resources.ThrowIfFailed();
            },
            encrypted: protection.Encrypted,
            byteOrderMark: request.ByteOrderMark);
        return new CellsConvertResult
        {
            Input = FileObjects.Input(request.Input, loaded.FormatId, loaded.Encrypted),
            Outputs = [new CellsConvertOutput(FileObjects.Output(written, request.Output.Format, protection.Encrypted))],
            Sheets = request.Sheets,
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = loaded.Warnings.Count == 0 ? null : loaded.Warnings,
        };
    }
}

/// <summary>The sheet an option names, found as the table finds <c>sheet/&lt;name&gt;</c>.</summary>
internal static class CellsSheets
{
    /// <summary>The one sheet whose name is <paramref name="name"/> but for case.</summary>
    /// <exception cref="Sdk.Errors.CliException"><c>ADDRESS_NOT_FOUND</c> for none, <c>ADDRESS_AMBIGUOUS</c>'s fact for several.</exception>
    internal static Worksheet Find(Workbook workbook, string name)
    {
        Worksheet[] matches = [.. workbook.Worksheets.Where(sheet => string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase))];
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw TableErrors.NotFound("sheet", name, [.. workbook.Worksheets.Select(static sheet => sheet.Name)]),
            _ => throw Sdk.Errors.CliErrors.OptionInvalid(CellsOptionNames.Sheet, $"'{name}' names {matches.Length} sheets: {string.Join(", ", matches.Select(static sheet => $"'{sheet.Name}'"))}"),
        };
    }
}
