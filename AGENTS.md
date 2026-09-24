# Aspose CLI repository guide

A complete independent open-source CLI project whose executable is `aspose-cli`; Windows x64
is the only supported platform. `eng/products.json` is the only product roster and
`eng/distribution.json` the fixed distribution identity; generated projections and the
solution come from `scripts/sync.ps1` and are never hand-edited. Build, test and code
conventions are in [CONTRIBUTING.md](CONTRIBUTING.md); the security boundary is in
[SECURITY.md](SECURITY.md).

## Boundaries

- Launcher -> Host -> SDK; launcher -> active Products; Products -> SDK. Products never reference another Product.
- SDK types from a document engine stay inside its matching Product Engine adapter, in
  signatures and method bodies alike; analyzers and architecture tests enforce this.
- Source, project references, build imports, tests and publishing tools stay inside this
  project. Never reference the parent workspace or the sibling CLI project.
- Host carries no product-specific branch. Product ids come from the catalog, and
  distribution metadata identifies this executable.
- Each Product owns its Contracts, Ports, Engine, Commands, Output, Schemas/v2, view adapter,
  Presenter, Skills and module definition, and its definitions are pure and deterministic.
  Skill names begin with `aspose-cli-`, their executable examples must match this build's
  capabilities, and package-relative documentation links must resolve.

## Must not break

- Atomic publication, bounded extraction, resource budgets, worker cancellation and rollback.
- Loopback HTTP with exact Host, same-origin, CSRF and current-user service control.
- File publication and per-user install/PATH transactions holding resource-based
  interprocess locks, which other Aspose CLI distributions on the machine share.
- Licensed and evaluation behavior, including honest disclosure of evaluation output changes.
- Existing user files and unrelated worktree changes. Never clean unknown files.
- Files stay primary: never round-trip an entire document through JSON.

## Conventions

Use only the corresponding commercial Aspose SDK packages; no FOSS source, gitlinks or
Git LFS. The CLI source license does not replace the SDKs' own terms.
Code comments, diagnostics, documentation and Skills are English.
Never add old-command aliases, legacy installer or Skill readers, historical version
baselines, historical trust allowlists, or migration frameworks for unpublished builds.
The application is pre-release: update code, schemas, Skills and tests together for an
intentional contract change.

Before changing code around a commercial SDK, verify the official API usage and reproduce
suspected engine behavior with a minimal SDK-only case. Correct our misuse in the owning
adapter. Keep confirmed SDK defects as upstream issues and release blockers, recorded in
[KNOWN-ISSUES.md](KNOWN-ISSUES.md) and, where it can be reproduced, guarded by an acceptance
gate or a `ProductDefect` test; never
hide them with implicit default rewrites, file-format patches, or product, producer or version
special cases.
