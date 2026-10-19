# Editing with operation batches

Every product edits through `aspose-cli <product> edit <file> --ops <ops>`.
The ops document is a JSON object with an `ops` array; each entry names its
`op` and that operation's fields. Put every related change in one document.

## Discovering operations and fields

The generated schema is the only field reference. Ask it instead of guessing:

```powershell
aspose-cli capabilities pdf edit --output-format json
aspose-cli schema v2/pdf/ops --operation set_properties
aspose-cli schema v2/pdf/ops
```

- `capabilities <product> edit` names the operations, the batch limit and the
  schema command.
- `schema v2/<product>/ops --operation <op>` prints one operation's fields,
  types, defaults, allowed values and descriptions.
- The whole schema states the optional top-level fields of the document, such
  as `ifMatch`, and its `description` says how the product resolves addresses
  across a batch (against the original document or as the earlier operations
  left it) and what each field's `x-readBack` value means.
- A field's `x-readBack` names the read result property that reports what
  the field writes; read a changed field back there before you report it.

The product's `aspose-cli docs <product>/editing` groups the operations by task
and gives recipes and ordering rules the schema cannot state.

## The batch document

A PDF batch, for example:

```json
{
  "ops": [
    { "id": "title", "op": "set_properties", "title": "Quarterly report" },
    { "op": "add_page_numbers" }
  ]
}
```

- Operations apply in order. The bare array of operations is accepted as the
  document with only its `ops`.
- `id` is optional; results echo it, and `op-0001` style ids are assigned when
  omitted. Give ids to the operations you will look up in the result.
- Write the document to a file and pass its path. `--ops` also takes `-` for
  stdin or the JSON itself, but how a shell passes inline JSON or piped text
  varies (`aspose-cli docs troubleshooting`, Windows PowerShell), and only one
  option of a command reads stdin.

## Atomic, best-effort and dry run

- **Atomic (default).** If any operation fails, nothing is written. The error
  names the failing operation in `details.id` and `details.op`, and its
  message and hint say why; a misspelled field, operation or value lists the valid
  ones in `details.available` and the likely ones in `details.suggestions`.
  When several entries fail the same check, `details.errors` lists each of
  them. Fix those entries and run the whole batch again.
- **`--best-effort`.** Successful operations are kept and saved; each failed
  entry in `applied[]` has `status: "failed"` and an `error` that has the shape
  of an error envelope's `error`. The command exits 8 when any operation
  failed. An invalid document or an engine failure still writes nothing.
- A file an operation reads, such as an image, that is missing or locked fails
  only that operation, with `FILE_NOT_FOUND` or `FILE_LOCKED` and its
  `details.id`, like any failed operation: an atomic batch writes nothing,
  and `--best-effort` keeps the other operations.
- **`--dry-run`.** Checks the input, the output and the batch, applies the
  batch in memory and writes nothing. The result has `dryRun: true`, the
  `checked` list and the `applied[]` outcomes, with operation warnings such as
  `OP_NO_EFFECT`; with `--best-effort` it reports every operation's outcome in
  one pass. Warnings about a written output, such as `EVALUATION_MODE` or
  `ENCRYPTION_REMOVED`, come only from the real run, so a clean dry run does
  not mean the real run is free of them.

## Reading the outcome

Each operation has one `applied[]` entry with its `id`, `op`, `status`,
`itemsAffected` and `targets` (`aspose-cli schema v2/common/operation-outcome`).
The CLI reads the objects each operation can change before and after it, so
both fields state what actually changed:

- `itemsAffected` is the number of objects the operation changed. It is 0
  exactly when the operation changed nothing, and then the operation warns
  `OP_NO_EFFECT` with its `details.id`, `details.op` and `details.reason`:
  a replacement that matched no text, say, or a value written again over the
  same value. Act on that warning.
- `targets` lists the addresses of the objects the operation changed, where
  the output holds them, or where the input held an object the batch
  removed; an operation that changed nothing lists none. Past 100 addresses
  it lists one coarser address per unit instead: a range such as
  `page/1-150`, or `<unit>/all` for objects named rather than numbered, such
  as cells or styles.
- A failed operation (`status: "failed"`, kept by `--best-effort`) changed
  nothing: its `itemsAffected` is 0 without an `OP_NO_EFFECT` warning, and
  its `targets` are the addresses it attempted, which need not exist, such as
  `page/2-99` in a three-page document.

The outcome says what changed, not that it is what you meant: read the
changed parts back before you report them (`aspose-cli docs verification`).

A product Skill says when its edits reopen the output before publishing it;
where a product offers `--verify`, it adds a semantic check described in the
product Skill. Any `verification.issues` entry makes `verification.ok` false
and the command exit 8; each issue has a stable `code` and, when known, a
`location` and `hint`.

## Output, in-place and backups

| Option | Effect |
|--------|--------|
| (none) | Writes `<name>.out.<ext>` beside the input |
| `--out <path>` | Writes there; to replace the input use `--in-place` |
| `--overwrite` | Replaces an existing output; without it the edit fails with `OUTPUT_EXISTS` |
| `--in-place` | Replaces the input atomically |
| `--backup` | With `--in-place`: copies the input beside it first |
| `--if-match <sha256>` | Fails with `INPUT_CHANGED` unless the input still has that fingerprint |

Before the first in-place edit of a file you did not create, use
`--in-place --backup --if-match <sha256>` with the fingerprint from your last
read.

`--backup` creates the backup once and never overwrites it, so it stays the
pre-session state for a final comparison; the result's `backup` says whether
this run created it. When the kept backup is older than the file an edit
replaced, a `BACKUP_PREDATES_EDIT` warning says so; copy the file first if
that intermediate version must survive. Tell the user the backup's path and
which version it holds.

## Secrets and encryption

Operation fields whose names end in `Env` (such as `passwordEnv`) hold the
name of an environment variable, never the secret. A missing or empty
variable fails only the operation that names it. Command options follow the
same rule: prefer the `-env` and `-stdin` forms, such as `--password-env`,
`--encrypt-env` and `--password-stdin`; the plain forms are visible in the
process list.

An output made from an encrypted input takes the password an `--encrypt-env`
style option gives. Without one, an output in a format the product can
encrypt keeps the input's password. A command that writes several files into
`outputs[]`, such as `split`, applies the same rule to every file. `encrypted`
on `output` and on each `outputs[]` entry, present for a format the product
can encrypt, states whether that written file is encrypted. A document output
the product cannot encrypt is written unencrypted with an
`ENCRYPTION_REMOVED` warning; tell the user before you deliver it. Images and
the files `extract` writes never warn. The product Skill says which of its
outputs can be encrypted.

## When a target is not found

A not-found error (a `*_NOT_FOUND` code) states what exists, so you can
correct the request without another inspection: `details.requested` echoes
what you asked for, `details.available` lists the existing names and
`details.suggestions` the closest ones; `aspose-cli schema
v2/common/not-found-details` describes every field. Operation errors add
`details.id` and `details.op`, and a best-effort failure keeps the same
object in its `error.details`. A name that several targets share is refused
with `OPS_INVALID` instead of picking one; address the target by its stable id
or number.
