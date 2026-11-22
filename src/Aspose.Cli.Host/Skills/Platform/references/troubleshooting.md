# Troubleshooting

A failed command writes one JSON envelope to stderr, leaves stdout empty,
writes no file and exits non-zero. Its message and details state the fact;
changing the request is the caller's.

## The error envelope

```json
{
  "schema": "https://schemas.aspose.com/aspose-cli/v3/common/error.schema.json",
  "schemaVersion": 3,
  "error": {
    "code": "ADDRESS_NOT_FOUND",
    "message": "No sheet 'Nope' was found. Must be one of: Data, Summary",
    "details": { "subject": "sheet", "requested": "Nope", "availableCount": 2, "available": ["Data", "Summary"], "at": "sheet/Nope" }
  }
}
```

- `code` is stable; `aspose-cli schema v3/common/diagnostics` lists every
  error and warning code of this build with its category and exit code.
- `details` holds the facts: the resolved `path`, the `option` or `argument`
  at fault, the names that exist (`available`), an operation's `index` and
  `at`. `aspose-cli schema v3/common/error-payload` describes every key.
  `details.reason`, when present, is a short token such as `unknown-field`; an
  `OPS_INVALID` about one field names its path in `details.field`.

Warnings do not fail a command. They appear in the result's `warnings` array
with a `code`, a `message` and, when there are any, `details`.

## Exit codes

- 0: the command did what was named, and what it wrote was saved.
- 130: the command was cancelled with Ctrl+C; nothing was written and no error
  is printed.
- An exit code the CLI never uses (it uses 0 to 7, 9 and 130), such as
  -1073741571 (`0xC00000FD`): the runtime ended the process, as a stack
  overflow in a document engine does. No error envelope is written.
- Any other non-zero exit comes with an error envelope; the diagnostics
  document gives each code's exit code. 2 is the command line, 3 the input
  file or its password, 4 the request, 5 the output, 6 a format, 7 the license,
  9 a timeout.

## Common errors

**The command line.**

- A command line that does not parse (an unknown command or option, a missing
  argument) is `USAGE_ERROR`. One that parses but gives a value the command
  refuses is `OPTION_INVALID`. Both name the option in `details.option`, or
  the positional argument in `details.argument`.
- A token starting with `--` is never a file argument; pass such a file name
  after `--`.
- `-` names standard input only: `--out -` is refused. An `--out` that
  resolves to the input is refused too; `--in-place` replaces the input.
- An output whose extension is not one of its format's is refused before
  anything is read, with the accepted extensions in `details.available`.

**The input file.**

- A relative path resolves against `--workdir`, or the current directory. A
  file that is missing, held by another application or over
  `--max-input-bytes` (`FILE_NOT_FOUND`, `FILE_LOCKED`, `FILE_TOO_LARGE`) is
  named in `details.path`.
- `FORMAT_UNSUPPORTED`: the file's extension names a format the command does
  not read, such as one only `convert` reads; the message lists the
  extensions it reads.
- `FILE_CORRUPT`: the engine could not read the file as a format the product
  reads; the message gives the engine's reason.
- A file that needs a password (`PASSWORD_REQUIRED`, `PASSWORD_INVALID`) takes
  it from `--password-env <VAR>` or `--password-stdin`.
- `INPUT_CHANGED`: the file changed while the command read it.
- `INPUT_BUDGET_EXCEEDED`: a resource of the input is over its budget;
  `details` gives the budget and its limit.

**The output.**

- `OUTPUT_EXISTS`: the output exists and `--overwrite` was not given. Commands
  that write several files refuse the whole run when one exists.
- `OUTPUT_UNWRITABLE`: the operating system refused the write, as for a file
  another application holds open. The input is untouched.
- `OUTPUT_BUDGET_EXCEEDED`: the output is over a resource budget;
  `details.resource` and `details.limit` name it.

**The engine.**

- `ENGINE_ERROR`: the engine threw while it worked; `details.engineException`
  and `details.engineMessage` give its exception as it is.
- `RENDER_FAILED`: the engine lays out no page for a sheet the render
  includes, so there is nothing to draw; `details.sheet` names it.
  `RENDER_TOO_LARGE`: the image is over the pixel budget.
- Slides rendering needs the Microsoft Visual C++ Redistributable (x64),
  which the package does not carry: https://aka.ms/vs/17/release/vc_redist.x64.exe.
- `OPERATION_TIMEOUT` with `details.phase` `regex`: a regular expression you
  gave exceeded its own time budget.
- `EVALUATION_LIMIT`: an evaluation limit stopped the command
  (`references/licensing.md`).

## Windows PowerShell

How PowerShell hands an inline `--ops` document to `aspose-cli` depends on its
version and on `$PSNativeCommandArgumentPassing`:

| shell | inline `'{"ops":...}'` | inline with `\"` | piped to `--ops -` | ops file |
|-------|------------------------|------------------|--------------------|----------|
| Windows PowerShell 5.1 | quotes stripped, `OPS_INVALID` | a space inside a string can split it: `USAGE_ERROR` | non-ASCII text becomes `?`, no error | works |
| PowerShell 7.3+, `Windows` (default) or `Standard` | works | `OPS_INVALID` | works | works |
| PowerShell 7, `Legacy` | quotes stripped, `OPS_INVALID` | works | works | works |

- An ops file works in every shell, in UTF-8 with or without a byte order mark
  and in the UTF-16 that `Out-File` and `>` write in Windows PowerShell 5.1.
- Windows PowerShell 5.1 pipes text to a native command as ASCII
  (`$OutputEncoding`), and its `>` redirection re-encodes stdout as UTF-16.
