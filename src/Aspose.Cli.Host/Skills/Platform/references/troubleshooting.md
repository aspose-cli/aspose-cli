# Troubleshooting

Read `error.message` and `error.details`; do not retry unchanged input in a
loop. Product errors and their recovery are in the product Skill.

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
    "details": { "subject": "sheet", "requested": "Nope", "availableCount": 2, "available": ["Data", "Summary"] }
  }
}
```

- `code` is stable; branch on it, never on `message`. `capabilities` lists
  every error and warning code of this build under `diagnostics`, with its
  category and exit code.
- `details` carries structured context: the resolved path, the option or
  argument at fault, the valid alternatives (`available`), the
  failing operation's `id` and `op`. `aspose-cli schema v2/common/error-payload`
  describes every key; `errors` appears only when there are several problems.
  `details.reason`, when present, is a short kebab-case token such as
  `unknown-field`; branch on it, and read `message` for the explanation. An
  `OPS_INVALID` about one field names its path in `details.field`.
- `docs`, when present, names a topic for `aspose-cli docs <topic>`.

Warnings do not fail a command. They appear in the result's `warnings` array
with a `code`, `message` and, when there are any, `details`, and on stderr in
human output. A warning with `affectsCompleteness: true` leaves the
output incomplete; report it.

A `LOSSY_CONVERSION` that names what the output leaves out lists the kinds of
content in `details.content`; one without it names no kind, such as one that
says the format may not keep every feature. A `TEXT_NOT_REPLACED` says that an
operation found its text in a part of the document it does not change and left
that text as it was, and that the output still holds it there; a later
operation of the batch that changed or removed it leaves no warning.
`details.part` names the kind of part and `location` the place. `aspose-cli schema
v2/common/error-payload` gives the meaning of every value of both.

## Exit codes

Branch on the exit code first, then on the code in the envelope:

- 0: the command succeeded. Its warnings can still need action.
- 8: the command finished and printed its result, but the result did not pass
  or is incomplete: a failed verification.
  Read the result; it says what failed. Exit 8 means only this: no error
  envelope ever comes with it.
- 130: the command was cancelled with Ctrl+C, with or without --timeout. The
  work stops, nothing is published, its staging is removed and no error is
  written. A second Ctrl+C ends the process at once; the next command that
  writes into the same directory removes the staging it left.
- An exit code the CLI never uses (it uses 0 to 9 and 130), such as
  -1073741571 (`0xC00000FD`): the runtime ended the process, as a stack
  overflow in a document engine does. No error envelope is written, and what
  the command wrote before can remain. Run it with `--timeout` so that it runs
  in a worker, which then publishes nothing and reports `INTERNAL_ERROR`
  (exit 1) with the worker's exit code.
- Any other non-zero exit comes with an error envelope; its code's category
  and exit code are listed in `capabilities`. Usage errors (exit 2) are fixed
  on the command line, input errors (exit 3) in the input file or its
  password, and `OPERATION_TIMEOUT` (exit 9) by a longer `--timeout` or a
  smaller request. A publication that failed and could not be fully restored
  (`OUTPUT_PUBLICATION_PARTIAL`) exits 5 like the other output errors; a
  worker that timed out and could not be confirmed stopped
  (`WORKER_TERMINATION_FAILED`) exits 9: stop the process `details.pid`
  names before retrying.

## Diagnosis

```powershell
aspose-cli license status --output-format json
aspose-cli capabilities --output-format json
```

- `--verbose` writes structured JSONL diagnostics (timings, error codes) to
  stderr and leaves stdout untouched.
- `aspose-cli schema <id>` prints the exact schema of any input or result.

## Recovering from common errors

**The command line.**

- A command line that does not parse, with an unknown command or option or a
  missing argument or option, is `USAGE_ERROR`. One that parses but gives a
  value the command refuses, such as a value outside an option's list, an
  extension no format declares, a number out of range or a range that does not
  parse, is `OPTION_INVALID`. Both name the option in `details.option`, or the
  positional argument in `details.argument`.
- An unknown command, option, value, operation or field lists what exists in
  `details.available`. A
  command that only groups others, run without one, is a `USAGE_ERROR` that
  lists its commands in `details.available`; with human output it prints its
  help to stderr instead.
- A token starting with `--` is never taken as a file argument; pass such a
  file name after a `--` token, which ends the options.
- `-` names standard input only: `--out -` is refused, so name a file. An
  `--out` that resolves to the input is refused too: use `--in-place`.
- An output whose extension is not one of its format's is refused before
  anything is read, listing the accepted extensions in `details.available`.
  `convert` and `render` write the format their `--out`
  extension names when `--to` is absent; a `convert` with neither is refused.
- A selection such as `--pages` takes 1-based ranges such as `1-3,7,9-`, or
  `all`; a number past the end fails with the product's not-found code and
  `details.availableCount`.

**The input file.**

- A relative path resolves against `--workdir`, or the current directory;
  `details.path` is the resolved path.
- A file another application holds (`FILE_LOCKED`): ask the user to close it
  and retry the same command; nothing was written.
- A file that is not a readable document of its type (`FILE_CORRUPT`):
  renaming a file does not change its format. Content no product recognizes
  is `FILE_CORRUPT` when any product reads its extension, even one shared by
  many kinds of file such as `.html` or `.xml`, and `FORMAT_UNSUPPORTED` when
  none does or the CLI only writes its format, such as `.xps`. Find out what
  the file really is.
- A password that is missing or wrong (`PASSWORD_REQUIRED`,
  `PASSWORD_INVALID`): ask the user; never guess. Pass it with
  `--password-env <VAR>` or `--password-stdin`. A command that reads two
  documents, such as `compare`, names the argument in `details.argument`; an
  operation that needs a password names its `*Env` field in `details.field`.
- The file changed while the edit read it (`INPUT_CHANGED`): read it again,
  review what changed, and retry.
- An input over a resource budget: the message and `details` give the limit.
  An input larger than `--max-input-bytes`, by its size or by the bytes read
  of it, is `FILE_TOO_LARGE`: raise the option up to its hard maximum. Any
  other input resource is `INPUT_BUDGET_EXCEEDED`, and a smaller selection
  helps.

**The output.**

- An output that exists (`OUTPUT_EXISTS`): pass `--overwrite` deliberately or
  choose another path. Commands that write several files refuse the whole run
  when one exists; `--overwrite` replaces only the files the command writes.
- A write the operating system refused (`OUTPUT_UNWRITABLE`): a file open in
  another application usually reads fine but fails the final in-place
  replace; ask the user to close it. The input is untouched.
- An output over a resource budget (`OUTPUT_BUDGET_EXCEEDED`), such as too
  many files in one output set: write less, such as a narrower selection;
  `details.resource` and `details.limit` name the budget.

**The engine.**

- `FEATURE_UNSUPPORTED`: this build cannot perform the request on this input;
  the message names what. Do not retry it.
- `RENDER_FAILED`: the engine failed while rasterizing content it can
  otherwise read; the input can still be valid. Reduce the selection or check
  fonts. `RENDER_TOO_LARGE`: lower `--dpi` or render less.
- Slides rendering needs the Microsoft Visual C++ Redistributable (x64),
  which the package does not carry. When it is missing, Slides commands that
  need its native drawing library fail. Ask the user to install it from
  https://aka.ms/vs/17/release/vc_redist.x64.exe.
- `OPERATION_TIMEOUT` with `details.phase` `regex`: a regular expression you
  supplied exceeded its own budget, which `--timeout` does not raise; simplify
  it or search a narrower part of the document.
- Evaluation codes (`EVALUATION_LIMIT` and the `EVALUATION_*` warnings): see
  `aspose-cli docs licensing`.

## Windows PowerShell

How PowerShell hands an `--ops` document to `aspose-cli` depends on its version
and on `$PSNativeCommandArgumentPassing`:

| shell | inline `'{"ops":...}'` | inline with `\"` | piped to `--ops -` | ops file |
|-------|------------------------|------------------|--------------------|----------|
| Windows PowerShell 5.1 | quotes stripped, `OPS_INVALID` | works, but a space inside a string can split it: `USAGE_ERROR` | non-ASCII text becomes `?`, no error | works |
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
