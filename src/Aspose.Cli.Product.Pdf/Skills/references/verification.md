# PDF verification

Follow the delivery checklist and review protocol in `aspose-cli docs verification`.
Every `pdf edit` reopens its output before publishing it
(`mutation.verification: "reopened"`); this page adds the PDF evidence to collect.

## Content

```powershell
aspose-cli pdf inspect report.final.pdf --detail metadata permissions forms signatures attachments outline fonts --output json
aspose-cli pdf query pages report.final.pdf --pages 1-5 --mode layout --output json
aspose-cli pdf query search report.final.pdf --pattern DRAFT --output json
```

Choose page ranges from the inspected page count and compare the text with the
expected content. Search is not OCR.

For redaction, search every relevant page, inspect the redacted regions in the
review images, and check images, annotations, metadata and attachments as
needed. Search provides no redaction certification.

After `move_pages`, `delete_pages` or `pdf merge`, check each affected bookmark,
local link and named destination against the original target, including
destination type, coordinates and zoom. A `NAVIGATION_DEGRADED` warning counts
the entries left without their exact target; its absence covers only the page
each entry reaches, not its location or zoom.

For attachments, compare each extracted file with the original bytes.

## Standards and signatures

Require `valid: true` from `pdf validate` for archival output
([PDF/A and conversion](pdf-standards.md)) and for every signature in
`pdf inspect --detail signatures` ([forms and security](forms-security.md)).

## Appearance

```powershell
aspose-cli review report.final.pdf --out report.review-1 --output json
```

Check page order and size, crop and rotation, clipping, images, tables, headers
and footers, stamps, form field appearances, annotations, redaction appearance
and contrast. PDF review findings worth acting on:

- `PDF_FONTS_NOT_EMBEDDED`: a font the PDF uses without embedding it is drawn
  from the fonts installed here. Pass the delivered fonts with the same
  `--font-dir` to `fonts check`, `review` and `pdf create`, `render`, `convert`,
  `edit` and `sign`; HTML and text sources resolve CSS `font-family` from it too.
  Text operations that name a `font`, and a visible signature, need that font here.
- `PDF_FORM_APPEARANCE_REVIEW_REQUIRED`: open the pages with fields and look for
  stale, clipped or missing values.
- `PDF_PAGE_WITHOUT_READABLE_CONTENT`: a page without text that was not
  identified as scanned; look at it, it may be unintentionally blank. A blank or
  image-only page also has no font resources, so `fonts check` returns an empty
  `fonts` array for it.

```powershell
aspose-cli fonts check report.pdf --font-dir fonts --output json
```
