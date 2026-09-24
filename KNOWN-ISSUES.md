# Known issues

This file records confirmed defects in the commercial Aspose SDKs that this CLI builds on.
Each entry was reproduced with a minimal SDK-only program. The CLI does not hide these
defects with default rewrites, file patches or version special cases (see [AGENTS.md](AGENTS.md)).
Each one is tracked upstream and blocks the release until it is fixed or waived below.

## Upstream SDK defects

### Aspose.Slides 26.9.0: a non-string value in the per-user font registry key breaks every Slides command

- **Symptom:** when the SDK initializes fonts, it enumerates
  `HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts` and casts every value to
  `string`. A value of another kind, such as a `REG_DWORD` written by third-party software,
  throws `InvalidCastException: Unable to cast object of type 'System.Int32' to type
  'System.String'` from an obfuscated method taking `(IDictionary, RegistryKey, String)`.
- **CLI behavior:** every Slides command that loads fonts fails with `FEATURE_UNSUPPORTED`,
  because the failure originates inside the SDK. The error does not name the registry value.
- **Minimal reproduction:** add a `REG_DWORD` value to that key, then in a .NET 10 console
  application that references only `Aspose.Slides.NET6.CrossPlatform` 26.9.0, open or
  create a presentation and render a slide.
- **Workaround for users:** remove the non-string value from that key. Windows itself
  writes only string values there.
- **Tracking:** to be filed upstream. No acceptance gate yet, because a reproduction has
  to write to the current user's registry.

### Aspose.Slides 26.9.0: chart layout and axis scale change on save (gate `SLD-003`)

Saving an unchanged PowerPoint chart moves its plot area and changes its automatic major
unit. See [tests/acceptance/slides-sdk-fidelity](tests/acceptance/slides-sdk-fidelity/README.md).

### Aspose.PDF.Drawing 26.8.0: moving pages loses bookmark destinations (gate `PDF-MOVE-BOOKMARK`)

After a page move, outline destinations point at `PageNumber=0`, and there is no public API
that retargets a destination without losing information. The CLI reports
the `NAVIGATION_DEGRADED` warning with the number of bookmarks, links and named
destinations that an edit or merge left unresolved. See
[tests/acceptance/pdf-page-navigation](tests/acceptance/pdf-page-navigation/README.md).

### Aspose.PDF.Drawing 26.8.0: HTML import requests network resources despite the custom loader (gate `PDF-HTML-EGRESS`)

The HTML importer requests every http(s) stylesheet and image before it calls
`HtmlLoadOptions.CustomLoaderOfExternalResources`, so the loader cannot prevent the request,
and the Markdown importer has no resource hook at all. The CLI refuses HTML and Markdown input
that names any network address, hyperlinks included, with `FEATURE_UNSUPPORTED`, and fails an
HTML import whose loader still sees one. The Markdown importer also reads local images outside
the input directory without the CLI's guard. See
[tests/acceptance/pdf-html-egress](tests/acceptance/pdf-html-egress/README.md).

### Aspose.Cells 26.9.0: sparklines cannot reference a sheet whose name contains an apostrophe

- **Symptom:** `SparklineGroups.Add` throws `Invalid "'"` whenever the data range is on a
  sheet whose name contains an apostrophe. This happens whether the name is quoted
  (`'O''Brien'!A1:A3`) or not, and even for an unqualified range on that sheet.
- **CLI behavior:** `add_sparkline` on such a sheet fails with `FEATURE_UNSUPPORTED`.
  Sheet names with other special characters work, because the CLI quotes them.
- **Tracking:** to be filed upstream.

## Release gate waivers

`scripts/acceptance.ps1` lets a failing acceptance gate ship only if it has a row in this
table for the version declared in `Directory.Build.props`.

| Gate | Version | Tracking | Reason |
| --- | --- | --- | --- |
