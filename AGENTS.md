# Aspose CLI repository guide

An independent open-source CLI whose executable is `aspose-cli`; Windows x64 is the only
supported platform. `eng/products.json` is the only product roster and `eng/distribution.json`
the fixed distribution identity; the generated projections and the solution come from
`scripts/sync.ps1` and are never hand-edited. How to build and test is in
[CONTRIBUTING.md](CONTRIBUTING.md); the security boundary is in [SECURITY.md](SECURITY.md).
This guide holds only what the code and its checks cannot tell you; where a check owns a rule,
the check is its authority.

## Boundaries

- Launcher -> Host -> SDK; launcher -> active Products; Products -> SDK, never another Product.
  SDK types of a document engine stay inside its Product's Engine adapter. Enforced by
  `CommercialDependencyRulesTests`, `RepositoryProjectGraphTests`, `DependencyRulesTests`,
  `EngineSdkIsolationTests` and analyzers `APCLI006`, `APCLI009` and `APCLI010`.
- Inside a Product, `APCLI009` keeps Contracts <- Commands and Contracts <- Engine, and inside
  the Engine `Engine.Mapping` (loading, format and vocabulary names, read projections,
  addresses) <- `Engine.Editing` (operation handlers, snapshots, verification): what both read
  and write lives in Mapping. The module's namespace is the composition root; the layers reach
  it only for its build metadata, diagnostics, budget domains, format recognition and manifest.
- Host carries no product-specific branch: product ids come from the catalog, and distribution
  metadata identifies this executable (`DependencyRulesTests`, `HostCatalogIsolationTests`).
- A Product's module definitions are pure and deterministic (`APCLI007`). Its schemas are
  generated from its records and their documentation comments; nothing hand-writes a schema.
- What every product shares lives once, in the Host's `aspose-cli-platform` Skill; a product
  Skill holds only product knowledge and points to the generated schema instead of restating it
  (`PlatformSkillPointerTests`).
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
- The command tree built from the product menus is the registry: capabilities, help, schemas and
  documentation checks are projections of it, so keep no separate registration data. A product
  session holds only the write pipeline, the resource budgets and the loader; a product-specific
  concern that wraps every handler is a `.Guard` on the module.
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
| D1 | Output: path, format, overwrite, encryption, part names, staging, reported file objects | The SDK resolves a `ResolvedOutput`; SDK `OutputProtection` decides the password an output carries (PDF: `PdfOutputEncryption`); SDK `FileObjects` builds the `input` and `output` objects; SDK `FileWritePrecondition.Admit` admits an edit's loaded input (unchanged while loading, then `--if-match`); only the SDK write pipeline warns `ENCRYPTION_REMOVED` | `DecisionOwnershipTests` D1; invariant C-R41 and the compiler (`FileFingerprints.Ensure*` is internal) |
| D2 | Format capabilities | Each product's Contracts `*Formats` descriptor tables (`Definitions` and side tables such as `PdfFormats.Certificates`), mapped to the engine by its `*EngineFormats`; product commands and engines compare no value with a format id; no product source file reads a file extension, which only the SDK matches against the tables (`FormatDescriptorExtensions.NamedBy`) | `CellsEngineFormatTests`, `PdfEngineFormatTests`, `SlidesEngineFormatsTests`, `WordsEngineFormatsTests`; `DecisionOwnershipTests` D2 (format id literals) and D1 `D1_Products_DeriveNoFormatFromAnExtension` (extensions, no exceptions) |
| D3 | Mistake suggestions | SDK `Mistake` | `DecisionOwnershipTests` D3; invariants |
| D4 | Error construction | SDK error factories | `DecisionOwnershipTests` D4; `FreeTextErrorFactoryTests` |
| D5 | Secrets | SDK `Secret`, revealed only in an engine adapter; the Host's frames and files carry `WireSecret` | `DecisionOwnershipTests` D5; `HostSecretTests` |
| D6 | Evaluation detection and disclosure | The SDK write pipeline; a product supplies only its evaluation-mark recognizer and, for an extraction, its `ExtractionContent` | `EvaluationOwnershipTests`; the evaluation invariant |
| D7 | Truthful results: affected counts, targets, pages, no match | The SDK runner, from the snapshots of the product's `IOperationModel` and, with `--verify`, its `IEditVerifier`; only the SDK constructs `AppliedOperation` | `OperationSnapshotTests`, `BoundedOperationModelTests`; invariant C-R30 |
| D8 | Input loading: recognition, passwords, corruption | The SDK load result and error translation; a product supplies the engine call and an exception classifier | `InputLoadingOwnershipTests` |
| D9 | Read and write vocabulary | Each operation field's read-back declaration, published as `x-readBack`. Planned: one object model per product, where the fields an operation writes are a subset of the read result with the same names and meanings | `APCLI012`; invariants C-R21, C-R39 |
| D10 | Human-readable output | SDK `ResultText` | `ResultTextTests`, the product renderer golden tests, `TableRendererCoverageTests`, `ProductRendererCoverageTests` |
| D11 | Structure | Three places per command (Contracts, Commands, Engine handler) paired on the product menu; one-way SDK layers, IO checked by its sub-areas; one-way Host subsystem layers; one-way product layers | `SdkLayeringTests`, `HostLayeringTests`, `APCLI009`, `ContractFileLayoutTests`, `ProductMenuCompletenessTests` |
| D12 | Path case: whether two local paths are the same, one is within another, or one is a UNC or device path | SDK `FilePaths` (`Comparer`, `Comparison`, `Same`, `IsWithin`, `IsRemoteOrDevice`), and the lock keys and the license identity derived from a path, whose bytes other distributions share (`LockKey`, `FoldedKey`) | `DecisionOwnershipTests` D12; `PathCaseTests`; `LockKeyBytesTests` |
| D13 | Process identity and liveness: a process's start time, and whether a recorded process still runs | SDK `ProcessIdentity`; each caller states its own `ProcessLivenessPolicy` (access denied, vanishing, exit check, start tolerance, unknown start) | `DecisionOwnershipTests` D13; `ProcessIdentityTests` |

