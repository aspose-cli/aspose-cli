# Licensing and evaluation mode

The Aspose engines run in evaluation mode until a license applies. Each
product (`cells`, `pdf`, `slides`, `words`) is licensed independently: a
Cells license does not license Words, while a Total license covers every
product.

## Status

```powershell
aspose-cli license status --output-format json
```

`products[]` has one entry per product (`--product <id>` selects one) with its
`mode` and the effective `source`; `aspose-cli schema
v2/common/license-status` describes every field. A `mode` of `invalid` means a
configured source was rejected: its `problem` and `hint` say why and how to
fix it. `license status` is diagnostic and still exits 0 then, while document
commands refuse that source with exit 7.

A document result that ran in evaluation mode carries `license`, with its
`mode`; a licensed result omits `license`.

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
file) is an error, never a silent fall back to evaluation mode; an empty
environment variable counts as unset.

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

## License errors

A license error exits 7. When a configured source is missing or rejected, the
error's `details.source` names the source and its message and hint say what is
wrong: fix the path or file, or remove the setting to run in evaluation mode.
Do not retry unchanged input.

## Evaluation mode: what you must disclose

- A command that writes files in evaluation mode, such as `create`, `edit`,
  `convert` or `render`, warns `EVALUATION_MODE`, and what it writes carries evaluation watermarks or added evaluation content. The
  message names the marks the CLI recognizes in the output. The mark is in the
  file you deliver, so tell the user.
- `EVALUATION_INPUT_TRUNCATED` means evaluation mode kept only part of an
  input the command read, so the result is incomplete; the message names the
  engine and what it left out. Text a read returns can then be the engine's
  replacement rather than what the file holds: check the file's text in
  rendered images instead, and do not report the file as damaged.
- `EVALUATION_LIMIT` (exit 7) means an evaluation limit stopped the command,
  and nothing was written. `details.limit` and `details.unit` give the limit,
  and the hint offers a way without a license, such as a smaller selection,
  only where one works for that command.
- With a license, `EVALUATION_INPUT_MARKED` means the output keeps evaluation
  marks that an earlier save without a license wrote into its input; its
  message lists them. The license does not remove them, so tell the user and
  regenerate the deliverable from the original, unmarked inputs.
- Detect evaluation from the JSON `warnings` array, never from human output:
  `--quiet` hides warnings on stderr.
- Installing a license does not clean files already produced. Regenerate the
  deliverable from the original, unmarked inputs.

The product overview lists what evaluation mode changes in its own documents,
which reads it truncates and which commands it limits.

## Self-check the evaluation disclosure

To see what a user without a license gets, run the command with
`--license-mode evaluation`. It reads no license source, not even a broken
one, so the command runs in evaluation mode while every configured license
stays in place:

```powershell
aspose-cli words convert report.docx --to pdf --out eval-check.pdf --license-mode evaluation --output-format json
aspose-cli license status --license-mode evaluation --output-format json
```

The result reports the evaluation `license` and warnings as it would without
a license, except that `EVALUATION_MODE` names the request as the cause.
Write to a scratch output, never over a deliverable. The option applies to one
command and cannot be combined with `--license`; commands whose effect
outlives the command, such as `license install`, refuse it.
