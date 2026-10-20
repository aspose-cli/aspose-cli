# PDF standards and conversion

## PDF/A

`pdf convert --to pdfa-1b|pdfa-2b|pdfa-3b` produces an archival candidate; then
run `pdf validate --profile` on that output and require `valid: true`, because a
completed validation can return `valid: false`. Name the profile with `--to`:
a `.pdf` output alone chooses none, so a convert to a `.pdf` `--out` without
`--to` is a `USAGE_ERROR` that lists the profiles.

```powershell
aspose-cli pdf convert report.pdf --to pdfa-2b --out report.archive.pdf --output-format json
aspose-cli pdf validate report.archive.pdf --profile pdfa-2b --output-format json
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
`application/octet-stream`. Declare the type when you attach a file, such as
`"mimeType": "image/png"` on `add_attachment`, so an archive can identify it. Each removed attachment is a `LOSSY_CONVERSION`
warning whose `location` names it, and removed bookmarks are one with
`location: bookmark/all`; a further `LOSSY_CONVERSION` warning counts the other
changes, which `pdf validate` on the original lists. With `--pages`, bookmarks
and links to pages left out lose their target, counted by `NAVIGATION_DEGRADED`.
Compare the candidate with the original
(`pdf inspect --detail outline --detail attachments`, and its pages) before delivery.
The command validates the file it converted. When the engine cannot make the
document conform, or the converted file does not validate, the command fails
with `PDFA_CONVERSION_FAILED`, writes no output and lists what it could not fix
in `error.details.problems`. Button fields the conversion leaves with
appearances the profile rejects, such as the radio groups of a form made with
`pdf create --from-html`, are named in `error.hint`: flatten them with a
`flatten_forms` edit, which ends their fillability, then convert again. PDF/A must
embed every font, so a font missing here fails the conversion; pass the
delivered fonts with `--font-dir`. `pdf validate` checks only the selected
profile, not signatures, redaction or permissions, and completes with exit 8 when
the file does not conform; `aspose-cli schema v2/pdf/validate-result` describes
each issue, including whether a conversion to the profile can fix it. `pdf inspect` reports only the profile a file
declares, as `pdf.pdfaProfile`; a declaration is not a check.

## Images to PDF

`pdf create --from-images` puts each image on its own page of `--page-size`,
turned to landscape for an image wider than it is tall. The image keeps its
aspect ratio: it fills the space inside `--margins` in one dimension and is
centred in the other. A photo's EXIF orientation counts, and an SVG without
both a width and a height takes the proportions of its viewBox. A raster image
enlarged to fewer than 96 pixels per inch warns `IMAGE_RESOLUTION_LOW` with the
page and the pixels it needs: it shows blurred.

## Other formats

`aspose-cli capabilities pdf` lists the PDF `convertFormats` and `renderFormats`.
`--pages` selects physical pages. Page images come from `pdf render`, which
writes PNG, JPEG or SVG, one file per selected page; `pdf convert` refuses those
formats. `pdf convert --to tiff` writes the selected pages as one multipage TIFF.
Read the result's `outputs` for the actual paths.

Conversions to document, text and HTML formats are structurally lossy, because
PDF has fixed pages; review the results. Raster outputs keep no selectable text
or interactive features, and SVG or XPS keep no editable structure, forms,
annotations or signatures.

HTML output is one self-contained file that places every line of text
absolutely and embeds subsets of the PDF's fonts; its `<title>` is the PDF
title. Only a browser lays it out as the pages look: `review` does not take it
(`FORMAT_MISMATCH`), and `review --product words` reflows it into one column of
plain lines and reports the embedded fonts as missing, so neither shows what a
reader sees. Review the source PDF instead, then open the HTML in a browser and
compare the pages.

Table extraction is best effort: each extracted table names the page it comes
from, and nothing scores how well it was recognized, so check the values against
the source. Tables are recognized by their ruling lines: when none is
recognized, `outputs` is empty and a `TABLES_NOT_FOUND` warning says so, which
happens to a table drawn without lines, such as one laid out from Markdown; read
it from `pdf extract --what text` or `review` instead. Each table is a UTF-8 CSV file without a byte order mark; add `--bom` when a person will open it in Excel, which otherwise reads
non-English text in the system code page.

The CLI does not linearize PDFs; `pdf inspect` reports an existing file's
`pdf.linearized` state.
