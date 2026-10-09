using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Contract projection helpers the Cells handlers share.</summary>
internal static class CellsEngineSupport
{
    /// <summary>
    /// The non-null warnings the caller produced; the write pipeline adds the evaluation
    /// disclosure. Returns <c>null</c> when there are none, matching the envelope's
    /// omit-when-empty rule.
    /// </summary>
    internal static IReadOnlyList<Warning>? CombineWarnings(params Warning?[] extra)
    {
        List<Warning>? warnings = null;
        foreach (Warning? warning in extra)
        {
            if (warning is not null)
            {
                (warnings ??= []).Add(warning);
            }
        }

        return warnings;
    }


    internal static SourceInfo BuildSource(string path, Workbook workbook) => new()
    {
        Path = path,
        Format = CellsEngineFormats.IdOf(ResolveSourceFormat(path, workbook)),
        SizeBytes = new FileInfo(path).Length,
        Fingerprint = FileFingerprints.Capture(path),
    };

    /// <summary>
    /// The honest on-disk format of the file the user gave us. Detection is
    /// authoritative — <see cref="Workbook.FileFormat"/> reports the in-memory
    /// model, which the engine "upgrades" to <c>Xlsx</c> for the ancient BIFF
    /// family (an <c>Excel2</c> file would otherwise be reported as <c>xlsx</c>).
    /// The loaded workbook is trusted only where detection cannot see the true
    /// format: an <b>encrypted</b> container detects as its wrapper (an encrypted
    /// <c>.xlsx</c> sniffs as the OOXML/OLE2 shell, whereas the decrypted workbook
    /// knows it is <c>xlsx</c>), and a defeated sniff returns <c>Unknown</c> (an
    /// HTML export whose leading blank lines beat the detector still loaded as
    /// <c>Html</c> via the fallback).
    /// </summary>
    internal static FileFormatType ResolveSourceFormat(string path, Workbook workbook)
    {
        FileFormatInfo detected;
        try
        {
            using FileStream input = InputFiles.OpenRead(path);
            detected = FileFormatUtil.DetectFileFormat(input);
        }
        catch
        {
            return workbook.FileFormat;
        }

        return detected.IsEncrypted || detected.FileFormatType == FileFormatType.Unknown
            ? workbook.FileFormat
            : detected.FileFormatType;
    }

}
