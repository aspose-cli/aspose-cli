---
name: aspose-cli-platform
description: Start here for any document task with the local Aspose CLI (aspose-cli) - the session check, choosing the Cells, PDF, Slides or Words Skill, and what every product shares - addresses, the five operation shapes, read-back, windows, errors, the license marker and how to ask schema.
---

# Aspose CLI platform

`aspose-cli` reads, edits, converts and renders spreadsheets, PDF documents,
presentations and word-processing documents on the local machine with the
Aspose engines. Every command is `aspose-cli <product> <command>`, and every
result is JSON. This Skill holds what the four products share; each product
Skill holds its own objects, rows and actions.

## Session start

```powershell
aspose-cli --version --output-format json
aspose-cli license status --output-format json
aspose-cli schema
```

- `license status` says which products run licensed (`references/licensing.md`).
- `schema` lists the products with their commands and formats, the platform
  commands, and the id of every document `aspose-cli schema <id>` prints.

| Document | Product Skill |
|----------|---------------|
| Workbooks: XLSX, XLSM, XLSB, XLS; CSV and TSV to convert | `aspose-cli-cells` |
| PDF documents | `aspose-cli-pdf` |
| Presentations: PPTX, PPT, ODP | `aspose-cli-slides` |
| Word processing: DOCX, DOC, RTF, ODT; Markdown, HTML and text to convert | `aspose-cli-words` |

A command refuses, before it reads the file, an extension that names a format
it does not read, such as `.xlsx` given to `pdf read`: `FORMAT_UNSUPPORTED`,
whose message lists the extensions the command reads. A file without an
extension, or with one no format names, goes to the engine.
`aspose-cli schema v3/<product>/formats` marks `convert-input` the formats
only `convert` reads.

## The object table

Each product names the engine's objects and their members in one table:

- **An address** names one object: the document itself (`workbook` in Cells,
  `document` in PDF and Words, `presentation` in Slides), or segments and keys
  from it down, such as `sheet/Sales/cell/B2`, `page/1/annotation/2` or
  `slide/256/shape/3`. `read` gives every object's address in `at`. A key is an
  ordinal from 1, an index from 0, an id or a name, as each kind's address forms
  in the table say; in a name, `/` is written `\/` and `\` is written `\\`.
- **A kind** is the type of an object, such as `cell` or `paragraph`.
- **A row** is one member of the engine's object under one name, such as
  `font.isBold`. A value is written in one form: a color as `#RRGGBB`, a length
  in points unless the row names another unit, an enumeration as the engine's
  member name.

`read` returns each object with every row of its kind, as the engine reads it.
A row a read returns is set under the same name.

## Five operation shapes

`<product> edit <file> --ops <file>` applies one JSON document,
`{"ops": [...]}`, in order. Each entry is one of:

| Shape | Does |
|-------|------|
| `{"set": "<address>", "props": {...}}` | Sets rows of one object |
| `{"add": "<kind>", "in": "<address>", "args": {...}, "props": {...}}` | Makes the engine call that creates one object, with the arguments its add takes, then sets rows of it; `in` is the address of the parent, in every add |
| `{"delete": "<address>"}` | Deletes one object |
| `{"move": "<address>", "to": <position>}` | Moves one object among its siblings, to the engine's position from 0, where its kind allows it |
| `{"do": "<action>", "args": {...}}` | Makes one engine call, with every argument stated |

- An address that names no object, or more than one, is an error. A parameter
  that is not given is not set: the engine's default applies.
- When any operation fails, nothing is written (`references/editing.md`).
- The result reports each operation as it reads it back from the saved file,
  opened again: its address (`at`) and its rows `now`, `now: null` for a
  deleted object, and an action's `returned` value. `counts` lists the
  inventory counts that changed, before and after.

`aspose-cli schema v3/<product>/ops/directory` lists the product's kinds with
their address forms and rows, its adds and its actions. A name it lists is
printed in full with `aspose-cli schema v3/<product>/ops/<name>`, such as
`ops/cell`, `ops/add.chart` or `ops/do.sort`.

## Reading

`<product> read <file>` lists the objects with their rows and an inventory:
how many objects of each kind the file holds, and which kinds it lists only
when `--kind` names them (`window.notListedUnlessNamed`). `--at <address>` reads
one object and what it holds; `--kind` names the kinds to list. A read gives
one window at a time; `window.next` holds the options of the next one
(`references/reading.md`). `<product> search` returns the address and the row
each match is in.

## Results and errors

- A result goes to stdout; `--output-format compact` writes the same JSON on
  one line. A result names its `schema`, an id for `aspose-cli schema <id>`.
- An error is one JSON envelope on stderr with a stable `code`, a `message` and
  `details`, and a non-zero exit code; nothing is written
  (`references/troubleshooting.md`). A name that does not exist lists the
  names that do in `details.available`.
- Exit 0 means the engine did what was named and the file was saved and
  opened again; it does not judge the result.
- `warnings` carries what the engine reported while it worked.
- A run without a license carries `license` with its `mode` in the result;
  a licensed result has no `license` (`references/licensing.md`).

## Outputs and secrets

- An edit writes `--out <path>`, replaces its input with `--in-place`, or
  writes `<name>.out.<ext>` beside it. An existing output is replaced only with
  `--overwrite`.
- A file a command writes is written to a temporary file and moved into place;
  when the command fails, nothing is written.
- Pass passwords with `--password-env`, `--encrypt-env`, `--password-stdin`
  or an operation's `*Env` argument, which name where the secret is, never the
  secret.

## Ask the CLI

| Question | Ask |
|----------|-----|
| A command's options, values and defaults | `aspose-cli <product> <command> --help`, `aspose-cli schema v3/<product>/<command>-command` |
| What a result holds | `aspose-cli schema v3/<product>/<command>-result` |
| The kinds, rows, adds and actions | `aspose-cli schema v3/<product>/ops/directory`, then `ops/<name>` |
| The formats a product reads and writes, with their extensions | `aspose-cli schema v3/<product>/formats` |
| Every error and warning code, with its exit code | `aspose-cli schema v3/common/diagnostics` |
| What an error's `details` keys mean | `aspose-cli schema v3/common/error-payload` |
| The resource budgets | `aspose-cli schema v3/common/budgets` |

## References

| File | Holds |
|------|-------|
| `references/reading.md` | Windows and `window.next`, search hits, rendering, fonts, budgets |
| `references/editing.md` | The ops document, atomic batches, the read-back, outputs and encryption |
| `references/troubleshooting.md` | The error envelope, exit codes, common errors, Windows PowerShell |
| `references/licensing.md` | License sources, status, install, the evaluation marker |

`aspose-cli skill list` names every bundled Skill;
`aspose-cli skill install aspose-cli-cells --host claude-code` installs one.
