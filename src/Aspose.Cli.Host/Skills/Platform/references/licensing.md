# Licensing and evaluation mode

The Aspose engines run in evaluation mode until a license applies. Each
product (`cells`, `pdf`, `slides`, `words`) is licensed independently: a
Cells license does not license Words, while a Total license covers every
product.

## Status

```powershell
aspose-cli license status --output json
```

`products[]` has one entry per product (`--product <id>` selects one):

| Field | Meaning |
|-------|---------|
| `mode` | `licensed`, `evaluation`, or `invalid` when a configured source was rejected |
| `source` | The effective source, such as `flag`, `env:ASPOSE_PDF_LICENSE_PATH`, `project:words` or `user:cells`; `requested` under `--license-mode evaluation` |
| `path` | The license file behind a file source |
| `problem`, `hint` | Why a configured source is invalid, and how to fix it |
| `userLicenseInstalled` | Whether a product license is installed for this user |

The top level adds `sharedUserLicenseInstalled` and `identity`, an opaque hash
of the validated selection that is absent while any selected source is
invalid. `license status` is diagnostic: it reports a broken source as
`mode: "invalid"` and still exits 0, while document commands refuse that
source with exit 7. Document results also carry `license.mode`.

## Resolution order

For each product, the first source that is set wins (`<PRODUCT>` is `CELLS`,
`PDF`, `SLIDES` or `WORDS`):

1. `--license <path>` on the command.
2. `ASPOSE_<PRODUCT>_LICENSE_B64`, then `ASPOSE_<PRODUCT>_LICENSE_PATH`.
3. `ASPOSE_LICENSE_B64`, then `ASPOSE_LICENSE_PATH`.
4. `.aspose/licenses/<product>.lic`, then `.aspose/license.lic`, in the working
   directory.
5. The product license, then the shared license, installed for this user, in
   the configuration directory: `%APPDATA%\aspose-cli`, or the absolute
   directory named by `ASPOSE_CLI_CONFIG_DIR`. Windows locates `%APPDATA%`
   itself, so changing the `APPDATA` variable does not move it.

A configured source that is broken (a missing path, a directory, a rejected
file) is an error, never a silent fall back to evaluation mode. An empty
`--license` value is refused with `OPTION_INVALID`; an empty environment
variable counts as unset.

## Install and remove

```text
aspose-cli license install <file.lic>
aspose-cli license install <file.lic> --product words
aspose-cli license remove --product words
```

`license install` validates the file and installs it for this user, for every
product it covers or only for `--product`. `license remove` removes installed
user licenses, all of them or one product's; environment variables and project
files stay untouched. Neither overrides a higher source: a `--license` option
or an environment variable keeps priority. License content is never displayed
or logged. People can do the same in the App (`aspose-cli docs app`). After a
change, the viewer service applies the new license on its next render
(`aspose-cli docs preview`).

## License errors (exit 7)

| Code | Meaning |
|------|---------|
| `LICENSE_FILE_NOT_FOUND` | The path configured by `details.source` does not exist or is a directory |
| `LICENSE_INVALID` | The engine rejected the file configured by `details.source`; `details.reason` says why |
| `EVALUATION_LIMIT` | An evaluation restriction prevents the requested operation, which is refused before any output is written |

Fix the path or file, or remove the setting to run in evaluation mode. Do not
retry unchanged input.

## Evaluation mode: what you must disclose

- Output-producing commands (`create`, `edit`, `convert`, `render` and the
  like) return an `EVAL_MODE` warning, and the produced file carries evaluation
  watermarks or added evaluation content. Its message names the evaluation
  marks the CLI recognizes in the output, whether this save or an earlier one
  wrote them, such as a warning sheet or a notice row, and, for page images and
  page text, those of the source they show. The mark is in the file
  you deliver, so tell the user.
- `EVAL_INPUT_TRUNCATED` means evaluation mode kept only part of the content,
  so the result is incomplete: usually an input it loaded, and for some
  products (its Skill says which) an edited output it saved. On a read, the text you get back is the
  engine's replacement ("...text has been truncated due to evaluation version
  limitation"), not what the file holds: check the file's text in rendered
  images instead, and do not report the file as damaged. Whether a saved
  output carries the replacement depends on the product; its Skill says which
  outputs do.
- Reads (`inspect`, `query`) add no marks and carry no `EVAL_MODE` warning;
  input, resource and engine limits still apply. A dry run publishes nothing
  and carries none either.
- With a license, `EVAL_INPUT_MARKED` means the output keeps evaluation marks
  that an earlier save without a license wrote into its input (a watermark, a
  banner, a warning sheet or slide box), or a page image or page text shows a
  source that carries them; its message lists them. A licensed
  save adds none, so every mark a licensed output carries came from its input.
  The license does not remove them, so tell the user and regenerate the
  deliverable from the original, unmarked inputs. A PDF is checked on its first
  four pages, which every evaluation save of Aspose.PDF stamps; a notice only
  on a later page, as in a PDF merged from a clean and a marked one, is not
  reported, so `review` the whole file.
- Detect evaluation from the JSON `warnings` array. With `--output table`,
  `--quiet` hides the `EVAL_MODE` line together with the stderr license status
  line.
- Installing a license does not clean files already produced. Regenerate the
  deliverable from the original, unmarked inputs.

The product overview lists what evaluation mode changes in its own documents.

## Self-check the evaluation disclosure

To see what a user without a license gets, run the command with
`--license-mode evaluation`. It reads no license source, not even a broken
one, so the command runs in evaluation mode while every configured license
stays in place:

```powershell
aspose-cli words convert report.docx --to pdf --out eval-check.pdf --license-mode evaluation --output json
aspose-cli license status --license-mode evaluation --output json
```

The result reports `license.mode: "evaluation"`, the `EVAL_MODE` warning and,
where evaluation mode changed what the result holds, `EVAL_INPUT_TRUNCATED`, as
it would without a license, except that
`EVAL_MODE` names the request as the cause; `license status` names the source
`requested`, and `doctor` says the mode was requested. Write to a scratch output, never over a
deliverable. The option applies to one command (and to every call an MCP
server started with it runs, unless a call chooses otherwise); it cannot be
combined with `--license`. `preview open` opens such a document as its own, never
reusing a licensed one. `license install`, `license remove` and `app` refuse
it, because their effect outlives the command.
