# PDF HTML import egress acceptance: PDF-HTML-EGRESS

This explicitly invoked gate asserts that the Aspose.PDF HTML importer makes no network
request when the documented custom resource loader refuses every resource. It **fails on
Aspose.PDF.Drawing 26.8.0** in both licensed and evaluation modes. It is outside normal
regression discovery under `tests/acceptance`, adds no project or dependency, and neither
skips failures nor accepts the broken result as its baseline.

## Run

Use PowerShell 7 after a normal repository build. A license is optional; the defect does
not depend on it. Run from the repository root:

```powershell
$env:ASPOSE_PDF_LICENSE_PATH = 'C:/private/Aspose.PDF.lic'
./tests/acceptance/pdf-html-egress/reproduce.ps1 `
  -SdkDir ./src/Aspose.Cli/bin/Release/net10.0 `
  -OutputDirectory ./artifacts/pdf-html-egress/run-01
```

Choose a fresh output directory on every run. The script starts a counting HTTP server on
a loopback port, writes a synthetic HTML file whose stylesheet and image point at it, and
imports it through the public SDK only:

```csharp
var options = new HtmlLoadOptions(outputDirectory + Path.DirectorySeparatorChar);
options.CustomLoaderOfExternalResources = uri =>
    new LoadOptions.ResourceLoadingResult(new byte[0]) { LoadingCancelled = false };
using var document = new Document(htmlPath, options);
document.Save(pdfPath);
```

No CLI assembly or product engine is loaded. `result.json` records the SDK version and
hash, every loader call and every request line the server received. The script returns
**0 when no request was made**, **1 when the importer made a request** and **2 when the
check could not run**, including when the importer never consulted the loader.

## Observed behavior

With SDK 26.8 the server receives `GET /style.css` and `GET /image.png`, and only then
does the importer call the custom loader with each address. The loader's result decides
what is embedded, but it cannot prevent the request. The same happens with every result
variant tried: empty or non-empty data, `LoadingCancelled` true or false, an exception in
`ExceptionOfLoadingIfAny`, a MIME type, a null result, with or without a base path, and
from a file or a stream. `HtmlLoadOptions` exposes no other public switch for external
loading. Local `file:` references do go through the loader first, and anchors (`<a href>`),
XML namespace names and document type identifiers are never requested.

The Markdown importer (`MdLoadOptions`) has no resource hook at all: it requests remote
images and reads local image files outside the input directory directly. SVG images placed
with `Image.File` or stamped with `ImageStamp` likewise request their external stylesheets
and images, with no hook.

## Ownership and release boundary

This is an upstream SDK defect: the documented resource-loading callback does not govern
network access. The CLI does not rewrite or sanitize HTML. Until a fixed SDK passes this
gate, `pdf create --from-html`, Markdown `--from-text` and SVG image inputs refuse any
input that names a network address, with `FEATURE_UNSUPPORTED`, before the engine reads
it; and an HTML import
whose loader nevertheless sees a network address fails without publishing output instead
of reporting the resource as blocked. When this gate passes, remove the refusal and keep
the loader as the only policy.

Official references:

- [Convert HTML to PDF](https://docs.aspose.com/pdf/net/convert-html-to-pdf/)
- [HtmlLoadOptions](https://reference.aspose.com/pdf/net/aspose.pdf/htmlloadoptions/)
- [LoadOptions.ResourceLoadingStrategy](https://reference.aspose.com/pdf/net/aspose.pdf/loadoptions.resourceloadingstrategy/)