## Gates

They judge a change without a reviewer reading it. Fix the product or the code to pass them; never
weaken a check or remove a supported operation to make a test pass.

- **Scenarios and invariants.** Add a reproduction or example as a `*.scenario.json` in
  `tests/Aspose.Cli.Invariants.Tests/Scenarios` (format in
  `tests/Aspose.Cli.TestKit/Scenarios/README.md`). The `*InvariantTests` generate their cases from
  `capabilities` and each ops schema.
- **Known violations.** `tests/Aspose.Cli.Invariants.Tests/known-violations.json` lists today's
  product defects, and `generated-cases.txt` beside it every generated case id
  (`KnownViolationsTests`, `GeneratedCaseListTests`). Delete an entry when its defect is fixed.
  Adding an entry, widening its `modes`, or deleting a case id needs the owner's
  `quality-exception` label.
- **Rule exemptions.** A test's list of cases a rule excuses (such as `RoundTripExempt`,
  `SameNameOptions.Exemptions` or a product's `Homonyms`) sits between
  `// rule-exemptions: begin` and `// rule-exemptions: end` lines in a file `.github/CODEOWNERS`
  names. Adding or editing an exemption, taking one out of its block, or removing or moving a
  marker is adding a known violation and needs the same label; deleting an exemption does not.
  `scripts/require-approval.ps1` checks both lists for the pull request.
- **Analyzers.** Production code builds with the analyzers in the `[src/**.cs]` section of
  `.editorconfig` as errors; fix a violation rather than suppressing it.

## Contracts

- **Operations** are records with `[Operation("name")]` under the product's
  `[OperationVocabulary]` base. The record is the contract: attributes state its constraints, and
  a `Validated()` override, stated in its summary, the rest. Summaries are the schema's
  descriptions. Every field declares its read-back (D9); a selector, a source or a parameter is
  `[NotState]`, and so are the drawing parameters of an operation that leaves no object the file
  can be read back by. `APCLI012` rejects an incomplete contract. What every operation is
  classified by — its address or targets, its footprint and protection scope, whether
  `--track-changes` admits it, whether the verification compares its state — dispatches through
  the generated `I{Vocabulary}Handler<T>` visitor with one method per operation and no default
  branch, so a new operation does not compile until it is placed (`CellsOpFootprints`,
  `WordsOpAddresses`, `PdfEditVerifier.Recording`). A no-effect explanation that only a few
  operations have (`Explain`, the Words review authors) is not such a classification.
