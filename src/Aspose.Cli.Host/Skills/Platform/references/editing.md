# Editing with operation batches

Every product edits through `aspose-cli <product> edit <file> --ops <ops>`.
The ops document is a JSON object with an `ops` array; each entry names its
`op` and that operation's fields. Put every related change in one document.

## Discovering operations and fields

The generated schema is the only field reference. Ask it instead of guessing:

```powershell
aspose-cli capabilities pdf edit --output json
aspose-cli schema v2/pdf/ops --operation encrypt
aspose-cli schema v2/pdf/ops
```

- `capabilities <product> edit` lists the operation names under
  `products[].operations[].ops`, the batch limit (`maximumOperations`) and
  the `operationSchema` command.
- `schema v2/<product>/ops --operation <op>` prints one operation's fields,
  types, defaults, allowed values and descriptions. An unknown name fails with
  `OPTION_INVALID` and lists the valid names in `details.available`.
- The whole schema's top-level `description` states how the product resolves
  addresses across a batch (against the original document or as the earlier
  operations left it).

The product's `aspose-cli docs <product>/editing` groups the operations by task
and gives recipes and ordering rules the schema cannot state.

## The batch document

A PDF batch, for example:

```json
{
  "ops": [
    { "id": "title", "op": "set_metadata", "title": "Quarterly report" },
    { "op": "add_page_numbers" }
  ]
}
```

- `ops` holds 1 or more entries, applied in order.
- `id` is optional; results echo it, and `op-0001` style ids are assigned when
  omitted.
- Optional top-level fields: `schema` (checked when present),
  `schemaVersion` (`2`) and `ifMatch` (the input's SHA-256 fingerprint).
- `--ops` takes a file path, `-` for stdin, or the JSON itself when the value
  starts with `{` or `[`. Windows PowerShell strips inner double quotes from
  inline JSON passed to a native command; write the document to a file or pipe
  it through `--ops -`.

## Atomic, best-effort and dry run

- **Atomic (default).** If any operation fails, nothing is written. The error
  carries `details.index` (zero-based), `details.op` and, for validation
  failures, `details.reason` (such as `unknown field 'style.shiny'`). Fix that
  entry and run the whole batch again.
- **`--best-effort`.** Successful operations are kept and saved; each failed
  entry in `applied[]` has `status: "failed"` and an `error` with `code`,
  `message`, `hint` and `details`. The command exits 8 when any operation
  failed. An invalid document or an engine failure still writes nothing.
- **`--dry-run`.** Resolves and applies the batch in memory and writes
  nothing; the result has `dryRun: true`. With `--best-effort` it reports every
  operation's outcome in one pass.

Each `applied[]` entry has `id`, `index`, `op`, `status`, `itemsAffected` and
`targets` (the addresses it changed). Products that reopen their output before
publishing report `mutation.verification: "reopened"`; where a product offers
`--verify`, it adds a semantic check described in the product Skill.

## Output, in-place and backups

| Option | Effect |
|--------|--------|
| (none) | Writes `<name>.out.<ext>` beside the input |
| `--out <path>` | Writes there; an `--out` that resolves to the input is `OPTION_INVALID` |
| `--overwrite` | Replaces an existing output; without it the edit fails with `OUTPUT_EXISTS` |
| `--in-place` | Replaces the input atomically |
| `--backup` | With `--in-place`: copies the input to `<name>.backup.<ext>` first |
| `--if-match <sha256>` | Fails with `INPUT_CHANGED` (exit 3) unless the input still has that fingerprint |

`--backup` creates the backup once and never overwrites it: later runs report
`backup.created: false` and keep the original, so the backup stays the
pre-session state for a final comparison. When the kept backup holds an earlier
version than the file an edit replaced, `backup.holdsReplacedVersion` is false
and a `BACKUP_PREDATES_EDIT` warning gives its `lastWriteUtc`; copy the file
first if that intermediate version must survive. Tell the user its path and
which version it holds.

Before the first in-place edit of a file you did not create, use
`--in-place --backup --if-match <sha256>` with the fingerprint from your last
read.

## Secrets

Operation fields whose names end in `Env` (such as `passwordEnv` or
`ownerPasswordEnv`) hold the name of an environment variable, never the
secret. Each named variable is read once. A missing or empty variable fails
only the operation that names it, with `OPS_INVALID` naming the variable: an
atomic batch writes nothing, `--best-effort` keeps the other operations and
exits 8. Command options follow the same rule: prefer `--password-env`,
`--encrypt-env` and `--password-stdin`; `--password` and `--encrypt` are
visible in the process list.

## When a target is not found

A not-found error (`SHEET_NOT_FOUND`, `PAGE_NOT_FOUND`, `BOOKMARK_NOT_FOUND`,
...) states what exists, so you can correct the request without another
inspection:

| Field | Meaning |
|-------|---------|
| `details.subject` | What was looked up, such as `sheet` or `bookmark` |
| `details.requested` | The name, number or range as you wrote it |
| `details.availableCount` | How many targets of that kind exist |
| `details.available` | The existing names in document order, at most 50; present for named targets |
| `details.suggestions` | Up to three existing names closest to the request, best first |

Operation errors add `details.index` and `details.op`, and a best-effort
failure keeps the same object in its `error.details`. A name that several
targets share is refused with `OPS_INVALID` instead of picking one; address
the target by its stable id or number.
