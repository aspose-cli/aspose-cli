# Licensing and evaluation mode

The CLI itself is free (Apache-2.0). The Aspose engine underneath needs a
license to leave *evaluation mode*; without one, every file you produce is
watermarked. Reads are never restricted.

## Two modes

```
aspose-cli license status --output json
```

`mode` is `licensed` or `evaluation`; when licensed, `source` names the winning
license source (e.g. `env:ASPOSE_LICENSE_PATH`). `aspose-cli doctor --output json` reports
the same as a health check — its `license` check is `ok` (licensed) or `warn`
(evaluation).

## Providing a license

For an interactive local setup, run `aspose-cli app --welcome`, choose the `.lic`
file in the browser, and manage it later under Settings. The file is validated
before an atomic user-level install; license content is never displayed or
logged. The CLI workflow below remains available for agents and CI.

Resolution order — the first that resolves wins:

`--license <path>` → `ASPOSE_CELLS_LICENSE_B64` →
`ASPOSE_CELLS_LICENSE_PATH` → `ASPOSE_LICENSE_B64` →
`ASPOSE_LICENSE_PATH` → `.aspose/licenses/cells.lic` →
`.aspose/license.lic` in the working directory → product-specific and shared
licenses in the user config directory.

A broken *explicit* license is a hard error (exit 7), never a silent fall back
to evaluation — see the error section. `aspose-cli license install <file>`
validates a file and installs it as this user's default; `aspose-cli license
remove` deletes that default (env and project sources are left untouched).

## Evaluation mode — what you must handle

Every output-producing command (`new`, `write`, `edit`, `convert`, `render`)
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
you deliver, not just on your screen. Reads (`read`, `info`) carry no
`EVAL_MODE` warning and are unaffected.

Then handle these traps (each verified against the real CLI):

- **The eval sheet becomes the ACTIVE sheet.** `read` and `render` *without*
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
  `--sheet`, the first worksheet is exported and named in `SHEETS_DROPPED`.
  Text outputs still include the evaluation notice.

- **Every save stacks another eval sheet** ("Evaluation Warning",
  "Evaluation Warning (1)", …). `info` lists them all; the count climbs with
  each edit.

- **Data projections are contaminated too**, not only renders: a `csv` gains a
  trailing watermark row, a `md` a trailing `# Evaluation Only…` heading, and a
  whole-workbook `json` the eval sheets plus their watermark string.

- **The mark is permanent.** The eval sheet is baked into the file and survives
  a later licensed re-save — applying a license does not heal an already-marked
  file. The only cure is to delete the sheet(s) while licensed:

  ```
  aspose-cli cells edit book.xlsx --ops '{"ops":[{"op":"delete_sheet","sheet":"Evaluation Warning"}]}' --in-place --output json
  ```

- **`--quiet` with `--output table` hides the `EVAL_MODE` line from BOTH
  streams.** Do not rely on table mode to notice eval. Detect it reliably from
  `--output json` and read the `warnings` array — it survives `--quiet`.

## License errors (exit 7)

| code | meaning |
|------|---------|
| `LICENSE_FILE_NOT_FOUND` | the configured path does not exist |
| `LICENSE_INVALID` | the file is not a valid Aspose license |

Both mean an *explicitly configured* license is broken. Fix the path/file or
remove the setting to run in evaluation mode. Do NOT retry in a loop — the input
will not change on its own.

```
aspose-cli license status --license missing.lic --output json
```

## See also

`aspose-cli docs troubleshooting` — the exit-7 recovery table and every other error
code's fix.
