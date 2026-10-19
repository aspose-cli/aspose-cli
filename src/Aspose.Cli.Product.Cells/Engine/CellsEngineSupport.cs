using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Contract projection helpers the Cells handlers share.</summary>
internal static class CellsEngineSupport
{
    internal static InputInfo BuildSource(string path, LoadedWorkbook loaded) =>
        FileObjects.Input(path, CellsEngineFormats.IdOf(loaded.SourceFormat), loaded.IsEncrypted);
}
