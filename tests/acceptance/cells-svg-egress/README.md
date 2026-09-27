# Cells SVG picture egress acceptance: CELLS-SVG-EGRESS

This explicitly invoked gate asserts that adding an SVG picture to a worksheet makes no
network request when the workbook's documented resource provider refuses every resource.
It **fails on Aspose.Cells 26.9.0** in both licensed and evaluation modes. It is outside
normal regression discovery under `tests/acceptance`, adds no project or dependency, and
neither skips failures nor accepts the broken result as its baseline.

## Run

Use PowerShell 7 after a normal repository build. A license is optional; the defect does
not depend on it. Run from the repository root:

```powershell
$env:ASPOSE_CELLS_LICENSE_PATH = 'C:/private/Aspose.Cells.lic'
./tests/acceptance/cells-svg-egress/reproduce.ps1 `
  -SdkDir ./src/Aspose.Cli/bin/Release/net10.0 `
  -OutputDirectory ./artifacts/cells-svg-egress/run-01
```

Choose a fresh output directory on every run. The script starts a counting HTTP server on
a loopback port and adds a synthetic SVG whose `xlink:href` image points at it, through the
public SDK only:

```csharp
var workbook = new Workbook();
workbook.Settings.ResourceProvider = provider; // InitStream: ResourceLoadingType.Skip, no stream
workbook.Worksheets[0].Pictures.Add(1, 1, new MemoryStream(svg, writable: false));
workbook.Save(xlsxPath);
```

No CLI assembly or product engine is loaded. On Windows the script preloads the SkiaSharp
native library shipped beside the SDK, which SVG rasterization needs inside PowerShell.
`result.json` records the SDK version and hash, every provider call and every request line
the server received. The script returns **0 when no request was made**, **1 when adding the
picture made a request** and **2 when the check could not run**.

## Observed behavior

With SDK 26.9 `Pictures.Add` requests the SVG's `xlink:href` image twice while it builds
the picture; `WorkbookSettings.ResourceProvider`, documented as the stream provider for
external resources, is never called. The same happens without a provider and from a file
stream. The requests come from rasterizing the picture's fallback image:
`Shapes.AddSvg` with a caller-supplied fallback image makes none. Saving afterwards,
reloading the saved workbook and rendering it to PDF, PNG or HTML make no request, and a
compressed SVG (SVGZ) is not recognized as an image.

## Handling

KNOWN-ISSUES.md describes the CLI's handling of this defect under the gate's id, and the code
that handles it names the id. While this gate exits 1 the release proceeds; when it exits 0,
delete the issue, its handling and this gate. Do not make it pass by rewriting input or output,
or by weakening its assertion.

Official references:

- [Insert pictures](https://docs.aspose.com/cells/net/insert-pictures/)
- [WorkbookSettings.ResourceProvider](https://reference.aspose.com/cells/net/aspose.cells/workbooksettings/resourceprovider/)
- [IStreamProvider](https://reference.aspose.com/cells/net/aspose.cells/istreamprovider/)
