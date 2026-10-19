# Contributing to Aspose CLI

Only collaborators open pull requests; others report problems and proposals through an issue, and
vulnerabilities through [SECURITY.md](SECURITY.md). Code rules are in [AGENTS.md](AGENTS.md).

## Prerequisites

Windows x64 with a .NET 10 SDK at or above the version in `global.json`, PowerShell 7.4 or later
(`pwsh`), Windows PowerShell 5.1 (it runs the customer installer) and access to the Playwright CDN
(the browser tests download a pinned Chromium).

## Build and test

`scripts/test.ps1` builds once and runs the tests; its help describes the scopes. A licensed run
needs `ASPOSE_CLI_TEST_LICENSE_PATH` set to a license file. CI runs the `Changed` scope without
a license, so before opening a pull request run the licensed cases it skips:

```powershell
.\scripts\test.ps1 -Configuration Release -Scope Changed -Base origin/master
```

Run `-Scope Full` before a release and after an SDK update; `-CiLike` reproduces a CI failure
locally. After a catalog, identity or dependency change, run `scripts/sync.ps1`. After an intended
change to help, capabilities or schemas, rerun `CliContractTests` with
`ASPOSE_CLI_TEST_UPDATE_SNAPSHOTS=1` and review the snapshot diff.
The invariant tests run their command lines in warm `aspose-cli` worker processes;
`ASPOSE_CLI_TEST_WORKERS=0` runs each as a child process instead, and `ASPOSE_CLI_TEST_WORKERS=n`
sets the number of workers.
`scripts/install-local.ps1` installs a development build.

A test that a change adds or edits must fail without the change's `src/` code.
`scripts/test-fails-before.ps1` checks this for the committed change (HEAD against its merge base
with `origin/master`, or `-Commit <sha>` against its parent): in a temporary worktree it reverts
`src/`, keeps the tests, and runs the `[Fact]` and `[Theory]` methods the diff touches with and
without the license; it exits non-zero and names every test that still passes and every filter
term that selects no test. A test project that fails to build only with compiler errors under
`tests/` counts as failing, but only when another selected test also runs and fails, since a
compiler error alone proves nothing about behavior; any other build failure, including the reverted `src/` itself, leaves
the change unjudged and the script exits non-zero (pass a `-Base` whose build works). A rule id
such as `C-R39` on a changed line of an invariant rule selects that rule's cases; any other change
outside test methods (helpers, fixtures, snapshots) fails the check as unselected until `-Filter`
and `-TestProject` name the tests that cover it (for example
`-Filter 'DisplayName~C-R39' -TestProject Aspose.Cli.Invariants.Tests`). `-Plan` lists the
selection without building; `-BuildParallelism` and `-TestThreads` bound the processor use.
The script is for tests that pin a fix or new behavior. A refactor that must not change behavior
commits its protective tests first, where they pass on the old code, and they still pass after
the change; `scripts/test-fails-before.ps1` does not judge them.

## Pull requests

- **Branch:** `<type>/<kebab-case-summary>` or `stage/<kebab-case-summary>` from the latest
  `master`. Rebase on `master`, never merge it in.
- **Updates:** change a pull request by pushing to its branch; after a rebase or a folded fix,
  push with `--force-with-lease`. CI reruns on every push.
- **Title and every commit subject:** `<type>(<scope>): <summary>`, imperative, lower case, no
  final period, at most 85 characters. Types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`,
  `build`, `ci`, `chore`, `revert`. Optional scopes: `sdk`, `host`, `cli`, `app`, `cells`, `pdf`,
  `slides`, `words`, `skills`, `install`, `release`, `deps`.
- **Commits:** every commit builds, review fixes are folded into the commits they fix, and a pull
  request holds at most 100 commits (the most GitHub can rebase-merge).
- **Merge:** by rebase, after the required `verify` and `conventions` checks pass.
- **A failure the change cannot reach** may be flaky: rerun the failed job once. If it passes, make
  the test reliable by relaxing only its own timing, never a product check.
