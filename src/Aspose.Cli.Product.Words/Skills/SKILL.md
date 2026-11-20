---
name: aspose-cli-words
description: Read, edit, convert, compare and render Word documents with the local Aspose CLI. Edits are batches of set, add, delete and do operations on the Aspose.Words object model, each read back from the saved file.
---

# Aspose Words

`aspose-cli words` reads, edits and converts DOC, DOT, DOCX, DOCM, DOTX, DOTM, Flat OPC, RTF,
WordML, ODT and OTT through Aspose.Words; `edit` writes the format it reads. HTML, MHTML, plain
text, Markdown, PDF, EPUB, MOBI, AZW3 and CHM are read only by `convert`. The shared rules
(session start, secrets, licensing, errors) are in the Platform Skill.

| Command | What it does |
|---|---|
| `words read` | Every object of the document, or of one subtree, with every row of the object table, and the document's inventory |
| `words search` | Matches in the paragraphs' text, each with the paragraph's address |
| `words edit` | One batch of operations; writes the input's format; nothing is written when one fails |
| `words convert` | The engine's save to another format: PDF, XPS, HTML, Markdown, text, EPUB, another document format; text, Markdown, HTML or PDF to a document |
| `words render` | The engine's image of each page |
| `words compare` | `Document.Compare` of a changed document with the original, written as a redline |
| `words extract --what images` | The bytes of each image the document stores, one file each with the engine's `imageType`; the engine reports an SVG picture as `Unknown`, and a file of a type it does not know gets no extension |
| `words create` | The engine's new document, or a copy of `--template` saved as a new file |

## The object table

`aspose-cli schema v2/words/ops --operation directory` lists the vocabulary in one short JSON
document: each kind with its address forms, the rows a set writes (`rows`) and the rows that are
read only (`readOnlyRows`), and whether it can be deleted; each add with the kinds it adds to and
its parameters; each action with its parameters. One entry in full, with each row's type and the
engine member it names: `--operation paragraph`, `--operation add.paragraph`, `--operation
do.replace`. A row names one engine member; setting it sets that member only, and whatever else the
engine changes with it is the engine's doing. Colors are `#RRGGBB`, lengths are points,
enumeration values are the engine's member names (`Center`, `Landscape`, `FooterPrimary`).

| Address | Object |
|---|---|
| `document`, `layout` | The document; its page layout, whose `pageCount` makes the engine lay the document out |
| `section/0`, `section/0/body`, `section/0/headerFooter/FooterPrimary` | A section; its body; a header or footer by its type |
| `section/0/body/paragraph/3`, `…/paragraph/3/run/0` | A paragraph of the body, counted among its paragraphs; a run of it |
| `section/0/body/table/0/row/1/cell/2/paragraph/0` | A table, a row, a cell, a paragraph of the cell |
| `…/paragraph/3/field/0`, `…/paragraph/3/shape/0` | A field that starts in the paragraph; a picture or text box in it |
| `…/sdt/0` | A content control |
| `comment/0`, `footnote/0` | A comment, a footnote or endnote, in document order; their paragraphs are below them |
| `bookmark/Price`, `style/Heading 1`, `list/1/level/0`, `customProperty/Client`, `revision/0` | By name, by ListId, by position |
| `…/range` | The text of a story, cell or the whole document, as one string |

Every position counts from 0 among the parent's objects of that kind. A paragraph and a table of
the same body are counted apart: `paragraph/7` can come after `table/0`. A form such as
`{paragraph}/run/<index>` continues the address of a paragraph, whatever story holds it.

## Reading

```powershell
aspose-cli words read contract.docx
aspose-cli words read contract.docx --at section/0/body/range
aspose-cli words read contract.docx --kind paragraph --kind table --max-objects 25
aspose-cli words search contract.docx --pattern "Contoso"
```

- Each object comes with its `at` address, its `kind` and its `props`.
- A read without `--kind` lists the document, sections, headers and footers, bodies, tables,
  content controls, comments, footnotes, lists and custom properties, but no paragraph, run,
  field, shape, row, cell, bookmark, style, list level or revision: `window.notListedUnlessNamed`
  says which kinds it left out. Name them with `--kind`.
- The cheapest way to read the text is a range: `--at section/0/body/range` is one object whose
  `text` is the body's text. `words search` gives the address of each paragraph that holds a
  match; read or edit at it.
- A paragraph is about 1,200 characters with all its rows; `--max-objects 25` keeps a window
  under 30,000. When `window.truncated` is true, run the same command with `--from` set to
  `window.next.from`. read lists objects in the document's order: a table among the paragraphs
  around it.
- Text is as the engine gives it: `\r` ends a paragraph, `\a` ends a cell, `\f` a section or page,
  `\u0013`, `\u0014`, `\u0015` surround a field's code and result. While changes are tracked, a
  paragraph's `text` holds its deleted text too; each run says whether it is an insertion or a
  deletion.
- The engine's collections hold a content control's content as its parent's own: read lists the
  control, then its paragraphs, rows, cells or runs among its parent's. The same content can also
  be named by its position in the control: `section/0/body/sdt/0/paragraph/1`, which an add in the
  control reports, and the `section/0/body/paragraph/3` that read lists are one paragraph. A
  content control in another is listed below it, `…/sdt/0/sdt/0`. `…/sdt/0/range` reads what it
  holds.
