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
- Each Product owns its Contracts, Ports, Engine, Commands, Output, Schemas/v2, view adapter,
  Presenter, Skills and module definition, and its definitions are pure and deterministic.
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

## Must not break

- Every boundary in [SECURITY.md](SECURITY.md): local services, MCP, network, files and secrets.
- Worker cancellation and rollback.
- The resource-based interprocess locks, which other Aspose CLI distributions on the machine
  share.
- Licensed and evaluation behavior, including honest disclosure of evaluation output changes.
- Existing user files and unrelated worktree changes. Never clean unknown files.
- Files stay primary: never round-trip an entire document through JSON.

## Workflow

- Plan in the gitignored `.claude/`: `roadmap.md` is the task list, one line per task, read at
  the start of a session, and a
  multi-step task gets `plans/<branch>.md` with its goal, acceptance, what is out of scope and
  how it is verified. Knowledge that must last goes into this file, KNOWN-ISSUES.md or the pull
  request, not into these notes.
- Work in the repository's one checkout, without extra worktrees; one session changes it at a
  time. One task is one branch from the latest `master`, named as
  [CONTRIBUTING.md](CONTRIBUTING.md#pull-requests) requires, with its own pull request, opened
  as soon as that task passes. Commit the current work before switching branches; a pushed
  branch waits for CI while the next task starts. A task that needs another unmerged task
  waits for its merge, or branches from it and is rebased on `master` once it merges.
- Subagents never change the checkout at the same time: one implements at a time, and
  reviewers only read.
- Before opening the pull request, a reviewer in a fresh context checks the diff for
  correctness, scope, weakened or deleted tests, a code change's test plan without a licensed
  `Affected` run, and dead code or stale docs the change left;
  fix only what is real. Add anything outside the task to `roadmap.md` instead of widening the
  pull request; a pull request description is not a task list.
- Merge once that review finds nothing blocking and the required checks pass, in the order
  [CONTRIBUTING.md](CONTRIBUTING.md#pull-requests) gives; never bypass a check. Afterwards
  delete the branch and its plan.

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
[CONTRIBUTING.md](CONTRIBUTING.md#known-sdk-issues) describes, and handle it openly with a
refusal, a workaround through other public API, or a warning. Never hide a defect with implicit
default rewrites, file-format patches, or product, producer or version special cases.
