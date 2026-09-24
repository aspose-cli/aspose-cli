# Licensing and evaluation mode

The CLI itself is free (Apache-2.0). The Aspose engine underneath needs a
license to leave *evaluation mode*. Read-only commands do not add watermarks;
input, resource and SDK limits still apply. Produced artifacts may carry
watermarks or added evaluation content.

## Product status

```
aspose-cli license status --product cells --output json
```

Inspect the `cells` entry in `products[]`. Its `mode` is `licensed`, `evaluation`,
`invalid` (a configured source was rejected), or `not-applicable`. `source`
names the effective configuration, for example `env:ASPOSE_LICENSE_PATH`.
No top-level mode represents another product's status. `identity` is an
opaque hash of the validated selection and is absent if any selected source
is invalid. `license status` reports such failures in `products[].problem`
and still exits 0; document commands reject the broken source with exit 7.

Table/Markdown startup prints an SDK-verified status line to stderr. `--quiet`
suppresses it; JSON, verbose JSONL, MCP and internal service protocols remain
machine-readable. Use the result envelope and `license status` for automation.

## Providing a license

For an interactive local setup, run `aspose-cli app --welcome`, choose the `.lic`
file in the browser, and manage it later under Settings. The file is validated
from one private snapshot of at most 1 MiB before an atomic user-level install;
license content is never displayed or logged. CLI and App use the same source
precedence; installing a user file does not override an explicit or environment
source. After a change the viewer service recycles its renderer, so the next
render applies the new license (`aspose-cli docs app`). The CLI workflow remains
available for agents and CI.

Resolution order — the first that resolves wins:

`--license <path>` → `ASPOSE_CELLS_LICENSE_B64` →
`ASPOSE_CELLS_LICENSE_PATH` → `ASPOSE_LICENSE_B64` →
`ASPOSE_LICENSE_PATH` → `.aspose/licenses/cells.lic` →
`.aspose/license.lic` in the working directory → product-specific and shared
licenses in the user config directory.

For document operations, a broken configured license is a hard error (exit 7),
never a silent fallback to evaluation — see the diagnostic distinction above.
`aspose-cli license install <file>`
validates a file and installs it as this user's default; `aspose-cli license
remove` atomically removes saved user files (env and project sources are left untouched).
An unchanged validated license can reuse a preview; a changed source or content
recycles its renderer. A rejected license leaves an existing preview untouched.

## Evaluation mode — what you must handle

Every output-producing command (`create`, `edit`, `convert`, `render`)
returns a warning and stamps the file:

```
aspose-cli cells create book.xlsx --sheets "Data,Summary" --overwrite --output json
```

```json
"warnings": [ { "code": "EVAL_MODE", "docs": "licensing",
               "message": "Evaluation mode: ... watermark ...", "hint": "..." } ]
```

The engine also inserts an "Evaluation Warning" worksheet and a cell watermark
into the saved file. **Disclose the watermark to the user** — it is in the file
you deliver, not just on your screen. Reads (`query`, `inspect`) carry no
`EVAL_MODE` warning; they remain subject to normal budgets and SDK limits.

Then handle these traps (each verified against the real CLI):

- **The eval sheet becomes the ACTIVE sheet.** `query range` and `render` *without*
  `--sheet` target it, so you get the watermark text/image instead of your
  data. Always name the sheet:

  ```
  aspose-cli cells render book.xlsx --sheet Data --out check.png --overwrite --output json
  ```

- **Text export is limited to the first worksheet in evaluation mode.**
  `convert --to csv`, `--to tsv`, and `--to md` can export only sheet index 0.
  An explicit `--sheet` naming another worksheet fails with `EVALUATION_LIMIT`
  (exit 7) before writing or replacing any output. Apply an Aspose.Cells license
  to export that worksheet, or explicitly choose the first worksheet. Without
  `--sheet`, the first worksheet is exported; `SHEETS_DROPPED` names it when
  the workbook contains other sheets.
  Text outputs still include the evaluation notice.

- **Further saves can add another eval sheet** ("Evaluation Warning",
  "Evaluation Warning (1)", …). `inspect` lists them all; the count climbs with
  each edit.

- **Data projections are contaminated too**, not only renders: a `csv` gains a
  trailing watermark row, a `md` a trailing `# Evaluation Only…` heading, and a
  whole-workbook `json` the eval sheets plus their watermark string.

- **Applying a license does not clean existing evaluation artifacts.**
  Added sheets and watermark content can survive a licensed re-save.
  Generate the licensed deliverable again from the original unmarked inputs.

- **`--quiet` with `--output table` hides the `EVAL_MODE` line from BOTH
  streams.** Do not rely on table mode to notice eval. Detect it reliably from
  `--output json` and read the `warnings` array — it survives `--quiet`.

## License errors in document commands (exit 7)

| code | meaning |
|------|---------|
| `LICENSE_FILE_NOT_FOUND` | the configured path does not exist |
| `LICENSE_INVALID` | the file is not a valid Aspose license |

Both mean a configured license source is broken. Fix the path/file or remove
the setting to run in evaluation mode. The diagnostic command below exits 0
and reports `mode: "invalid"` plus `problem`; it does not run a document
operation. Do not retry unchanged input in a loop.

```
aspose-cli license status --product cells --license missing.lic --output json
```

## See also

`aspose-cli docs troubleshooting` — the exit-7 recovery table and every other error
code's fix.
