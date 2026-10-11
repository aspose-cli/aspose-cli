---
name: aspose-cli-platform
description: Start here for any document task with the local Aspose CLI (aspose-cli) - session checks, choosing the Cells, PDF, Slides or Words Skill, and the rules every product shares for reading, editing, verifying, previewing, licensing and recovering from errors.
---

# Aspose CLI platform

`aspose-cli` reads, edits, converts, renders and reviews spreadsheets, PDF
documents, presentations and word-processing documents on the local machine
with the Aspose engines, without Office or Python. This Skill holds how to work
with every product. Each product Skill holds its own commands, operations and
recipes.

## Ask the CLI, not this Skill

The CLI describes itself, and its answer is the truth about this build. The
Skills do not repeat it:

| Question | Ask |
|----------|-----|
| Which commands, options, option values and defaults exist | `aspose-cli <command> --help`, `aspose-cli capabilities <product> <command>` |
| Which formats a product loads, converts and renders | `aspose-cli capabilities --summary` |
| Which edit operations exist and what fields they take | `aspose-cli schema v2/<product>/ops --operation <op>` |
| What a result field means | `aspose-cli schema <id>`, with the id the result names in `schema` |
| Which error and warning codes exist, and each code's exit code | `capabilities`, under `diagnostics` |
| What an error's `details` keys mean | `aspose-cli schema v2/common/error-payload` |
| Resource budgets and the operation batch limit | `capabilities`, under `resourceBudgets`, and `capabilities <product> edit` |
| A product's commands, recipes and checks | `aspose-cli docs <product>/overview` |

A mistyped command, option, value, operation or field fails with the valid
names in `details.available` and the likely ones in `details.suggestions`;
read them instead of guessing again. A command that only groups others, such
as `aspose-cli pdf`, run without one exits 2: with JSON output (the default
when stdout is redirected) it is a `USAGE_ERROR` that lists its commands in
`details.available`, and with human output it prints its help to stderr.

## Session start

Run these once per session:

```powershell
aspose-cli --version --output-format json
aspose-cli doctor --output-format json
aspose-cli license status --output-format json
aspose-cli capabilities --summary --output-format json
```

- `doctor` checks the runtime, budgets, licenses and output folder; `ok`
  false means a check failed, and the command then exits 8.
- `license status` says which products run licensed and which in evaluation
  mode (`aspose-cli docs licensing`).
- `capabilities --summary` is the short first look: each product's commands,
  formats and operation names. Narrow the full `capabilities` with a product
  and a command instead of reading all of it.

## Pick the product

| Document | Product Skill | Overview |
|----------|---------------|----------|
| Workbooks: XLSX, XLSM, XLSB, XLS, ODS, CSV, TSV | `aspose-cli-cells` | `aspose-cli docs cells/overview` |
| PDF: inspect, assemble, fill, redact, sign, validate | `aspose-cli-pdf` | `aspose-cli docs pdf/overview` |
| Presentations: PPTX, PPT, ODP | `aspose-cli-slides` | `aspose-cli docs slides/overview` |
| Word processing: DOCX, DOC, RTF, ODT, Markdown, HTML | `aspose-cli-words` | `aspose-cli docs words/overview` |

Read the product overview before the first command on that document type.
The product-neutral commands `review`, `preview open` and `fonts check`
choose the product from the file's content; `review` and `preview open` accept
`--product` to override the choice.

## Golden rules

1. **Files stay primary.** Commands read and write the document files
   themselves. Read only what you need; never rebuild a document from a JSON
   dump of it.
2. **Inspect before editing.** Start with `<product> inspect`, then read
   bounded windows (`aspose-cli docs reading`).
3. **One validated batch.** Put all related changes in one `edit --ops`
   document. Batches are atomic by default (`aspose-cli docs editing`).
4. **Protect the user's file.** Write to `--out`, or use `--in-place --backup`
   when replacing the input is intended; pass the fingerprint you read as
   `--if-match`. Existing outputs are replaced only with `--overwrite`.
   Every command that writes files takes `--dry-run`: it checks the request,
   writes nothing and lists what it checked in `checked`; a dry run that
   passes does not guarantee that the real run succeeds.
5. **Report engine values.** Numbers and text you report come from a read-back
   after the edit, not from your own arithmetic or memory.
6. **Verify before delivery.** A clean exit code is not done: read changes
   back, run `review`, and open every image it lists
   (`aspose-cli docs verification`).
7. **Disclose evaluation mode.** An `EVALUATION_MODE` or `EVALUATION_INPUT_MARKED`
   warning means the delivered file carries evaluation marks, and
   `EVALUATION_INPUT_TRUNCATED` that the result is incomplete; tell the user
   (`aspose-cli docs licensing`).
8. **Recover from the error envelope.** Read `error.hint` and `error.details`
   first; never retry unchanged input in a loop
   (`aspose-cli docs troubleshooting`).
9. **Keep secrets out of arguments.** Pass passwords through environment
   variables (`--password-env`, `--encrypt-env`, `*Env` operation fields) or
   `--password-stdin`, never inline or in ops JSON.
10. **Serialize writes to one file.** Parallel reads are safe; concurrent
    writes to the same file are not.

Pass `--output-format json` (indented) or `--output-format compact` (the same
JSON on one line) whenever you parse a result. Every result names its
`schema`; `aspose-cli schema <id>` prints that JSON Schema.

## Routing

| Task | Read |
|------|------|
| Read a document within budgets: windows, `window.next`, search paging | `aspose-cli docs reading` |
| Edit with an ops batch: outcomes, outputs, backups, secrets, not-found recovery | `aspose-cli docs editing` |
| The delivery checklist, the review protocol, review checks, fonts | `aspose-cli docs verification` |
| Show a document to a person in the live viewer | `aspose-cli docs preview` |
| The browser App: welcome, files, preview, settings | `aspose-cli docs app` |
| License sources, status, install, evaluation disclosure | `aspose-cli docs licensing` |
| The error envelope, exit codes, recovering from errors | `aspose-cli docs troubleshooting` |
| Product commands, operations, recipes and product checks | `aspose-cli docs <product>/overview` |

`aspose-cli docs` lists every topic. Install a product Skill for an agent host
with `aspose-cli skill install aspose-cli-cells --host claude-code`
(`aspose-cli skill list` names every bundled Skill).