- A paragraph lists the fields that start among its own children; a field in a footnote, comment
  or text box anchored in it is listed below that story's paragraph.
- A field's `result`, a paragraph's `listLabel.labelString` and the document's word count are as
  the engine last updated them: the `update_fields`, `update_list_labels`, `update_word_count`
  and `update_page_layout` actions update them; opening and saving do not.
- `inventory.read` counts the objects of the kinds the table addresses; `inventory.notRead`
  counts content it does not, such as group shapes, charts, equations and form fields.

## Editing

```json
{"ops": [
  {"set": "section/0/body/paragraph/0/run/0", "props": {"text": "Proposal 2026"}},
  {"add": "paragraph", "in": "section/0/body", "args": {"before": "section/0/body/paragraph/7"}, "props": {"paragraphFormat.styleName": "Heading 1"}},
  {"add": "run", "in": "section/0/body/paragraph/7", "args": {"text": "Risks"}},
  {"add": "run", "in": "section/0/body/table/0/row/1/cell/2/paragraph/0", "args": {"text": "Wang Fang"}},
  {"do": "replace", "args": {"pattern": "Contoso", "replacement": "Fabrikam"}}
]}
```

```powershell
aspose-cli words edit proposal.docx --ops ops.json --out proposal.edited.docx
```

- Operations run in order, each on what the ones before it left: after an add before
  `paragraph/7`, the new paragraph is `paragraph/7` and the old one `paragraph/8`. When one fails,
  nothing is written, and the error names it: `details.index` (its position in `ops`),
  `details.at`.
- The result has one entry per operation, read from the saved file after it was opened again:
  `at` and `now` (every row of the object there); an action gives `returned`, the engine's return
  value, and `at` when it names an object. `counts` lists the inventory counts that changed. A
  position in `at` is read after the whole batch.
- Every argument of an add or an action is the engine call's parameter of that name; the ones not
  given are not passed. An add's `before` is the sibling the new object goes before; without it,
  it goes last.

Tasks, as the table does them:

- Text: a paragraph's text is in its runs. Set a run's `text`; a paragraph of several runs keeps
  the others, so delete the ones you do not want. An empty paragraph, such as the one of an empty
  table cell, has no run: `add run` into it. `replace` changes every match the engine finds in the
  whole document, or in the object `node` names; the engine may split runs to do it, which
  `counts.runs` shows.
- A heading or paragraph: `add paragraph` with `before`, `paragraphFormat.styleName` in its
  `props`, then `add run` in it.
- A table: `add table`, `add row` in it, `add cell` in each row, `add paragraph` in each cell. The
  engine does not keep a table without rows, or a row without cells, when it saves; it then
  leaves an empty paragraph where the table was. To add a row like an existing one,
  `{"do": "clone", "args": {"node": "…/table/0/row/1", "parent": "…/table/0", "isCloneChildren": true}}`
  copies it with its formatting and text.
- A page number: `add headerFooter` of type `FooterPrimary` in the section, `add paragraph` in it,
  `add field` with `fieldCode` `PAGE`. A section without a header or footer of a type shows the
  previous section's.
- A content control: an add `in` it adds to its content. `delete` removes the control with its
  content; `remove_self_only` keeps the content.
- Tracked changes: `start_track_revisions` records the operations after it as revisions; without
  `dateTime` they take the time they are made, so two runs of the same batch differ.
  `accept_revision`, `reject_revision` and the `_all_` forms accept and reject them.
- Merging cells: set `cellFormat.verticalMerge` to `First` on one cell and `Previous` on the cells
  below it. The engine saves a horizontal merge as one wider cell.

## Converting, comparing and rendering

```powershell
aspose-cli words convert contract.docx --out contract.pdf
aspose-cli words convert notes.md --to docx --encoding utf-8
aspose-cli words convert notes.txt --to docx --encoding utf-8 --auto-numbering-detection false --detect-numbering-with-whitespaces false --detect-hyperlinks false --leading-spaces Preserve
aspose-cli words compare original.docx changed.docx --author "Legal Review" --date-time 2026-03-01T09:00:00 --out redline.docx
aspose-cli words render contract.docx --pages 1-2 --out page.png
```

- A plain text input is read with the five options the engine would otherwise detect; Markdown
  with `--encoding`. Every output option not given is the engine's default.
- An HTML, html-fixed or Markdown output is one file that holds its images.
- `compare` takes the engine's author and date and time of the revisions; the result gives
  `revisionCount`, the redline's revision count.
- `render` draws every page, or the ones `--pages` names, from 1.

## Evaluation mode

Without a Words license the result says `"license": {"mode": "evaluation"}`. The engine then adds
a paragraph of notice at the start of every document it opens and a line to its headers and
footers, which reads list and counts include: the body's paragraphs are each one position later
than with a license. It cuts a long document short. It does not save a document while
`start_track_revisions` is in effect (`ENGINE_ERROR`); a batch that ends with
`stop_track_revisions` keeps its revisions and saves.

## More

- [Engine behavior](references/engine-behavior.md): what the engine does with text, tables,
  fields, styles and conversions, which the CLI passes on unchanged.
- [Fill a table](examples/fill-a-table/README.md): a new document, a table built and filled, a
  row cloned, and a PDF to look at.
