# CLI scenarios

A scenario is a JSON document that states a CLI interaction without code: the files a fresh
workspace starts with, the command lines run in it, and what each one must produce.
`ScenarioRunner.Run` executes it against the built `aspose-cli` (the one `CliRunner` finds) in a
`TempWorkspace`, under the run's license mode, and returns every problem it finds rather than the
first; `ScenarioOutcome.AssertPassed()` fails a test with all of them. `ScenarioTests` runs every
`*.scenario.json` in `tests/Aspose.Cli.Invariants.Tests/Scenarios`; add a reproduction
or an executable example there. The generated invariants in
`tests/Aspose.Cli.Invariants.Tests` build their scenarios in code from the same records.

```json
{
  "name": "pdf convert refuses an --out extension that contradicts --to",
  "files": {
    "input.pdf": { "fixture": "pdf.pdf" },
    "notes.txt": { "text": "Hello" }
  },
  "steps": [
    {
      "args": ["pdf", "convert", "input.pdf", "--to", "docx", "--out", "out.xlsx"],
      "expect": {
        "exitCode": 2,
        "error": ["USAGE_ERROR", "FORMAT_UNSUPPORTED"],
        "files": { "absent": ["out.xlsx"], "unchanged": ["input.pdf"], "nothingWritten": true }
      }
    }
  ]
}
```

## Files

Each entry of `files` maps a workspace-relative path to exactly one source:

| Source | Content |
| --- | --- |
| `fixture` | A named fixture from `ScenarioFixtures`, built once per test process with the products' own commands |
| `text` | UTF-8 text without a byte order mark |
| `base64` | Raw bytes |
| `copy` | A file relative to the scenario file |

Fixtures: `<product>.<format>` is a small document holding `ScenarioFixtures.SampleText`; the
product's primary format (`xlsx`, `pdf`, `pptx`, `docx`) comes from its `create` command and any
other format is that document converted with `convert --to <format>`. `<product>.encrypted` is
the primary-format document encrypted with `ScenarioFixtures.Password`. `<product>.ops` is a valid
one-operation document for the product's `edit --ops`; `markdown` and `text` are source text; and
`certificate.pfx` is a self-signed certificate protected by `ScenarioFixtures.Password`.

## Steps

| Field | Meaning |
| --- | --- |
| `args` | The arguments after `aspose-cli`; `--output json` is appended unless they choose an output |
| `env` | Extra environment variables, for `*-env` options |
| `stdin` | Standard input, for `--password-stdin` or `--ops -`; empty when omitted |
| `expect` | What the step must produce; optional |

## Expectations

| Field | Holds when |
| --- | --- |
| `exitCode` | The exit code is this number, or one of an array of numbers |
| `error` | The error envelope's code is this code, or one of an array of codes |
| `warnings` | `present` codes are all in the result's `warnings`, `absent` codes none |
| `json` | Every assertion `{ "path", "equals" \| "contains" \| "exists" }` holds on the step's JSON document: the result, or the error envelope for a failure. Paths are dotted with indexes, such as `error.details.suggestions` or `outputs[0].format`; `contains` tests an array item or a substring |
| `files` | `present` files exist, `absent` files do not, `unchanged` files keep their bytes, and with `nothingWritten` no file is created, changed or deleted |
| `reopens` | Each file opens again with `<product> inspect`; the product is the one routing assigns to the file's extension, else the command's own product when it reads the extension, or `product:path` names it. Any other file must be non-empty and carry its format's signature where one is known (images, PDF, PostScript, HTML, SVG and the ZIP packages XPS and EPUB), since a product that only declares an unrouted extension does not judge another product's output. `@result` stands for every file of the result's `output.path`, `outputs[].path` and `outputs[].output.path` |
| `hidden` | None of the texts, such as secret values, appears in stdout or stderr |

## Contract checks

Every step is also checked, whatever it expects, against the CLI's own catalog
(`CliCatalog`, read from the live `capabilities --output json`):

- `no-internal-error`: it never ends with `INTERNAL_ERROR` or that code's exit code;
- `error-envelope`: a failure leaves stdout empty and writes exactly one JSON error envelope to
  stderr, conforming to its schema, whose code is in the diagnostics catalog with the same exit
  code and whose details conform to the catalog's details schema;
- `result-envelope`: a result is one JSON document conforming to the schema it names, and each
  warning code is in the catalog. The raw `schema` and `docs` documents are exempt.

## License mode

A run is licensed when `ASPOSE_CLI_TEST_LICENSE_PATH` names a license: the license is copied into
each workspace as its project license, `.aspose/license.lic`, which `files` expectations ignore.
Otherwise the CLI runs in evaluation mode. Fixtures are built in the same mode.
