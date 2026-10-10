# Aspose CLI repository guide

An independent open-source CLI whose executable is `aspose-cli`; Windows x64 is the only
supported platform. `eng/products.json` is the only product roster and `eng/distribution.json`
the fixed distribution identity; the generated projections and the solution come from
`scripts/sync.ps1` and are never hand-edited. How to build and test is in
[CONTRIBUTING.md](CONTRIBUTING.md); the security boundary is in [SECURITY.md](SECURITY.md).

## Boundaries

- Launcher -> Host -> SDK; launcher -> active Products; Products -> SDK. Products never
  reference another Product.
- SDK types from a document engine stay inside its matching Product Engine adapter, in
  signatures and method bodies alike; analyzers and architecture tests enforce this.
- Host carries no product-specific branch. Product ids come from the catalog, and
  distribution metadata identifies this executable.
- Each Product owns its Contracts, Engine (a session and the static handlers its commands run),
  Commands (options, binding and table output per command), view adapter, Presenter, Skills
  and module definition, whose command menu pairs each command with its handler; its
  definitions are pure and deterministic. Its result and operation schemas are generated from its
  records and their documentation comments; nothing hand-writes a schema.
  Skill names begin with `aspose-cli-`, their executable examples must match this build's
  capabilities, and package-relative documentation links must resolve. What every product
  shares lives once, in the Host's `aspose-cli-platform` Skill; a product Skill holds only
  product knowledge and points to the generated schema instead of restating it.
- Source, project references, build imports, tests and publishing tools stay inside this
  project. Never reference a parent workspace or another CLI project.

## Design rules

- Every behavior has one owner. Behavior shared by Products belongs in the SDK, not in copies.
- Extend at compile time through the catalog and explicit registration; no runtime plugin
  loading, reflection scanning or dependency-injection containers.
- Derive contracts from one source: generate what can be generated and test that it is current,
  rather than keeping hand-written copies in sync.
- Prefer deleting unused surface to preserving it.
- The [gates](#gates) judge a change: never add a known violation to make a change pass. Code
  metrics (`eng/tools/CodeHealth`) are a diagnostic, never a target.

## Gates

They judge a change without a reviewer reading it. Fix the product or the code to pass them; never
weaken a check or remove a supported operation to make a test pass.

- **Scenarios and invariants.** `tests/Aspose.Cli.TestKit/Scenarios` runs declarative
  `*.scenario.json` files (format in its README); add a reproduction or example to
  `tests/Aspose.Cli.Platform.Tests/Invariants/Scenarios`. The `*InvariantTests` generate cases
  from `capabilities` and each ops schema and check what every command keeps: no internal error,
  one valid error envelope, suggestions for mistakes, refused unwritable outputs, read-only
  commands and dry runs that write nothing, outputs that reopen, hidden secrets, and evaluation
  writes that warn `EVAL_MODE`.
- **Known violations.** `Invariants/known-violations.json` lists today's product defects by
  `cause`, per license mode (`modes`). A case passes only when its violations and their text match
  its entries, so a new, changed or fixed violation fails; delete an entry when its defect is
  fixed. Adding an entry or widening `modes` needs the owner's `quality-exception` label.
- **Analyzers.** Production code builds with the analyzers in the `[src/**.cs]` section of
  `.editorconfig` as errors; fix a violation rather than suppressing it. `src` projects write a
  documentation file, so documentation comments must be well formed.
- **Skill operations.** Every operation a product Skill shows must parse and match the product's
  ops schema.

## Contracts

- **Operations** are records with `[Operation("name")]` under the product's
  `[OperationVocabulary]` base, listed in the product's ops JSON context, with the handler method
  `I{Base}Handler` requires. The record is the contract: `required` members, initializers for
  defaults, `[InputPath]`, `[SecretEnv]`, constraint attributes and record rules
  (`[ExactlyOneOf]`, `[AtLeastOneOf]`, `[DependentRequired]`, `[PresentWhen]`, `[MinProperties]`),
  and a `Validated()` override, stated in its summary, for the rest. Summaries are the schema's
  descriptions. `APCLI012` rejects an incomplete contract.
- **Results** are records passing their relative schema id and version to `ResultEnvelope`,
  `EngineResultEnvelope` (adds `license`) or `WindowedResultEnvelope` (adds `window`), listed in
  the assembly's JSON context; a shared block publishes itself with `[SchemaId]`. Ids are relative
  to the owner: `v2/common/` for the SDK and Host, `v2/<product>/` for a product. `required`,
  nullability, `[AlwaysPresent]`, `[OneOfBy]` and `[OpenEnum]` state the shape. `APCLI013` rejects
  a record it cannot describe.
- **JSON input** defaults are tested through the production source-generated serializer,
  including omitted fields and explicit `false`, `0` and `null`. It
  [does not preserve init-only initializers](https://github.com/dotnet/runtime/issues/84484), so an
  input record outside an operation vocabulary takes scalar defaults as optional constructor
  parameters.
- **Error codes** shared by products or the Host are declared once in the SDK's `ErrorCodes` and
  built only by SDK error factories; a product declares its own in its `*Diagnostics` class. A
  missing target uses an `ErrorCode.NotFound` code built with `CliErrors.NotFound` (named
  targets) or `CliErrors.NotFoundAt` (numbered targets); a name several targets share is refused.
- **Command parameters** declare their input role with `WithInput` and their value sources and
  secret handling on the symbol; the Host reads only these declarations. `GlobalOptionNames` and
  `StandardOptionNames` are the reserved names (`APCLI008`). Products define commands with
  `CommandDefinition` or `EditDefinition`, paired with their handlers on the product menu
  (`APCLI011` forbids `StandardOptions`).

## Tests

- xUnit v3 with real engines and CLI child processes. A test that takes several seconds carries
  `[Category(TestCategory.Slow)]`; installer and Playwright tests carry `Installer` and
  `Browser`.
- A test that changes process-wide state joins its serial collection in
  `tests/TestAssemblyFixture.cs`. A test project that reads a repository file outside the
  projects lists it as a `RepositoryInput` item.
- Runs never read `%APPDATA%\aspose-cli`, project `.aspose` files or `ASPOSE_*` settings; only
  `ASPOSE_CLI_TEST_*` variables pass through. Keep license contents out of logs and fixtures.

## Must not break

- Every boundary in [SECURITY.md](SECURITY.md): local services, MCP, network, files and secrets.
- Worker cancellation and rollback.
- The resource-based interprocess locks, which other Aspose CLI distributions on the machine
  share.
- Licensed and evaluation behavior, including honest disclosure of evaluation output changes.
- Existing user files and uncommitted changes you did not make. Never clean unknown files.
- Files stay primary: never round-trip an entire document through JSON.

## Conventions

Use only the corresponding commercial Aspose SDK packages; no FOSS source, gitlinks or
Git LFS.
Code comments, diagnostics, documentation and Skills are English and describe present behavior.
Never add old-command aliases, legacy installer or Skill readers, historical version
baselines, historical trust allowlists, or migration frameworks for unpublished builds.
The application is pre-release: update code, schemas, Skills and tests together for an
intentional contract change.

Before changing code around a commercial SDK, verify the official API usage and reproduce
suspected engine behavior with a minimal SDK-only case. Correct our misuse in the owning
adapter. Record each confirmed SDK defect in [KNOWN-ISSUES.md](KNOWN-ISSUES.md) as
its introduction describes, and handle it openly with a
refusal, a workaround through other public API, or a warning. Never hide a defect with implicit
default rewrites, file-format patches, or product, producer or version special cases.
