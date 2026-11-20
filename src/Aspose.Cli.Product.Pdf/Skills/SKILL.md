---
name: aspose-cli-pdf
description: Read, edit, fill, stamp, redact, encrypt, sign, merge, split, convert, validate and render PDF documents with the local Aspose CLI. Edits are batches of set, add, delete and do operations on the Aspose.PDF object model, each read back from the saved file.
---

# Aspose PDF

`aspose-cli pdf` reads and edits PDF through Aspose.PDF; `edit` writes PDF. An HTML page is read
only by `convert`, which imports it; Markdown is read by no PDF command (`words convert` reads it).
The shared rules (session start, secrets, licensing, errors) are in the Platform Skill.

**Positions are the engine's, and they count from 1**: `page/1` is the first page, as are
`annotation/1`, `font/1`, `bookmark/1` and `attachment/1`. Only a page label counts from 0: it is
keyed by the engine's page index, so `pageLabel/0` starts at `page/1`. Rectangles are the engine's
`Rectangle`, written `LLX,LLY,URX,URY` in points, from the page's lower-left corner before any
rotation.

| Command | What it does |
|---|---|
| `pdf read` | Objects of the document, or of one subtree, with every row of the object table, and the document's inventory |
| `pdf search` | Matches the engine's `TextFragmentAbsorber` finds in the pages' text, each with the page's text address and the match's rectangle |
| `pdf edit` | One batch of operations; nothing is written when one fails |
| `pdf convert` | The engine's save to DOCX, XLSX, PPTX, HTML, EPUB, Markdown, XPS, text, TIFF, PDF or PDF/A; an HTML page to PDF |
| `pdf render` | The engine's image of each page |
| `pdf merge`, `pdf split` | `PdfFileEditor.Concatenate` of inputs; parts of page ranges, each `Pages.Add` of its pages into a new document |
| `pdf sign` | `PdfFileSignature.Sign` with the page, rectangle and texts you state |
| `pdf validate` | `Document.Validate` against a PDF/A profile: its return value and its log |
| `pdf extract` | Images, attachments, tables (CSV) or form data, one file each |
| `pdf create` | The engine's new document, which has no page, or a copy of `--template` |

## The object table

`aspose-cli schema v2/pdf/ops/directory` lists the vocabulary in one short JSON
document: each kind with its address forms, the rows a set writes (`rows`) and the rows that are
read only (`readOnlyRows`), and whether it can be deleted; each add with the kinds it adds to and
its parameters; each action with its parameters. One entry in full, with each row's type and the
engine member it names: `ops/field`, `ops/add.page`, `ops/do.stamp_text`.
A row names one engine member; setting it sets that member only, and whatever else the engine
changes with it is the engine's doing.

| Address | Object |
|---|---|
| `document` | The document: version, encryption, the document information's title, author and dates |
| `page/1` | A page: rotation, media box, crop box |
| `page/1/text`, `page/1/area/150,700,258,713.2` | A page's text; the text in a rectangle of it |
| `page/1/annotation/2`, `page/1/font/1`, `page/1/layer/oc1` | An annotation of any type, links and form widgets included; a font of the page; a layer by its id |
| `bookmark/1`, `bookmark/1/bookmark/2` | A bookmark and its children |
| `field/applicant`, `field/form.person.name` | A form field by its full name, a hierarchical one's with its dots; a radio group is one field |
| `attachment/1`, `pageLabel/0`, `info/Department`, `xmp/dc:title` | An embedded file; a page label by page index; a custom information entry; an XMP entry |
| `taggedContent` | The structure tree, read only by `--at` |

