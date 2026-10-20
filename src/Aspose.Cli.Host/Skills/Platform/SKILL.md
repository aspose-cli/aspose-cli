---
name: aspose-cli-platform
description: Start here for any document task with the local Aspose CLI (aspose-cli) - session checks, choosing the Cells, PDF, Slides or Words Skill, and the rules every product shares for reading, editing, verifying, previewing, licensing and recovering from errors.
---

# Aspose CLI platform

`aspose-cli` reads, edits, converts, renders and reviews spreadsheets, PDF
documents, presentations and word-processing documents on the local machine
with the Aspose engines, without Office or Python. This Skill holds the rules
every product shares; each product Skill holds its own commands, operations
and recipes.

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
- `capabilities --summary` lists each product's commands, formats and
  operation names. Narrow the full `capabilities` with a product and a command
  instead of reading all of it.

## Pick the product

| Document | Product Skill | Overview |
|----------|---------------|----------|
| Workbooks: XLSX, XLSM, XLSB, XLS, ODS, CSV, TSV | `aspose-cli-cells` | `aspose-cli docs cells/overview` |
| PDF: inspect, assemble, fill, redact, sign, validate | `aspose-cli-pdf` | `aspose-cli docs pdf/overview` |
| Presentations: PPTX, PPT, ODP | `aspose-cli-slides` | `aspose-cli docs slides/overview` |
| Word processing: DOCX, DOC, RTF, ODT, Markdown, HTML | `aspose-cli-words` | `aspose-cli docs words/overview` |

Read the product overview before the first command on that document type.
`review`, `preview open` and `fonts check` choose the product from the file's
content; `review` and `preview open` take `--product` to override it.

## How to work

1. **Inspect, then read windows.** Start with `<product> inspect`, then read
   only the ranges, pages, slides or blocks you need, following `window.next`
   (`aspose-cli docs reading`). Never rebuild a document from a JSON dump.
2. **Edit in one batch.** Put related changes in one `edit --ops` file; the
   batch is atomic by default (`aspose-cli docs editing`).
3. **Protect the user's file.** Write to `--out`, or replace the input with
   `--in-place --backup --if-match <sha256>` using the fingerprint you read.
   `--dry-run` checks a request without writing.
4. **Read back, then review.** Report values from a read after the edit, run
   `review` and open every image it lists (`aspose-cli docs verification`).
5. **Disclose what the warnings say.** Evaluation marks, removed encryption
   and incomplete results go to the user with the file
   (`aspose-cli docs licensing`).

Pass `--output-format json` (or `compact`, the same JSON on one line) whenever
you parse a result. Every result names its `schema`, an id for
`aspose-cli schema <id>`, not a URL to fetch. Ignore result fields, warning
codes and error codes you do not know; later versions add them. `--verbose`
output is diagnostics for a person, not something to parse.

## Common mistakes

- **Guessing names.** A mistyped command, option, value, operation or field
  fails with the valid names in `details.available` and the closest in
  `details.suggestions`; use them.
- **Retrying unchanged input.** Read `error.hint` and `error.details`, change
  the request, then retry (`aspose-cli docs troubleshooting`).
- **Inline ops JSON in PowerShell.** Shells mangle quotes and non-ASCII text;
  write the ops document to a file and pass its path.
- **Composing the next page yourself.** Run `window.next` as given.
- **Trusting the exit code.** Exit 0 can carry `OP_NO_EFFECT`; read changed
  parts back before reporting them.
- **Claiming a visual pass without looking.** Open every image `review`
  lists; state the coverage you inspected.
- **Missing evaluation marks.** Detect `EVALUATION_*` warnings in the JSON
  `warnings` array; `--quiet` hides them on stderr.
- **Secrets in arguments.** Pass passwords with `--password-env`,
  `--encrypt-env`, `--password-stdin` or `*Env` operation fields, never inline
  or in ops JSON.
- **Parallel writes.** Parallel reads are safe; serialize writes to one file.
- **Overwriting by accident.** Existing outputs are replaced only with
  `--overwrite`; choose a new path unless replacing is intended.

## Ask the CLI, not this Skill

The CLI describes itself, and its answer is the truth about this build:

| Question | Ask |
|----------|-----|
| Commands, options, values and defaults | `aspose-cli <command> --help`, `aspose-cli capabilities <product> <command>` |
| Formats a product loads, converts and renders | `aspose-cli capabilities --summary` |
| Edit operations and their fields | `aspose-cli schema v2/<product>/ops --operation <op>` |
| What a result field means | `aspose-cli schema <id>`, with the id the result names in `schema` |
| Error and warning codes and their exit codes | `capabilities`, under `diagnostics` |
| What an error's `details` keys mean | `aspose-cli schema v2/common/error-payload` |
| Resource budgets and the operation batch limit | `capabilities`, under `resourceBudgets`, and `capabilities <product> edit` |

## References

| Topic | Read it when |
|-------|--------------|
| `aspose-cli docs reading` | Reading within budgets: windows, `window.next`, search paging, rendering |
| `aspose-cli docs editing` | Editing with an ops batch: outcomes, outputs, backups, secrets, not-found recovery |
| `aspose-cli docs verification` | Before delivery: the checklist, the review protocol, review checks, fonts |
| `aspose-cli docs preview` | Showing a document to a person in the live viewer |
| `aspose-cli docs app` | A person wants the browser App: welcome, files, preview, settings |
| `aspose-cli docs licensing` | License sources, status, install, evaluation disclosure |
| `aspose-cli docs troubleshooting` | An error: the envelope, exit codes, recovery, updates, PowerShell |
| `aspose-cli docs <product>/overview` | Product commands, operations, recipes and checks |

`aspose-cli docs` lists every topic. Install a Skill for an agent host with
`aspose-cli skill install aspose-cli-cells --host claude-code`;
`aspose-cli skill list` names every bundled Skill.
