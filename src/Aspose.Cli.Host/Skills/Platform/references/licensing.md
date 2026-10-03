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
| `source` | The effective source, such as `flag`, `env:ASPOSE_PDF_LICENSE_PATH`, `project:words` or `user:cells` |
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
5. The product license, then the shared license, installed for this user.

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
  watermarks or added evaluation content. The mark is in the file you deliver,
  so tell the user.
- `EVAL_INPUT_TRUNCATED` means evaluation mode loaded only part of an input,
  so the result is incomplete. On a read, the text you get back is the
  engine's replacement ("...text has been truncated due to evaluation version
  limitation"), not what the file holds: check the file's text in rendered
  images instead, and do not report the file as damaged. A command that loads
  such an input and saves writes the replacement into its output.
- Reads (`inspect`, `query`) add no marks and carry no `EVAL_MODE` warning;
  input, resource and engine limits still apply.
- Detect evaluation from the JSON `warnings` array. With `--output table`,
  `--quiet` hides the `EVAL_MODE` line together with the stderr license status
  line.
- Installing a license does not clean files already produced. Regenerate the
  deliverable from the original, unmarked inputs.

The product overview lists what evaluation mode changes in its own documents.
