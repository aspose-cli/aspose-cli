---
name: aspose-cli-slides
description: Read, edit, convert and render PowerPoint presentations with the local Aspose CLI. Edits are batches of set, add, delete, move and do operations on the Aspose.Slides object model, each read back from the saved file.
---

# Aspose Slides

`aspose-cli slides` reads, edits and converts PPTX, PPTM, PPSX, PPSM, POTX, POTM, PPT, PPS, POT,
ODP, OTP and FODP through Aspose.Slides; `edit` writes the format it reads. The shared rules (session start, secrets, licensing,
errors) are in the Platform Skill. A deck exported to PDF is a PDF: use the PDF Skill for it.

| Command | What it does |
|---|---|
| `slides read` | Every object of the deck, or of one subtree, with every row of the object table, and the deck's inventory |
| `slides search` | Matches in the text of shapes, table cells, notes and chart titles, each with the object's address |
| `slides edit` | One batch of operations; writes the input's format; nothing is written when one fails |
| `slides convert`, `slides render` | The engine's save to another format; the engine's image of each slide |
| `slides extract --what media` | The pictures, audio and video the deck embeds, byte for byte |
| `slides create` | The engine's new presentation, or a copy of `--template` saved as a new file |

## The object table

`aspose-cli schema v2/slides/ops/directory` lists the vocabulary in one short JSON
document: each kind with its address forms, the rows a set writes (`rows`) and the rows that are
read only (`readOnlyRows`), and whether it can be deleted and moved; each add with the kinds it adds
to and its parameters; each action with its parameters. One entry in full, with each row's type and
the engine member it names: `aspose-cli schema v2/slides/ops/shape`,
`ops/add.slide`, `ops/do.replace_text`. A row names one engine member; setting it sets that member only, and whatever else the engine
changes with it is the engine's doing. Colors are `#RRGGBB`, lengths are points, enumeration values
are the engine's member names (`Solid`, `Title`, `ClusteredColumn`); a number the engine has not
set (NaN) reads as `null`.

Addresses use the engine's ids and positions, positions counted from 0:

| Address | Object |
|---|---|
| `presentation` | The deck: slide size, document properties |
| `slide/256` | A slide by its `slideId` |
| `slide/256/shape/3` | A shape by its `shapeId`, in a group or not |
| `slide/256/placeholder/Title`, `slide/256/placeholder/Body/1` | The shape whose placeholder type (and index) is that; none or several is an error |
| `…/paragraph/0`, `…/paragraph/0/run/1` | A paragraph of a shape, a cell or notes; a run of a paragraph |
| `slide/256/notes` | The slide's notes slide |
| `…/cell/1/2`, `…/row/1`, `…/column/2` | A table cell (row 1, column 2), row or column |
| `…/title`, `…/legend`, `…/axis/vertical`, `…/series/0`, `…/series/0/point/2`, `…/category/1`, `…/data/0/1/2` | Parts of a chart; `data/<worksheet>/<row>/<column>` is a cell of the chart's workbook |
| `section/<id>`, `master/<id>`, `layout/<id>`, `image/0` | Sections, masters, layouts, the deck's pictures |

## Reading

```powershell
aspose-cli slides read deck.pptx --kind slide --kind shape
aspose-cli slides read deck.pptx --at slide/257
```

- Each object comes with its `at` address, its `kind` and its `props`. Read `--kind slide --kind
  shape` first: a shape's `text` row holds all its text, paragraphs separated by `\r`; its
  paragraphs and runs repeat that text with their formatting, so read them (`--kind paragraph
  --kind run`) only to change formatting.
- To find where some text is, use `slides search`: each hit gives the address of the object that
  holds it.
- A window holds at most `--max-objects` objects (200 by default); when `window.truncated` is
  true, run the same command with `--from` set to `window.next.from`.
- `inventory.read` counts the objects read lists; `inventory.notRead` counts content the table
  does not address, such as comments, animations and SmartArt nodes.
- Layout ids for a new slide or a slide's `layout` row: `slides read deck.pptx --kind layout`.

## Editing

```json
{"ops": [
  {"set": "slide/256/placeholder/Title", "props": {"text": "2026 plan"}},
  {"add": "slide", "args": {"index": 1, "layout": 2147483650}},
  {"add": "autoShape", "in": "slide/256", "args": {"shapeType": "Rectangle", "x": 36, "y": 400, "width": 300, "height": 60}, "props": {"text": "Note"}},
  {"delete": "slide/258"},
  {"move": "slide/257", "to": 0},
  {"do": "replace_text", "args": {"oldText": "2025", "newText": "2026"}}
]}
```

```powershell
aspose-cli slides edit deck.pptx --ops ops.json --out deck.edited.pptx
```

- Operations run in order, each on what the ones before it left. When one fails, nothing is
  written, and the error names it: `details.index` (its position in `ops`), `details.at`.
- The result has one entry per operation, read from the saved file after it was opened again:
  `at` and `now` (every row of the object there); an action gives `returned`, the engine's return
  value. `counts` lists the inventory counts that changed. After a delete by id, `exists` is
  false. After a delete by position (`…/paragraph/2`), the entry reads that position: `exists:
  true` with the rows of the object that moved into it, `false` only when nothing is there. That
  is no failed delete: do not delete again, read the parent to see what is left.
- An add's entry gives the new object's address. Use it in the next batch; do not guess ids.
- A position in `at` is read after the whole batch: when a later operation inserts or deletes
  before it, `now` is whatever object is at that position then.
- Every argument of an add or an action is the engine call's parameter of that name; the ones not
  given are not passed. `ops/add.<name>` lists them.

Tasks, as the table does them:

- Title and body text: set `text` on `placeholder/Title` (`CenteredTitle` on a title slide) and on
  `placeholder/Body` or `placeholder/Object` (`read --kind shape` gives each shape's
  `placeholder.type` and `placeholder.index`).
- A table: `add table` with `columnWidths` and `rowHeights`, then set `text` on each
  `…/cell/<row>/<column>` in a second batch, at the address the add returned.
- Chart numbers: read the chart (`--at …/shape/<id>`); each point's `value.source` names its cell
  when it comes from the workbook; set `value` on that `…/data/<worksheet>/<row>/<column>`. A
  series' name is read only: set the cell its `name.source` names.
- Footers: set `footerVisible` (or `slideNumberVisible`, `dateTimeVisible`) first, then the text
  of `placeholder/Footer`. The engine shows a footer only where the slide's layout has the
  placeholder; making it visible copies the placeholder again, so text set before is lost.
- Speaker notes: `add notes` on a slide without them, then set `text` on `slide/<id>/notes`.

## Evaluation mode

Without a Slides license the result says `"license": {"mode": "evaluation"}`. The engine then
reads text longer than five characters as its first characters and a notice, and adds a watermark
text box to every slide of every file it saves; reads and counts show it as a shape.

## More

- [Engine behavior](references/engine-behavior.md): what `replace_text` covers, footers, chart
  data, positions; the engine's behaviors an edit runs into, which the CLI passes on unchanged.
- [Build a slide](examples/build-a-slide/README.md): a new deck, a text box, a table filled in a
  second batch, notes, and a render to look at.
