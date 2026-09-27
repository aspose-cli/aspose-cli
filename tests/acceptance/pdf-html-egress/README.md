# PDF HTML import egress acceptance: PDF-HTML-EGRESS

This explicitly invoked gate asserts that the Aspose.PDF HTML importer makes no network
request when the documented custom resource loader refuses every resource, and that the
Markdown importer makes none either. It **fails on Aspose.PDF.Drawing 26.8.0** in both
licensed and evaluation modes. It is outside normal
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

It then imports a synthetic Markdown file whose image points at the server, with
`new Document(markdownPath, new MdLoadOptions())`; `MdLoadOptions` has no resource hook to
install. When an SDK release adds one, install a refusing hook here as the HTML case does.

No CLI assembly or product engine is loaded. `result.json` records the SDK version and
hash, every loader call and the request lines the server received for each import. The
script returns **0 when no request was made**, **1 when either importer made a request**
and **2 when the check could not run**, including when the HTML importer never consulted
the loader.

## Observed behavior

With SDK 26.8 the server receives `GET /style.css` and `GET /image.png`, and only then
does the importer call the custom loader with each address. The loader's result decides
what is embedded, but it cannot prevent the request. The same happens with every result
variant tried: empty or non-empty data, `LoadingCancelled` true or false, an exception in
`ExceptionOfLoadingIfAny`, a MIME type, a null result, with or without a base path, and
from a file or a stream. `HtmlLoadOptions` exposes no other public switch for external
loading. Local `file:` references do go through the loader first, including those named
inside fetched resources, and anchors (`<a href>`), XML namespace names and document type
identifiers are never requested. Addresses named by a stylesheet or SVG file the loader
supplies are requested before the loader sees them too. The importer runs script: an
address that script computes is requested. With a
`LoadingCancelled` result the importer keeps what it fetched, without a second request.
An unanswered request holds the import for about 100 seconds before the loader is called.

The Markdown importer (`MdLoadOptions`) has no resource hook at all. It passes raw HTML to
the HTML engine: it requests remote images (inline and reference-style), stylesheets,
`@import`s, CSS images and fonts, SVG, `<object>`, `<iframe>` and `<script>` sources, and
runs script. It reads every local file those name, with no boundary: absolute paths,
`file:` URIs, `..` escapes after percent and character-reference decoding, and `file:`
addresses inside fetched stylesheets, SVG and HTML. It resolves the Markdown's relative
paths against the process working directory, not the Markdown file's directory, and has no
base-path option; a loaded file's references resolve against that file. Hyperlinks,
autolinks and references inside code are not loaded. SVG images placed with `Image.File` or
stamped with `ImageStamp` likewise request their external stylesheets and images, with no
hook.

## Handling

KNOWN-ISSUES.md describes the CLI's handling of this defect under the gate's id, and the code
that handles it names the id. While this gate exits 1 the release proceeds; when it exits 0,
delete the issue, its handling and this gate. Do not make it pass by rewriting input or output,
or by weakening its assertion.

Official references:

- [Convert HTML to PDF](https://docs.aspose.com/pdf/net/convert-html-to-pdf/)
- [HtmlLoadOptions](https://reference.aspose.com/pdf/net/aspose.pdf/htmlloadoptions/)
- [LoadOptions.ResourceLoadingStrategy](https://reference.aspose.com/pdf/net/aspose.pdf/loadoptions.resourceloadingstrategy/)