- **Results** pass their relative schema id and version to a result envelope; ids are
  `v2/common/` for the SDK and Host, `v2/<product>/` for a product. `APCLI013` rejects a record it
  cannot describe. An edit's `verification` derives from the SDK `EditVerification` core and
  declares only the product's evidence.
- **JSON input** defaults are tested through the production source-generated serializer,
  including omitted fields and explicit `false`, `0` and `null`. It
  [does not preserve init-only initializers](https://github.com/dotnet/runtime/issues/84484), so an
  input record outside an operation vocabulary takes scalar defaults as optional constructor
  parameters.
- **Error codes** shared by products or the Host are declared once in the SDK's `ErrorCodes`; a
  product declares its own in its `*Diagnostics` class (`ProductDiagnosticDeclarationTests`). A
  missing target is built with `CliErrors.NotFound` or `CliErrors.NotFoundAt`; a name several
  targets share is refused.
- **Command parameters** declare their input role, value sources and secret handling on the
  symbol; the Host reads only these declarations. Reserved names are checked by `APCLI008`, and
  `APCLI011` keeps products off `StandardOptions`. An SDK option type (`PartRangeOption`,
  `SampleOption`, `DetailOption`, `BomOption`, the render options) exists only for options with
  the same name and meaning across commands; any other option is a plain `new Option<T>`.
  Same-named options agree (`SameNameOptions`, C-R07), a bounded integer option uses `InRange`
  and `GetInRange` (`OptionRangeHelpTests`), and a description does not restate a default the
  parser applies.
- **Targets** in an operation outcome are `<unit>/<address>` addresses, at most
  `BoundedOperationOutcome.MaximumTargets`; beyond that an outcome reports one range per unit that
  covers them all, such as `page/1-150` (`<unit>/all` for named objects). The SDK derives targets
  and counts from the product's snapshots; a product counts nothing. An object is numbered by its
  position in the output of the whole batch, or in its input when the batch removed it, so a
  removed object and a changed one can share an address and the targets list one fewer than
  `itemsAffected`. The number comes from the instance the product names in `Positions()` (a Words
  block or section, a PDF page, a Slides slide, which also numbers `slide/3/shape/7` and
  `page/1/image/1`); numbered objects without one keep the number their operation saw: the Cells
  objects numbered within a sheet (`chart/Sheet/2`, `image/…`, `conditional-format/…`) and the
  PDF bookmarks and page labels. Names in addresses are escaped only by `TargetName.Escape`,
  whose summary states what it escapes (`TargetNameTests`, `TargetNameOwnershipTests`).
  A snapshot's state may hold what no read result reports, such as the PDF `Unreported`
  properties: it only decides whether an object changed, enters no result, and so needs no
  read-back.

## Contract conventions

The command-line contract aims to let an agent guess right from the habits it already has: POSIX
and GNU argument conventions, the Unix split of results on stdout and diagnostics on stderr, the
shared vocabulary of git, kubectl and gh, and [clig.dev](https://clig.dev/). Follow the common
practice, not an older tool that breaks it. The rules are numbered `C-Rnn`; their mechanical
parts are checked by `tests/Aspose.Cli.Invariants.Tests/Rules`, which is their authority, and
today's violations are entries in `known-violations.json` until a contract change clears them.
This section holds only what a test cannot judge.

- **Verbs mean one thing (C-R02).** A product verb does the same kind of work in every product.
  A missing verb is a gap to fill, never a reason to give another verb a second meaning.
- **Plural nouns (C-R19).** An operation whose noun is plural (`delete_rows`) acts on a set;
  a singular noun acts on one object.
- **Units in names (C-R20).** A field or option measured in anything but points names its unit
  (`widthChars`, `scalePercent`, `durationMs`, `dpi`). A measured op field declares its unit with
  `[Measured]`, which the rule reads and the schema does not publish.
- **One name, one shape (C-R20).** A field name has one type within and across products, except
  a field that takes the shape of the object it names (`ShapedByTheirObject` in `SchemaRules`);
  adding a name to those exceptions is a decision, not a default.
- **One fact, one code (C-R28).** Two codes never name the same fact, and one code never covers
  two: `ANCHOR_NOT_FOUND` (a search found nothing) and `BLOCK_NOT_FOUND` (a block number beyond
  the document) stay separate.
- **Skills claim only what is tested (C-R38).** What a Skill says about a result's meaning has a
  behavior check behind it. A product Skill need not show every command.

Kept on purpose:

- Commands are product first, `aspose-cli <product> <verb>`, not verb first: each product owns its
  verbs, options and formats, and the top level holds only the commands that serve every product
  (C-R01 checks it).
- `capabilities`, `schema` and `docs` stay three self-description entry points: the command tree,
  the JSON Schemas and the guides (C-R01).
- The positional argument of `create` is the file to create (C-R05).
- `--set` keeps one address syntax per product, `SHEET!CELL=VALUE` in Cells and
  `bookmark:NAME=TEXT` in Words; `--author` means the same in both of its places, and `--port`
  keeps the two descriptions of its two commands (C-R07). The other per-product differences are
  the exemptions in `SameNameOptions`, each with its reason.
- `--grid` is PDF only and `--width` Slides only; which commands take `--font-dir` is decided by
  `FontDirectoryOptionsTests` (C-R08).
- `--timeout` takes seconds without a unit in its name, as command-line timeouts do (C-R20).
- `--scan-range` stays hidden, only the carrier of `window.next` (C-R04).
- In JSON mode warnings are part of the stdout envelope, and redirected output defaults to JSON
  (C-R23).
- `compare` exits 0 when the files differ; the differences are its result (C-R24).
- Help and the version are `--help` and `--version` only; no `help` or `version` subcommand
  (C-R35).
- `capabilities` lists no `examples[]` or `errorDetailsKeys[]`: examples belong to help (C-R34),
  and the error `details` keys are described once, in the error envelope schema (C-R25).
- On an encrypted PDF input, `set_properties` with text beyond Latin-1 and no later
  `encrypt_document` or `--encrypt-env` is still refused rather than reported applied while the
  engine writes garbled text ([PDF-ENCRYPTED-INFO-TEXT](KNOWN-ISSUES.md#pdf-encrypted-info-text),
  C-R30).

## Tests

- Tests call handlers directly with real requests; production code carries no test shims.
- A test that changes process-wide state joins its serial collection in
  `tests/TestAssemblyFixture.cs`. A test project that reads a repository file outside the
  projects lists it as a `RepositoryInput` item (`RepositoryInputTests`).
- Slow, installer and browser tests carry their category; `scripts/test.ps1` reports an
  uncategorized test over its slow-test limit.
- Keep license contents out of logs and fixtures.

## Must not break

- Every boundary in [SECURITY.md](SECURITY.md): local services, MCP, network, files and secrets.
- Worker cancellation and rollback.
- The resource-based interprocess locks, which other Aspose CLI distributions on the machine
  share.
- Licensed and evaluation behavior, including honest disclosure of evaluation output changes.
- Existing user files and uncommitted changes you did not make. Never clean unknown files.
- Files stay primary: never round-trip an entire document through JSON.
- The update handoff, frozen for every version after 1.0 because each installed version updates
  from the latest release: the `RELEASE-MANIFEST.json` at `releases/latest` keeps
  `schemaVersion: 1` and its required fields (`productId`, `runtimeIdentifier`, `artifactVersion`,
  `sourceRevision`, `archive.path`, `archive.size`, `archive.sha256`), which a release may only add
  to (`ReleaseManifest` ignores other fields); the archive keeps `install.ps1` at its root, which
  `update install` runs with `-PackageRoot`, `-InstallDirectory`, `-Update`, `-WaitForProcessId`,
  `-CleanupRoot` and `-StatusPath`; an installed executable's `preview stop` without an id, which
  `install.ps1` runs to close every document and stop the service before it uninstalls that
  installation; and the status file keeps `schemaVersion: 1`, `state`
  (`pending`, `running`, `succeeded`, `failed`), `installerProcessId`, `message`, `log` and
  `updatedAt` (`UpdateStatus`, `UpdateHandoffTests`).

## Conventions

Use only the corresponding commercial Aspose SDK packages; no FOSS source, gitlinks or
Git LFS. License-free products live in a separate repository; every product here is licensed.
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
