# Editing with an ops document

Every product edits with `aspose-cli <product> edit <file> --ops <ops>`. The
ops document is a JSON object with an `ops` array of `set`, `add`, `delete`,
`move` and `do` entries on the product's object table.

## The table

```powershell
aspose-cli schema v2/pdf/ops/directory
aspose-cli schema v2/pdf/ops/field
aspose-cli schema v2/pdf/ops/do.stamp_text
```

- `ops/directory` lists the kinds with their address forms, the rows a `set`
  writes and the rows that are read only, each add with its arguments and
  each action with its arguments.
- `ops/<kind>`, `ops/add.<kind>` and `ops/do.<action>` print one entry in
  full: each row's or argument's type and the engine member it names.
- A row names one engine member: setting it sets that member only. What else
  the engine changes with it is the engine's doing.

## The document

```json
{
  "ops": [
    { "set": "document", "props": { "info.title": "Quarterly report" } },
    { "do": "stamp_text", "args": { "page": "page/1", "value": "1" } }
  ]
}
```

- Operations apply in order, each on what the ones before it left.
- `--ops` takes a path, `-` for stdin, or the JSON itself when it starts with
  `{`. A path works the same in every shell (`references/troubleshooting.md`).

## Atomic batches

When any operation fails, nothing is written. The error names the operation
by its position in `details.index` and its address in `details.at`; a name
that does not exist lists the names that do in `details.available`. An
exception the engine throws is `ENGINE_ERROR`, with the engine's exception
type and message in `details`.

## The read-back

After the save, the file is opened again and each operation is read from it:

- a `set`, `add` or `move` gives its `at` and the object's rows `now`;
- a `delete` gives its `at` and `now: null`;
- a `do` gives the engine's `returned` value, and the `at` it names, if any.

`counts` lists the inventory counts that changed, before and after. The
read-back is what the saved file holds, not a judgment of it.

## Outputs

| Option | Effect |
|--------|--------|
| (none) | Writes `<name>.out.<ext>` beside the input |
| `--out <path>` | Writes there |
| `--in-place` | Replaces the input |
| `--overwrite` | Replaces an existing output; without it an existing output fails with `OUTPUT_EXISTS` |

An edit writes its input's format; `convert` changes formats.

## Secrets and encryption

An argument whose name ends in `Env`, such as `passwordEnv`, holds the name of
an environment variable, never the secret; a missing or empty variable fails
that operation. Command options work the same way: `--password-env`,
`--encrypt-env` and `--password-stdin`, which reads the first line of stdin as
UTF-8. The plain `--password` and `--encrypt` are visible in the process
list.

An output made from an encrypted input keeps the input's password in a format
the product can encrypt, unless an `--encrypt` option gives another; the files
a command lists in `outputs[]` follow the same rule. `encrypted` on `output`
and on each `outputs[]` entry states whether that written file is encrypted. A document output in a format the product cannot
encrypt is written unencrypted with an `ENCRYPTION_REMOVED` warning.

## Not found

A not-found error (`ADDRESS_NOT_FOUND` and the other `*_NOT_FOUND` codes) gives
what was asked for in `details.requested` and the names that exist in
`details.available`; `aspose-cli schema v2/common/not-found-details` describes
every field.
