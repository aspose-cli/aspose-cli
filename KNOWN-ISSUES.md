# Known issues

Confirmed defects in the commercial Aspose SDKs this CLI runs on. Each is reproduced with a
minimal SDK-only program by the acceptance gate of the same id, and the code that handles it
names that id in a comment. The CLI does not hide them ([AGENTS.md](AGENTS.md)). A gate that
still reproduces its defect lets a release proceed; one that no longer does blocks it until the
issue, its handling and its gate are deleted ([CONTRIBUTING.md](CONTRIBUTING.md#acceptance-gates)).

## Aspose.Cells 26.9.0

### CELLS-SVG-EGRESS

- **Defect:** adding an SVG picture requests the images its `xlink:href` attributes name while
  the engine rasterizes the picture's fallback image; `WorkbookSettings.ResourceProvider` is
  never consulted.
- **CLI behavior:** `insert_image` refuses an SVG that names a network address, and a compressed
  SVG, with `FEATURE_UNSUPPORTED`.
- **Workaround:** embed the SVG's images as `data:` URIs, or insert a raster image.
- **Reproduction:** [tests/acceptance/cells-svg-egress](tests/acceptance/cells-svg-egress/README.md)

### CELLS-SPARKLINE-APOSTROPHE

- **Defect:** `SparklineGroups.Add(type, dataRange, isVertical, locationRange)` throws
  `Invalid "'"` for a data range on a sheet whose name has an apostrophe, however it is quoted.
- **CLI behavior:** none visible; `add_sparkline` builds the group from its parts, with the same
  result.
- **Reproduction:** [tests/acceptance/cells-sparkline-apostrophe](tests/acceptance/cells-sparkline-apostrophe/README.md)

## Aspose.PDF.Drawing 26.8.0

### PDF-HTML-EGRESS

- **Defect:** the HTML importer requests http(s) stylesheets and images, and runs script, before
  it calls `HtmlLoadOptions.CustomLoaderOfExternalResources`. The Markdown importer and SVG images
  (`Image.File`, `ImageStamp`) have no resource hook: they fetch remote resources and read any
  local file a reference names, and Markdown resolves relative paths against the process working
  directory.
- **CLI behavior:** HTML, Markdown and SVG input that names a network address or contains script
  is refused with `FEATURE_UNSUPPORTED` before the engine reads it, and Markdown also unless every
  file it references is an ordinary file beneath its directory. For trusted HTML,
  `pdf create --from-html --allow-network-resources` lets the importer fetch and lists every
  address in `NETWORK_RESOURCES_REQUESTED`.
- **Workaround:** keep resources beside the input and reference them by relative path, or
  convert HTML with `aspose-cli words convert`, which makes no network request.
- **Reproduction:** [tests/acceptance/pdf-html-egress](tests/acceptance/pdf-html-egress/README.md)

### PDF-MOVE-BOOKMARK

- **Defect:** destination getters read an omitted (null) coordinate as 0 and no public API writes
  one (`XYZExplicitDestination` with `NaN` writes an invalid `NaN` token), so a destination that
  named a moved page cannot always be rebuilt exactly; the SDK has no page move.
- **CLI behavior:** `move_pages` retargets navigation to and from the moved pages exactly, except
  a destination with a coordinate of 0, which it leaves without a target and counts in
  `NAVIGATION_DEGRADED`.
- **Workaround:** re-create the counted bookmarks and links with `add_bookmark` and `add_link`.
- **Reproduction:** [tests/acceptance/pdf-page-navigation](tests/acceptance/pdf-page-navigation/README.md)

## Aspose.Slides.NET6.CrossPlatform 26.9.0

### SLIDES-CHART-TITLE

- **Defect:** loading a chart whose automatic title is implicit (no `c:title`,
  `autoTitleDeleted` 0) reports a title that overlays the plot, and saving writes it, so the plot
  area grows and its automatic axis scale can change.
- **CLI behavior:** every edit, conversion and rendering of such a chart warns
  `CHART_TITLE_OVERLAID` with its slide.
- **Workaround:** inspect those charts in PowerPoint and restore their title layout there.
- **Reproduction:** [tests/acceptance/slides-sdk-fidelity](tests/acceptance/slides-sdk-fidelity/README.md)

### SLIDES-FONT-REGISTRY

- **Defect:** font initialization casts every value under
  `HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts` to `string`, so a `REG_DWORD` or
  `REG_BINARY` value written by other software throws `InvalidCastException`.
- **CLI behavior:** a Slides command that uses fonts fails with `FEATURE_UNSUPPORTED`, naming each
  such value, before the engine starts.
- **Workaround:** remove the named values; Windows writes only string values there.
- **Reproduction:** [tests/acceptance/slides-font-registry](tests/acceptance/slides-font-registry/README.md)