In a name with a `/`, write `\/` (and `\\` for a `\`): `info/dept\/team`.

## Reading

```powershell
aspose-cli pdf read form.pdf
aspose-cli pdf read form.pdf --kind field
aspose-cli pdf read report.pdf --at page/3/text
aspose-cli pdf read report.pdf --kind text --max-objects 20
aspose-cli pdf search form.pdf --pattern "110101199003074518"
```

- Each object comes with its `at` address, its `kind` and its `props`.
- A read without `--kind` lists the document, attachments, page labels, information and XMP
  entries; pages, page text, annotations, fonts, layers, bookmarks and fields are listed only when
  `--kind` names them, which `window.notListedUnlessNamed` says. `inventory.read.pages` is the
  page count.
- A page's text is `TextAbsorber.Text` with the engine's defaults; `--kind text` reads every
  page's. A form field's value is no page text: read it at `field/…`.
- A form field's controls on the pages are annotations of type `Widget`, with `fullName` naming
  the field; their rows are the control's own (rectangle, color). The value is the field's.
- When `window.truncated` is true, run the same command with `--from` set to `window.next.from`.

## Editing

```json
{"ops": [
  {"set": "field/applicant", "props": {"value": "Li Lei"}},
  {"set": "field/approved", "props": {"checked": true}},
  {"delete": "page/2"},
  {"do": "stamp_text", "args": {"page": "page/1", "value": "- 1 -", "horizontalAlignment": "Center", "verticalAlignment": "Bottom"}},
  {"do": "redact_area", "args": {"page": "page/1", "rect": "150,700,258,713.2"}}
]}
```

```powershell
aspose-cli pdf edit form.pdf --ops ops.json --out form.edited.pdf
```

- Operations run in order, each on what the ones before it left: after `delete page/2`, the old
  `page/3` is `page/2`. When one fails, nothing is written, and the error names it:
  `details.index` and `details.at`.
- The result has one entry per operation, read from the saved file after it was opened again:
  `at` and `now`; an action gives `returned` and the `at` it names. `counts` lists the inventory
  counts that changed. A position in `at` is read after the whole batch.
- Every argument of an add or an action is the engine call's parameter of that name; the ones not
  given are not passed, so the engine's defaults hold (a stamp: no alignment, margins 0,
  Helvetica 10, black).

Tasks, as the table does them:

- Fill a form: `set field/<name>` with `value`; a check box takes `checked` or one of its
  `allowedStates` as `value`, a radio group one of its `options`' names. The engine takes any
  text as a value.
- Reorder pages: there is no move. Copy the page where it should go, then delete the original:
  `{"add": "page", "args": {"pageNumber": 1, "page": "page/4"}}`, then `{"delete": "page/5"}`. A
  link that led to the deleted page then leads nowhere; bookmarks follow the engine's renumbering.
- Page numbers, headers, watermarks: one `stamp_text` per page, with the text you want on it.
- Redact text: `pdf search` gives each match's `rect`; one `redact_area` per rectangle. Its
  read-back is the text the engine still finds there; search the output again to see what is
  left. On a rotated page the engine's rectangle is in the unrotated page's space.
- Encrypt: the `encrypt` action with the passwords' environment variables, the `Permissions` it
  grants and the `CryptoAlgorithm`; `decrypt` removes it. `--encrypt` puts one password on the
  output instead; it cannot be given with these actions.
- Pages of another PDF: `import_pages` with its `file` and page numbers.

## Converting and other commands

```powershell
aspose-cli pdf convert report.pdf --out report.docx
aspose-cli pdf convert page.html --out page.pdf
aspose-cli pdf convert report.pdf --to pdfa-2b --convert-error-action Delete --out archive.pdf
aspose-cli pdf render report.pdf --pages 1-2 --out page.png
aspose-cli pdf merge cover.pdf body.pdf --out report.pdf
aspose-cli pdf split report.pdf --by pages --groups 1-3 --groups 4-6 --out-dir parts
aspose-cli pdf sign contract.pdf --page 1 --certificate signer.pfx --certificate-password-env PFX_PASSWORD --visible --rect 36,36,180,60
```

- An HTML page reads only the files beneath its folder; a network address is blocked and listed
  in `warnings`. The engine fails the import of a page whose `@font-face` font or `<object>` data
  is on the network or outside the folder; the error's `details.notSupplied` lists each reference
  that got nothing, with why.
- `--pages` applies to html and tiff outputs, whose save selects pages; other outputs hold the
  whole document.
- A Markdown output links each picture as a file the engine writes beside it; `outputs` lists
  them after the `.md`.
- A PDF/A output is `Document.Convert` with the `--convert-error-action` you state, then a save;
  the result holds what it returned (`converted`) and its log. `validate` gives the engine's
  `valid` and log; read them yourself.
- `sign --rect` is the engine's `System.Drawing.Rectangle`: `x,y,width,height` in whole points,
  `x,y` the lower-left corner.

## Evaluation mode

Without a PDF license the result says `"license": {"mode": "evaluation"}`. The engine then gives
only the first four objects of any collection: read lists four pages while `pages` counts all
of them, and an address past the fourth, such as `page/5`, is the engine's error. It prints a line
of notice on every page it saves and adds XMP entries to the file.

## More

- [Engine behavior](references/engine-behavior.md): what the engine does with pages, forms,
  annotations, redaction and conversions, which the CLI passes on unchanged.
- [Fill and stamp a report](examples/fill-and-stamp/README.md): HTML pages converted, merged,
  their form filled and their pages stamped.
