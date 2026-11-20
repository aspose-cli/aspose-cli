# Editing with operation batches

Every product edits through `aspose-cli <product> edit <file> --ops <ops>`.
The ops document is a JSON object with an `ops` array; each entry is one
`set`, `add`, `delete`, `move` or `do` on the product's object table. Put
every related change in one document.

## Discovering the table

The generated schema is the only reference. Ask it instead of guessing:

```powershell
aspose-cli schema v2/pdf/ops --operation directory
aspose-cli schema v2/pdf/ops --operation field
aspose-cli schema v2/pdf/ops --operation do.stamp_text
```

- `--operation directory` lists the kinds with their address forms, the rows
  a `set` writes and the rows that are read only, each add with its
  parameters and each action with its parameters, in one short document.
- `--operation <kind>`, `add.<kind>` or `do.<action>` prints one entry in
  full: each row's or parameter's type and the engine member it names.
- A row names one engine member: setting it sets that member only, and what
  else the engine changes with it is the engine's doing.

## The batch document

A PDF batch, for example:

```json
{
  "ops": [
    { "set": "document", "props": { "info.title": "Quarterly report" } },
    { "do": "stamp_text", "args": { "page": "page/1", "value": "1" } }
  ]
}
```

- Operations apply in order, each on what the ones before it left.
- Write the document to a file and pass its path. `--ops` also takes `-` for
  stdin or the JSON itself, but how a shell passes inline JSON or piped text
  varies (`aspose-cli docs troubleshooting`, Windows PowerShell), and only one
  option of a command reads stdin.

## Atomic batches

- If any operation fails, nothing is written. The error names the failing
  operation by its position in `details.index` and its address in
  `details.at`, and its message says why; a misspelled kind, row or value
  lists the valid ones in `details.available`. Fix the entry and run the whole
  batch again.
- A file an operation reads, such as an image, that is missing or locked fails
  that operation, with `FILE_NOT_FOUND` or `FILE_LOCKED`, like any failed
  operation, and nothing is written.

## Reading the outcome

The result has one entry per operation, read from the saved file after it was
opened again: its `at` and what the object's rows are `now`; an action gives
the engine's `returned` value and the `at` it names. `counts` lists the
inventory counts that changed. The outcome says what the file holds, not that
it is what you meant: check it against what you asked for before you report
it.

## Output and in-place

| Option | Effect |
|--------|--------|
| (none) | Writes `<name>.out.<ext>` beside the input |
| `--out <path>` | Writes there; to replace the input use `--in-place` |
| `--overwrite` | Replaces an existing output; without it the edit fails with `OUTPUT_EXISTS` |
| `--in-place` | Replaces the input atomically |

## Secrets and encryption

Parameters whose names end in `Env` (such as `passwordEnv`) hold the name of
an environment variable, never the secret. A missing or empty variable fails
only the operation that names it. Command options follow the same rule:
prefer the `-env` and `-stdin` forms, such as `--password-env`,
`--encrypt-env` and `--password-stdin`; the plain forms are visible in the
process list. A `-stdin` form reads the password as the first line of stdin,
as UTF-8 text whatever the console's code page, and refuses other bytes with
`OPTION_INVALID`.

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
correct the request without another read: `details.requested` echoes what you
asked for and `details.available` lists the existing names;
`aspose-cli schema v2/common/not-found-details` describes every field.
