# Troubleshooting

Errors are built for self-correction. Read `error.hint` first, then
`error.details`; do not retry unchanged input in a loop. Product errors and
their recovery are in the product Skill.

## The error envelope

A failed command writes one JSON envelope to stderr, leaves stdout empty and
exits non-zero:

```json
{
  "schema": "https://schemas.aspose.com/aspose-cli/v2/common/error.schema.json",
  "schemaVersion": 2,
  "error": {
    "code": "SHEET_NOT_FOUND",
    "message": "No sheet 'Nope' was found. Must be one of: Data, Summary",
    "details": { "subject": "sheet", "requested": "Nope", "availableCount": 2, "available": ["Data", "Summary"] },
    "hint": "Use one of the names in details.available."
  }
}
```

- `code` is stable; branch on it, never on `message`. `capabilities` lists
  every error and warning code of this build under `diagnostics`, with its
  category and exit code.
- `details` carries structured context: the resolved path, the option or
  argument at fault, the valid alternatives (`available`, `suggestions`), the
  failing operation's `id` and `op`. `aspose-cli schema v2/common/error-payload`
  describes every key; `errors` appears only when there are several problems.
- `hint` is the most likely next action; `docs`, when present, names a topic
  for `aspose-cli docs <topic>`.

Warnings do not fail a command. They appear in the result's `warnings` array
with a `code`, `message` and, when there is one, `hint` and `details`, and on
stderr in human output. A warning with `affectsCompleteness: true` leaves the
output or its evidence incomplete; report it.

## Exit codes

Branch on the exit code first, then on the code in the envelope:

- 0: the command succeeded. Its warnings can still need action.
- 8: the command finished and printed its result, but the result did not pass
  or is incomplete: a failed operation under `--best-effort`, failed
  verification, a failed review, a missing font in `fonts check`, a failed
  `doctor` check. Read the result; it says what failed.
- 130: the command was cancelled with Ctrl+C, with or without --timeout. The
  work stops, nothing is published, its staging is removed and no error is
  written. A second Ctrl+C ends the process at once; the next command that
  writes into the same directory removes the staging it left.
- Any other non-zero exit comes with an error envelope; its code's category
  and exit code are listed in `capabilities`. Usage errors (exit 2) are fixed
  on the command line, input errors (exit 3) in the input file or its
  password, and `OPERATION_TIMEOUT` (exit 9) by a longer `--timeout` or a
  smaller request.

## Diagnosis

```powershell
aspose-cli doctor --output-format json
aspose-cli license status --output-format json
aspose-cli capabilities --output-format json
```

- `--verbose` writes structured JSONL diagnostics (timings, error codes) to
  stderr and leaves stdout untouched.
- `aspose-cli schema <id>` prints the exact schema of any input or result.
- `aspose-cli fonts check <file>` explains most rendering and layout
  surprises (`aspose-cli docs verification`).

## Recovering from common errors

**The command line.**

- A command line that does not parse, with an unknown command or option or a
  missing argument or option, is `USAGE_ERROR`. One that parses but gives a
  value the command refuses, such as a value outside an option's list, an
  extension no format declares, a number out of range or a range that does not
  parse, is `OPTION_INVALID`. Both name the option in `details.option`, or the
  positional argument in `details.argument`.
- An unknown command, option, value, operation or field lists what exists in
  `details.available` and the closest names in `details.suggestions`; the
  hint names a command by its full path, such as `pdf inspect`. A command that
  only groups others, run without one, is a `USAGE_ERROR` that lists its
  commands in `details.available`; with human output it prints its help to
  stderr instead.
- A token starting with `--` is never taken as a file argument; pass such a
  file name after a `--` token, which ends the options.
- `-` names standard input only: `--out -` is refused, so name a file. An
  `--out` that resolves to the input is refused too: use `--in-place`.
- An output whose extension is not one of its format's is refused before
  anything is read, listing the accepted extensions in `details.available`
  and, for a mistyped one such as `.docz`, the closest in
  `details.suggestions`. `convert` and `render` write the format their `--out`
  extension names when `--to` is absent; a `convert` with neither is refused.
  An output that names another format, such as `--out report.pdf`, has a hint
  naming the command that writes it: an `edit` or `create` writes its own
  format first, to convert or render after; a `render` points to the
  `convert` of the same input.
- A selection such as `--pages` takes 1-based ranges such as `1-3,7,9-`, or
  `all`; a number past the end fails with the product's not-found code and
  `details.availableCount`.

**The input file.**

