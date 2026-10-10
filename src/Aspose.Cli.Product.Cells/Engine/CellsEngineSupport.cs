using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Contract projection helpers the Cells handlers share.</summary>
internal static class CellsEngineSupport
{
    internal static SourceInfo BuildSource(string path, LoadedWorkbook loaded) => new()
    {
        Path = path,
        Format = CellsEngineFormats.IdOf(loaded.SourceFormat),
        SizeBytes = new FileInfo(path).Length,
        Fingerprint = FileFingerprints.Capture(path),
    };
}
