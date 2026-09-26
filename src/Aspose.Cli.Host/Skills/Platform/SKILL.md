---
name: aspose-cli-platform
description: Start here for any document task with the local Aspose CLI (aspose-cli) - session checks, choosing the Cells, PDF, Slides or Words Skill, and the rules every product shares for reading, editing, verifying, previewing, licensing and recovering from errors.
---

# Aspose CLI platform

`aspose-cli` reads, edits, converts, renders and reviews spreadsheets, PDF
documents, presentations and word-processing documents on the local machine
with the Aspose engines, without Office or Python. This Skill holds what every
product shares. Each product Skill holds its own commands, operations and
recipes.

## Session start

Run these once per session:

```powershell
aspose-cli --version
aspose-cli doctor --output json
aspose-cli license status --output json
aspose-cli capabilities --output json
```

- `doctor` reports `ok` and one entry per check (`cli`, `runtime`,
  `resource-budgets`, `license`, `output`). A `license` check of `warn` means
  at least one product runs in evaluation mode.
- `license status` reports each product's `mode` (`licensed`, `evaluation`,
  `invalid`) and effective `source`; see `aspose-cli docs licensing`.
- `capabilities` is the machine-readable truth about this build: products,
  verbs, formats, edit operations and their schema command, review checks,
  every command and option, and every error and warning code with its exit
  code (`diagnostics`). `aspose-cli capabilities <product>` narrows it to one
  product.

When unsure about a command, option, operation or field, ask the CLI
(`--help`, `capabilities`, `schema`, `docs`) instead of guessing.

## Pick the product

| Document | Product Skill | Overview |
|----------|---------------|----------|
| Workbooks: XLSX, XLSM, XLSB, XLS, ODS, CSV, TSV | `aspose-cli-cells` | `aspose-cli docs cells/overview` |
| PDF: inspect, assemble, fill, redact, sign, validate | `aspose-cli-pdf` | `aspose-cli docs pdf/overview` |
| Presentations: PPTX, PPT, ODP | `aspose-cli-slides` | `aspose-cli docs slides/overview` |
| Word processing: DOCX, DOC, RTF, ODT, Markdown, HTML | `aspose-cli-words` | `aspose-cli docs words/overview` |

Read the product overview before the first command on that document type.
`capabilities` lists each product's load, convert and render formats. The
product-neutral commands `review`, `preview` and `fonts check` choose the
product from the file's content; `review` and `preview` accept `--product` to
override the choice.

## Golden rules

1. **Files stay primary.** Commands read and write the document files
   themselves. Read only what you need; never rebuild a document from a JSON
   dump of it.
2. **Inspect before editing.** Start with `<product> inspect`, then read
   bounded windows (`aspose-cli docs reading`).
3. **One validated batch.** Put all related changes in one `edit --ops`
   document. Batches are atomic by default; `--dry-run` validates without
   writing (`aspose-cli docs editing`).
4. **Protect the user's file.** Write to `--out`, or use `--in-place --backup`
   when replacing the input is intended; pass the fingerprint you read as
   `--if-match`. Existing outputs are replaced only with `--overwrite`.
5. **Report engine values.** Numbers and text you report come from a read-back
   after the edit, not from your own arithmetic or memory.
6. **Verify before delivery.** A clean exit code is not done: read changes
   back, run `review`, and open every image it lists
   (`aspose-cli docs verification`).
7. **Disclose evaluation mode.** Any `EVAL_MODE` warning means the delivered
   file carries evaluation marks; tell the user (`aspose-cli docs licensing`).
8. **Recover from the error envelope.** Read `error.hint` and `error.details`
   first; never retry unchanged input in a loop
   (`aspose-cli docs troubleshooting`).
9. **Keep secrets out of arguments.** Pass passwords through environment
   variables (`--password-env`, `--encrypt-env`, `*Env` operation fields) or
   `--password-stdin`, never inline or in ops JSON.
10. **Serialize writes to one file.** Parallel reads are safe; concurrent
    writes to the same file are not.

Pass `--output json` (indented) or `--output compact` (the same JSON on one
line) whenever you parse a result. Every result names its `schema`;
`aspose-cli schema <id>` prints that JSON Schema.

## Routing

| Task | Read |
|------|------|
| Read a document within budgets: windows, `window.next`, search paging | `aspose-cli docs reading` |
| Edit with an ops batch: fields, outputs, backups, secrets, not-found recovery | `aspose-cli docs editing` |
| The delivery checklist, the review protocol, review checks, fonts | `aspose-cli docs verification` |
| Show a document to a person in the live viewer | `aspose-cli docs preview` |
| The browser App: welcome, files, preview, settings | `aspose-cli docs app` |
| License sources, status, install, evaluation disclosure | `aspose-cli docs licensing` |
| The error envelope, exit codes, shared error codes | `aspose-cli docs troubleshooting` |
| Product commands, operations, recipes and product checks | `aspose-cli docs <product>/overview` |

`aspose-cli docs` lists every topic. Install a product Skill for an agent host
with `aspose-cli skill install aspose-cli-cells --host claude-code`
(`aspose-cli skill list` names every bundled Skill).
