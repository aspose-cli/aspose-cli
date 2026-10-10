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
  The owner implements the decision with a type, a pipeline or a generator so that nothing else
  can make it, and analyzers, architecture tests and invariants enforce that; the same decision
  made in several places is this repository's main source of defects. See
  [Decision owners](#decision-owners).
- Extend at compile time through the catalog and explicit registration; no runtime plugin
  loading, reflection scanning or dependency-injection containers. Commands are written by hand:
  no command source generator, runtime reflection binding, reflection-based JSON, or generic
  session type in the SDK.
- A command lives in three places, the Contracts request and result, the Commands options,
  binding and table, and the Engine handler, paired in one line of the product menu. The command
  tree built from the menus is the registry: capabilities, help, schemas and documentation checks
  are projections of it, so keep no separate registration data. A product session holds only
  the write pipeline, the resource budgets and the loader; a product-specific concern that wraps
  every handler is a `.Guard` on the module.
- Do not adopt string property bags with regex dispatch, parallel dispatch paths, in-memory
  sessions with deferred writes, partially successful batches, ambiguous exit codes, raw XML or
  XPath write layers, whole-document export as a replayable batch, self-update or
  auto-install, out-of-process plugins, or a home-grown renderer or formula engine. They break
  one source of truth, atomic publication, local-only operation or files as primary, or move an
  SDK's job into the CLI.
- Derive contracts from one source: generate what can be generated and test that it is current,
  rather than keeping hand-written copies in sync.
- Prefer deleting unused surface to preserving it.
- The [gates](#gates) judge a change: never add a known violation to make a change pass. Code
  metrics (`eng/tools/CodeHealth`) are a diagnostic, never a target.

### Decision owners

| # | Decision | Owner | Enforced by |
|---|---|---|---|
| D1 | Output: path, written format, overwrite, encryption, part names, staging | The SDK resolves a `ResolvedOutput` for the product; the format is resolved once: the format `--to` names, else the one among the command's writable formats that the `--out` extension declares, else the edited input's format, the `--to` default or the command's only format | `DecisionOwnershipTests` D1: no raw output path string in requests, no format from an extension, no product-built `FORMAT_UNSUPPORTED`, resolver or protectable-format list |
| D2 | Format capabilities | Each product declares its formats once in its Contracts `*Formats.Definitions`, passed to `.Formats(...)` on the module; its `*EngineFormats` maps every declared id to the engine | `CellsEngineFormatTests`, `PdfEngineFormatTests`, `SlidesEngineFormatsTests`, `WordsEngineFormatsTests`: every declared format has an engine mapping; `DecisionOwnershipTests` D2 |
| D3 | Mistake suggestions | SDK `Mistake` alone writes `details.suggestions[]` and the "did you mean" text | `DecisionOwnershipTests` D3; invariants |
| D4 | Error construction | SDK error factories alone build shared codes | `DecisionOwnershipTests` D4; `FreeTextErrorFactoryTests` |
| D5 | Secrets | SDK `Secret`: redacted when printed, revealed only in an engine adapter | `DecisionOwnershipTests` D5: products read no environment variable; passwords outside the engine are `Secret` |
| D6 | Evaluation detection and disclosure | The SDK write pipeline resolves the license once and derives disclosure by comparing input and output marks; a product supplies only its evaluation-mark recognizer | `EvaluationOwnershipTests`; the evaluation invariant |
| D7 | Truthful results: affected counts, targets, pages, no match | Planned: the SDK snapshots before and after each operation, reports `OP_NO_EFFECT` for zero, and runs `--verify` as one step | Planned: handlers count nothing; invariant "affected > 0 iff the output changed" |
| D8 | Input loading: recognition, passwords, corruption | The SDK load result and error translation; a product supplies the engine call and an exception classifier | `InputLoadingOwnershipTests` |
| D9 | Read and write vocabulary | Planned: one object model per product, where the fields an operation writes are a subset of the read result with the same names and meanings | Planned: a generated operation-to-read field map and the invariant "read back what was written" |
| D10 | Human-readable output | SDK `ResultText` section helpers | `TableRendererCoverageTests`, `ProductRendererCoverageTests` |
| D11 | Structure | Three places per command paired on the menu; acyclic SDK layers; one-way product layers | `SdkLayeringTests` (SDK); analyzer `APCLI009` (product layers) |

## Gates

They judge a change without a reviewer reading it. Fix the product or the code to pass them; never
weaken a check or remove a supported operation to make a test pass.

- **Scenarios and invariants.** `tests/Aspose.Cli.TestKit/Scenarios` runs declarative
  `*.scenario.json` files (format in its README); add a reproduction or example to
  `tests/Aspose.Cli.Invariants.Tests/Scenarios`. The `*InvariantTests` generate cases
  from `capabilities` and each ops schema and check what every command keeps: no internal error,
  one valid error envelope, suggestions for mistakes, refused unwritable outputs, read-only
  commands and dry runs that write nothing, outputs that reopen, hidden secrets, and evaluation
  writes that warn `EVAL_MODE`.
- **Known violations.** `tests/Aspose.Cli.Invariants.Tests/known-violations.json` lists today's
  product defects by `cause`, per license mode (`modes`). A case passes only when its violations
  and their text match its entries, so a new, changed or fixed violation fails; delete an entry
  when its defect is fixed. Adding an entry or widening `modes` needs the owner's `quality-exception` label.
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
  (`APCLI011` forbids `StandardOptions`). An SDK option type (`PartRangeOption`,
  `PreviewOption`, `DetailOption`, the render options) exists only for options with the same name
  and meaning across commands; any other option is a plain `new Option<T>`, and `SameNameOptions`
  checks that same-named options agree. A description does not restate a default the parser
  applies.
- **Targets** in an operation outcome are capped at `BoundedOperationOutcome.MaximumTargets`. An
  operation that lists more reports its product's degenerate form instead: the document root
  address, or range addresses that cover every changed part (Words reports `blocks/<ranges>` and
  the changed `section/...` targets, and `document` when it addressed no range or that list is
  still too long). `BoundedOperationRunner` throws `InvalidOperationException` when a product
  without a degenerate form exceeds the cap.

## Tests

- xUnit v3 with real engines and CLI child processes. A test that takes several seconds carries
  `[Category(TestCategory.Slow)]`; installer and Playwright tests carry `Installer` and
  `Browser`.
- Tests call handlers directly with real requests, whose `Input` is `required`; production code
  carries no test shims. Generated schemas are not committed; the contract snapshots in
  `Integration/Snapshots` are split per product.
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
Git LFS. License-free products live in a separate repository; the license-free path still in
this one (`ProductBinding.CreateLicenseFree` and its Host branches) is planned for removal.
Code comments, diagnostics, documentation and Skills are English and describe present behavior.
Never add old-command aliases, legacy installer or Skill readers, historical version
baselines, historical trust allowlists, or migration frameworks for unpublished builds.
The application is pre-release: update code, schemas, Skills and tests together for an
intentional contract change.

Before changing code around a commercial SDK, verify the official API usage and reproduce
suspected engine behavior with a minimal SDK-only case. Correct our misuse in the owning
adapter. Record each confirmed SDK defect only in [KNOWN-ISSUES.md](KNOWN-ISSUES.md) as
its introduction describes, without reporting it upstream, and handle it openly with a
refusal, a workaround through other public API, or a warning. Never hide a defect with implicit
default rewrites, file-format patches, or product, producer or version special cases.