- A relative path resolves against `--workdir`, or the current directory;
  `details.path` is the resolved path.
- A file another application holds (`FILE_LOCKED`): ask the user to close it
  and retry the same command; nothing was written.
- A file that is not a readable document of its type (`FILE_CORRUPT`):
  renaming a file does not change its format. When the extension disagrees
  with the content (`FORMAT_MISMATCH`), `details.detected` names the product
  the content looks like and the hint names its command: use it, rename the
  file, or pass `--product <id>` to `review` or `preview open`. When several
  products or readings fit (`FORMAT_AMBIGUOUS`), pass what the hint names.
- A password that is missing or wrong (`PASSWORD_REQUIRED`,
  `PASSWORD_INVALID`): ask the user; never guess. Pass it with
  `--password-env <VAR>` or `--password-stdin`. A command that reads two
  documents, such as `compare`, names the argument in `details.argument` and
  its own option in the hint; an operation that needs a password names its
  `*Env` field in `details.field`.
- The file changed since you read it (`INPUT_CHANGED`): read it again, review
  what changed, and retry with the new `input.fingerprint.sha256`.
- An input over a resource budget: the message and `details` give the limit;
  `--max-input-bytes` raises the size limit up to its hard maximum,
  and a smaller selection helps the others.

**The output.**

- An output that exists (`OUTPUT_EXISTS`): pass `--overwrite` deliberately or
  choose another path. Commands that write several files refuse the whole run
  when one exists; `--overwrite` replaces only the files the command writes.
- A write the operating system refused (`OUTPUT_UNWRITABLE`): a file open in
  another application usually reads fine but fails the final in-place
  replace; ask the user to close it. The input and any backup are untouched.

**The engine.**

- `FEATURE_UNSUPPORTED`: this build cannot perform the request on this input;
  the message names what. Do not retry it.
- `RENDER_FAILED`: the engine failed while rasterizing content it can
  otherwise read; the input can still be valid. Reduce the selection or check
  fonts. `RENDER_TOO_LARGE`: lower `--dpi` or render less.
- `OPERATION_TIMEOUT` with `details.phase` `regex`: a regular expression you
  supplied exceeded its own budget, which `--timeout` does not raise; simplify
  it or search a narrower part of the document.
- Evaluation codes (`EVALUATION_LIMIT` and the `EVALUATION_*` warnings): see
  `aspose-cli docs licensing`.

**Updating.** `update check` and `update install` read a release feed: the
latest published release by default, or a `RELEASE-MANIFEST.json` URL, file
or downloaded release directory.

- `RELEASE_FEED_UNAVAILABLE`: the feed could not be read, and nothing was
  checked or installed. `details.feed` names it and the hint says whether the
  network or the address is the cause.
- `RELEASE_VERIFICATION_FAILED`: the release did not match its manifest, or
  is older than this build; nothing was installed. Never work around it.
- `UPDATE_IN_PROGRESS` (warning): the installer of an earlier
  `update install` is still running; wait, then check `aspose-cli --version`.
- `UPDATE_FAILED` (warning): the installer of an earlier `update install`
  failed or stopped. Its message gives the reason and the hint the installer
  log; close the programs that use this installation, including agents
  running `aspose-cli mcp serve`, then run `update install` again.

## Windows PowerShell

How PowerShell hands an `--ops` document to `aspose-cli` depends on its version
and on `$PSNativeCommandArgumentPassing`:

| shell | inline `'{"ops":...}'` | inline with `\"` | piped to `--ops -` | ops file |
|-------|------------------------|------------------|--------------------|----------|
| Windows PowerShell 5.1 | quotes stripped, `OPS_INVALID` | `USAGE_ERROR` or `OPS_INVALID` | non-ASCII text becomes `?`, no error | works |
| PowerShell 7.3+, `Windows` (default) or `Standard` | works | `OPS_INVALID` | works | works |
| PowerShell 7, `Legacy` | quotes stripped, `OPS_INVALID` | works | works | works |

- Write the ops document to a file and pass its path: that works in every
  shell, in UTF-8 with or without a byte order mark and in the UTF-16 that
  `Out-File` and `>` write in Windows PowerShell 5.1. Never escape quotes as
  `\"`; whether that helps depends on the shell.
- Windows PowerShell 5.1 pipes text to a native command as ASCII
  (`$OutputEncoding`), so `--ops -` silently turns Chinese and other non-ASCII
  text into `?`.
- Windows PowerShell 5.1's `>` redirection re-encodes stdout as UTF-16; parse
  stdout directly or use PowerShell 7.
