# Known issues

Confirmed defects in the commercial Aspose SDKs this CLI runs on, each reproduced with a minimal
SDK-only program. The CLI does not hide them ([AGENTS.md](AGENTS.md)); each is tracked upstream
and blocks the release until it is fixed or waived below.

## Upstream SDK defects

### Aspose.PDF.Drawing 26.8.0: HTML and Markdown import and SVG images reach the network (gate `PDF-HTML-EGRESS`)

- **Defect:** the HTML importer requests http(s) stylesheets and images, and runs script,
  before it calls `HtmlLoadOptions.CustomLoaderOfExternalResources`. The Markdown importer and
  SVG images (`Image.File`, `ImageStamp`) have no resource hook: they fetch remote resources and
  read any local file a reference names, and Markdown resolves relative paths against the
  process working directory.
- **CLI behavior:** HTML, Markdown and SVG input that names a network address or contains script
  is refused with `FEATURE_UNSUPPORTED` before the engine reads it. A Markdown import is refused
  unless every image, HTML and CSS reference resolves to an ordinary file beneath the Markdown
  file's directory. For trusted HTML, `pdf create --from-html --allow-network-resources` lets the
  importer fetch and lists every address in `NETWORK_RESOURCES_REQUESTED`; an unanswered request
  holds the import for up to 100 seconds, so combine it with `--timeout`.
- **Workaround:** `aspose-cli words convert page.html --to pdf` makes no network request.
- **Reproduction:** [tests/acceptance/pdf-html-egress](tests/acceptance/pdf-html-egress/README.md).

### Aspose.Cells 26.9.0: adding an SVG picture fetches its external images (gate `CELLS-SVG-EGRESS`)

- **Defect:** `Pictures.Add` requests the `xlink:href` images of an SVG, and
  `WorkbookSettings.ResourceProvider` is never consulted.
- **CLI behavior:** `insert_image` refuses an SVG that names a network address, and a compressed
  SVG, with `FEATURE_UNSUPPORTED`.
- **Reproduction:** [tests/acceptance/cells-svg-egress](tests/acceptance/cells-svg-egress/README.md).

### Aspose.PDF.Drawing 26.8.0: moving pages loses bookmark destinations (gate `PDF-MOVE-BOOKMARK`)

- **Defect:** after a page move, outline destinations point at `PageNumber=0`, and no public API
  retargets a destination without losing information.
- **CLI behavior:** an edit or merge that leaves navigation unresolved reports
  `NAVIGATION_DEGRADED` with the number of bookmarks, links and named destinations affected.
- **Reproduction:** [tests/acceptance/pdf-page-navigation](tests/acceptance/pdf-page-navigation/README.md).

### Aspose.Slides 26.9.0: chart layout and axis scale change on save (gate `SLD-003`)

- **Defect:** saving an unchanged PowerPoint chart moves its plot area and changes its automatic
  major unit.
- **Reproduction:** [tests/acceptance/slides-sdk-fidelity](tests/acceptance/slides-sdk-fidelity/README.md).

### Aspose.Slides 26.9.0: a non-string value in the per-user font registry key breaks Slides

- **Defect:** font initialization casts every value under
  `HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts` to `string`, so a value of another
  kind, such as a `REG_DWORD` written by other software, throws `InvalidCastException`.
- **CLI behavior:** every Slides command that loads fonts fails with `FEATURE_UNSUPPORTED`.
- **Workaround:** remove the non-string value from that key; Windows writes only string values
  there.
- **Tracking:** to be filed upstream. There is no acceptance gate, because reproducing it writes
  to the user's registry.

## Release gate waivers

`scripts/acceptance.ps1` lets a failing gate ship only with a row in this table for the version
in `Directory.Build.props`.

| Gate | Version | Tracking | Reason |
| --- | --- | --- | --- |
