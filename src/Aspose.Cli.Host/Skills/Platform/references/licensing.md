# Licensing and evaluation mode

The Aspose engines run in evaluation mode until a license applies. Each
product (`cells`, `pdf`, `slides`, `words`) is licensed on its own: a Cells
license does not license Words, while a Total license covers every product.

## Status

```powershell
aspose-cli license status --output-format json
```

`products[]` has one entry per product (`--product <id>` selects one) with its
`mode` and the effective `source`; `aspose-cli schema v3/common/license-status`
describes every field. A `mode` of `invalid` means a configured source was
rejected: its `problem` says why. `license status` still exits 0 then, while
document commands refuse that source with exit 7.

## The evaluation marker

A document command that ran without a license carries `license` in its result,
with `mode` `evaluation`; a licensed result has no `license`. In evaluation
mode the engines change what they read and write as the Aspose SDKs decide:
watermarks or added content in the files written, inputs read in part. The
product Skill lists what its engine changes.

- Text a read returns can be the engine's evaluation replacement rather than
  what the file holds: check the text in rendered images, and do not report
  the file as damaged.
- `EVALUATION_LIMIT` (exit 7): an evaluation limit stopped the command, and
  nothing was written; `details.limit` and `details.unit` give the limit.

`--license-mode evaluation` runs one command in evaluation mode without
reading any license source, while every configured license stays in place.
It cannot be combined with `--license`, and commands whose effect outlives
the command, such as `license install`, refuse it.

## Resolution order

For each product, the first source that is set wins (`<PRODUCT>` is `CELLS`,
`PDF`, `SLIDES` or `WORDS`):

1. `--license <path>` on the command.
2. `ASPOSE_<PRODUCT>_LICENSE_B64`, then `ASPOSE_<PRODUCT>_LICENSE_PATH`.
3. `ASPOSE_LICENSE_B64`, then `ASPOSE_LICENSE_PATH`.
4. `.aspose/licenses/<product>.lic`, then `.aspose/license.lic`, in the working
   directory.
5. The product license, then the shared license, installed for this user, in
   `%APPDATA%\aspose-cli`, or the absolute directory `ASPOSE_CLI_CONFIG_DIR`
   names. Windows locates `%APPDATA%` itself, so changing the `APPDATA`
   variable does not move it.

A configured source that is broken (a missing path, a directory, a rejected
file) is an error with exit 7 and `details.source`, never a fall back to
evaluation mode; an empty environment variable counts as unset.

## Install and remove

```text
aspose-cli license install <file.lic>
aspose-cli license install <file.lic> --product words
aspose-cli license remove --product words
```

`license install` validates the file and installs it for this user, for every
product it covers or only for `--product`. `license remove` removes installed
user licenses, all of them or one product's; environment variables and project
files stay as they are. A `--license` option or an environment variable keeps
priority over an installed license. License content is never printed or
logged, and installing a license does not change files written before.
