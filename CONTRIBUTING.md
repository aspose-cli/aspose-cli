# Contributing to Aspose CLI

Work from this project's root with .NET SDK 10 and PowerShell 7.
Source, imports, tests and scripts must not depend on a parent or sibling project.
No FOSS source, gitlinks or Git LFS are required.

## Development

1. Update the owning Product with its contracts, schemas, Skills and tests.
2. Run scripts/sync.ps1 after catalog, identity or dependency changes.
3. Run scripts/test.ps1 -Configuration Release and inspect the per-project TRX files under artifacts/TestResults/.
4. Verify portable and win-x64 publication and applicable installation tests.
5. Use a standalone checkout when changing project or build boundaries.

The catalog is the single product roster; generated files and the solution are projections.
Products own their complete behavior. Host provides project-local common lifecycle and commands.
SDK types from a document engine stay inside its matching implementation boundary.

## Quality

Use real engines and CLI child processes. The standard test script provisions the pinned
Chromium runtime for App browser tests. Browser prerequisites are required; failures retain
screenshots and traces beside the project's TRX under artifacts/TestResults/.
Preserve atomic publication, budgets, rollback and service controls.
Do not weaken checks or remove a supported operation to obtain passing tests.
Code comments, diagnostics and public documentation are English.
Do not introduce old command aliases, historical trust allowlists or migration frameworks for unpublished builds.
Report sensitive defects through SECURITY.md.

Commercial SDK tests must remain valid in licensed and evaluation modes. Preserve evaluation disclosures.

Assess relevant shared defect reports independently; entire platform trees do not require synchronization.

## JSON input defaults

Verify optional input defaults through the production source-generated serializer, including omitted
fields and explicit `false`, `0` and `null`. CLR construction alone does not test the wire contract.
For new immutable input records, use optional constructor parameters for scalar defaults:
the current .NET generator [does not preserve init-only property initializers](https://github.com/dotnet/runtime/issues/84484).
Keep required-field and semantic validation in the owning product.

## Command parameter semantics

Every string argument and string option, including arrays, must declare its input role at construction with `WithInput`: `InputKind.File` for document files, `InputKind.JsonSource` for file/inline/stdin JSON, or `InputKind.None` for ordinary values, output paths, directories and separately owned configuration or credentials. Numeric and boolean values default to `None`. Shared option factories own their declarations.

Value sources and secret handling are also declared on the symbol. The Host consumes the actual parser result and these declarations; it must not infer roles from token order, option spelling or the existence of a same-named file. Command-tree construction rejects missing string declarations, conflicting declarations and invalid type/source combinations.

## Preview response ownership

An accepted Preview route owns response completion. Embedded hosts must leave that response
to Preview, including SSE streams owned by the live event hub. A declined route leaves the
response untouched. Keep finite request admission separate from live-stream limits.

Local-service discovery reads, writes and deletes each marker/secret pair under one
resource lock. Keep this coordination in the marker store so readers cannot observe a
mixed pair or block the private writer's atomic replacement on Windows.
