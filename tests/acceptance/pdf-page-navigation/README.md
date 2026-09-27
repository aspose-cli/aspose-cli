# PDF page-navigation acceptance: PDF-MOVE-BOOKMARK

This explicitly invoked gate asserts the desired navigation-preserving page move and
**fails on the current CLI with Aspose.PDF.Drawing 26.8.0**. It is outside normal
regression discovery under `tests/acceptance`, adds no project or dependency, and
neither skips failures nor accepts a broken result as its expected baseline.

## Minimal synthetic fixture

`input.pdf` is a deterministic, independently authored two-page PDF: Approval on page
1 and Appendix on page 2. Its four bookmarks are Approval (Fit), Appendix (Fit),
Appendix-null (XYZ with three null/inherited parameters), and Appendix-zero (XYZ
with three numeric zero parameters). Both Appendix variants deliberately refer to
page 2. The explicit null/zero distinction can be inspected in the small, readable
fixture source, `create-fixture.ps1`. That script only authors this independent test
input; it is not a production PDF writer, postprocessor, or repair tool.

Regenerate the same fixture at a fresh path, without overwriting the shipped input:

```powershell
./tests/acceptance/pdf-page-navigation/create-fixture.ps1 `
  -Output ./artifacts/pdf-page-navigation/independent-input.pdf
```

## Run the separate failing gate

Use PowerShell 7 and the .NET runtime required by the supplied commercial SDK. Run
from the repository root, supply a matching CLI/SDK pair and an existing PDF license:

```powershell
$env:ASPOSE_PDF_LICENSE_PATH = 'C:/private/Aspose.PDF.lic'
./tests/acceptance/pdf-page-navigation/reproduce.ps1 `
  -CliPath ./src/Aspose.Cli/bin/Release/net10.0/aspose-cli.exe `
  -SdkDir ./src/Aspose.Cli/bin/Release/net10.0 `
  -OutputDirectory ./artifacts/pdf-page-navigation/run-01
```

Choose a fresh output directory on every run. The script records executable/SDK/input
hashes, the exact bounded CLI command and both output channels. It runs the actual CLI
`move_pages` operation (`pages: "2", to: 1`) and a separate public-SDK control:

```csharp
using var document = new Document(input);
var page = document.Pages[2];
document.Pages.Insert(1, page);
document.Pages.Delete(3);
document.Save(output);
```

No CLI engine, private SDK member, raw PDF patch, destination replacement, or SDK
behavior/default override is used by the native control. License contents are not
copied or logged. The original fixture remains unchanged.

`verify.ps1` reopens each saved output through public SDK APIs. Eight assertions check
page count, both page texts, bookmark count and each bookmark's destination page/type.
Desired result: Appendix is page 1, Approval is page 2, and all three Appendix bookmarks
still target page 1. Actual current result: page order is correct, and Approval and
Appendix (Fit) target their pages; Appendix-null and Appendix-zero report
`PageNumber = 0`, because their coordinates read 0 and the CLI does not rebuild a
destination it cannot reproduce exactly. The native control leaves all three Appendix
bookmarks at `PageNumber = 0`.

The aggregate script and verifier return **1 for failed desired assertions**, **0 only
when those assertions pass**, and **2 for setup/operation exceptions**. CLI exit 0 is
not a navigation pass. A retained output can be checked separately:

```powershell
./tests/acceptance/pdf-page-navigation/verify.ps1 `
  -Document ./artifacts/pdf-page-navigation/run-01/cli.pdf `
  -SdkDir ./src/Aspose.Cli/bin/Release/net10.0
```

The getter observation in `provenance.json` is diagnostic, not a passing preservation
assertion. SDK 26.8 reads both source null and source zero coordinates as double zero.
`NullSemanticsVerified` stays false: a future page-target pass alone is insufficient to
certify complete navigation fidelity. No NaN sentinel is used by this reproduction.

## Observed behavior

The SDK has no page move: `PageCollection` offers insertion and deletion only, inserting a
page copies it, and reinserting a removed page throws. Copying pages keeps the links between
them, but every other destination that named an original is left without a page. Typed
coordinate getters and `ToString` read an omitted (null) coordinate as 0, the destination Page
property is read-only, and no public constructor writes an omitted coordinate:
`XYZExplicitDestination` with `NaN` writes the invalid token `NaN`. `PdfFileEditor.Extract`
with CopyOutlines, CopyLogicalStructure and KeepActions drops every bookmark, so it is no move
substitute either.

## Handling

KNOWN-ISSUES.md describes the CLI's handling of this defect under the gate's id, and the code
that handles it names the id. While this gate exits 1 the release proceeds; when it exits 0,
delete the issue, its handling and this gate. Do not make it pass by rewriting input or output,
or by weakening its assertion.

Official references:

- [Move pages example](https://docs.aspose.com/pdf/net/move-pages/)
- [PageCollection insertion](https://reference.aspose.com/pdf/net/aspose.pdf/pagecollection/insert/)
- [ExplicitDestination properties and factories](https://reference.aspose.com/pdf/net/aspose.pdf.annotations/explicitdestination/)
- [GoToAction destination](https://reference.aspose.com/pdf/net/aspose.pdf.annotations/gotoaction/destination/)
- [PdfFileEditor API](https://reference.aspose.com/pdf/net/aspose.pdf.facades/pdffileeditor/)
