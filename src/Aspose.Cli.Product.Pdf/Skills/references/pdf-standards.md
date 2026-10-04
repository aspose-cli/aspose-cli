# PDF standards and conversion

## PDF/A

`pdf convert --to pdfa-1b|pdfa-2b|pdfa-3b` produces an archival candidate; then
run `pdf validate --profile` on that output and require `valid: true`, because a
completed validation can return `valid: false`.

```powershell
aspose-cli pdf convert report.pdf --to pdfa-2b --out report.archive.pdf --output json
aspose-cli pdf validate report.archive.pdf --profile pdfa-2b --output json
```

Conversion keeps the document's parts the profile allows and changes or removes
the rest:

| Content | pdfa-1b | pdfa-2b | pdfa-3b |
| --- | --- | --- | --- |
| Bookmarks, document title, page labels | kept | kept | kept |
| Attachments that are PDF/A documents | removed | kept | kept |
| Other attachments | removed | removed | kept |

PDF/A-3 needs a media type on every attachment: one the source declares is kept,
an untyped PDF becomes `application/pdf` and any other untyped attachment
`application/octet-stream`. Each removed attachment is a `LOSSY_CONVERSION`
warning whose `location` names it, and a removed bookmark is one with
`location: outline`; a further `LOSSY_CONVERSION` warning counts the other
changes, which `pdf validate` on the original lists. With `--pages`, bookmarks
and links to pages left out lose their target, counted by `NAVIGATION_DEGRADED`.
Compare the candidate with the original (`pdf inspect --detail outline
attachments`, and its pages) before delivery.
The command validates the file it converted. When the engine cannot make the
document conform, or the converted file does not validate, the command fails
with `PDFA_CONVERSION_FAILED`, writes no output and lists what it could not fix
in `error.details.problems`. Button fields the conversion leaves with
appearances the profile rejects, such as the radio groups of a form made with
`pdf create --from-html`, are named in `error.hint`: flatten them with a
`flatten_forms` edit, which ends their fillability, then convert again. PDF/A must
embed every font, so a font missing here fails the conversion; pass the
delivered fonts with `--font-dir`. `pdf validate` checks only the selected
profile, not signatures, redaction or permissions. Each `issues` entry reads
`clause (severity, page N): message`, and a `LIST_TRUNCATED` warning gives the
total when the list is capped. `pdf inspect` reports only the profile a file
declares, as `pdf.pdfaProfile`; a declaration is not a check.

## Other formats

`aspose-cli capabilities` lists the PDF `convertFormats` and `renderFormats`.
`--pages` selects physical pages. PNG, JPEG and SVG exports write one file per
selected page and TIFF one multipage file; read the result's `outputs` for the
actual paths. `pdf render` takes the format from `--to`, or from the `--out`
extension when `--to` is omitted, and refuses a `--to` that disagrees with the
extension; use `pdf convert --to tiff` for TIFF.

Conversions to document, text and HTML formats are structurally lossy, because
PDF has fixed pages; review the results. Raster outputs keep no selectable text
or interactive features, and SVG or XPS keep no editable structure, forms,
annotations or signatures.

Table extraction is best effort. Extracted tables carry page and rectangle
context, but `confidence` is a fixed 0.5, not a calibrated score; check the
values against the source. Each table is a UTF-8 CSV file without a byte order
mark; add `--bom` when a person will open it in Excel, which otherwise reads
non-English text in the system code page.

The CLI does not linearize PDFs; `pdf inspect` reports an existing file's
`pdf.linearized` state.
