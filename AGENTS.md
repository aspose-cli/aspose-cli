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

- Atomic publication, bounded extraction, resource budgets, worker cancellation and rollback.
- Loopback HTTP with exact Host, same-origin, CSRF and current-user service control.
- No network egress from documents unless the caller opts in.
- File publication and per-user install/PATH transactions holding resource-based
  interprocess locks, which other Aspose CLI distributions on the machine share.
- Licensed and evaluation behavior, including honest disclosure of evaluation output changes.
- Existing user files and unrelated worktree changes. Never clean unknown files.
- Files stay primary: never round-trip an entire document through JSON.

## Workflow

- Plan in the gitignored `.claude/`: `roadmap.md` is the task list, one line per task, and a
  multi-step task gets `plans/<branch>.md` with its goal, acceptance, what is out of scope and
  how it is verified. Knowledge that must last goes into this file, KNOWN-ISSUES.md or the pull
  request, not into these notes.
- One task is one branch in its own worktree, named as
  [CONTRIBUTING.md](CONTRIBUTING.md#pull-requests) requires. Never switch, edit or clean another
  session's worktree. A session may work on several tasks at once; each still gets its own
  branch from the latest `master` and its own pull request, opened as soon as that task
  passes. A task that needs another unmerged task waits for its merge, or branches from it
  and is rebased on `master` once it merges.
- Parallel subagents own disjoint files and commit in their own worktrees; integrate their
  commits with cherry-pick or merge, not patch files.
- Before opening the pull request, a reviewer in a fresh context checks the diff for
  correctness, scope, weakened or deleted tests, and dead code or stale docs the change left;
  fix only what is real. Add anything outside the task to `roadmap.md` instead of widening the
  pull request; a pull request description is not a task list. Start a session by reading it.
- Merge only when the owner says so, for one pull request or a named set of them, and as
  [CONTRIBUTING.md](CONTRIBUTING.md#pull-requests) orders merges. Afterwards delete the branch,
  its worktree and its plan.
## Conventions

Use only the corresponding commercial Aspose SDK packages; no FOSS source, gitlinks or
Git LFS. The CLI source license does not replace the SDKs' own terms.
Code comments, diagnostics, documentation and Skills are English and describe present behavior.
Never add old-command aliases, legacy installer or Skill readers, historical version
baselines, historical trust allowlists, or migration frameworks for unpublished builds.
The application is pre-release: update code, schemas, Skills and tests together for an
intentional contract change.

Before changing code around a commercial SDK, verify the official API usage and reproduce
suspected engine behavior with a minimal SDK-only case. Correct our misuse in the owning
adapter. Record each confirmed SDK defect once, under its id in
[KNOWN-ISSUES.md](KNOWN-ISSUES.md), with an SDK-only test that reproduces it
(`KnownIssue.Reproduces`); code that handles it (a refusal, a workaround through other public
API, or a warning) names the id in a comment. When an SDK update fixes the defect, its test fails:
delete the issue, its handling and its test together. Never hide a defect with implicit default
rewrites, file-format patches, or product, producer or version special cases.
