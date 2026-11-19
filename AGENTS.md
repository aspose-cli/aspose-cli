# Aspose CLI repository guide

An independent open-source CLI whose executable is `aspose-cli`; Windows x64 is the only
supported platform. `eng/products.json` is the only product roster and `eng/distribution.json`
the fixed distribution identity; the generated projections and the solution come from
`scripts/sync.ps1` and are never hand-edited. How to build and test is in
[CONTRIBUTING.md](CONTRIBUTING.md); the security boundary is in [SECURITY.md](SECURITY.md).
This guide holds only what the code and its checks cannot tell you; where a check owns a rule,
the check is its authority.

## What the tool is

The caller is an AI agent such as Claude Code or Codex. It has its own judgment, reads the
results and retries, and no run of it is deterministic. The CLI is a file-format tool and
nothing more. It makes three promises, and each holds whatever a document contains:

1. **Read.** What a file holds, as the engine reads it.
2. **Write.** Only the change that was named. A target that does not exist is an error, a
   parameter that was not given is not set, and an engine error is reported as it is. After the
   write the CLI reads the changed place back from the saved file and returns it, without a
   verdict.
3. **Convert and render.** The engine's conversion and rendering, with every option stated by
   the caller.

Most earlier defects came from promises of another kind: a warning, a hint, a check or a
fallback that had to be right for every document, and each fix added one more.

## Boundary

| Who | Owns |
|---|---|
| The engine (the Aspose SDK) | How a file is parsed, what the object model holds, what else a property changes when it is set, what a save or a conversion loses, how a page renders |
| The agent | What to change and in which order, whether the result is good, what to do next, how to recover from its own mistake, how to combine steps |
| The CLI | The command line, opening and saving, one table of names, the report |

- **The command line**: a fixed vocabulary, checks of an argument's form, errors that state facts.
- **Opening and saving**: paths, passwords and the license; a write goes to a temporary file that
  then replaces the target, never over a file the caller did not allow; no network and no file
  outside the input's folder.
- **The table**: a name for an engine object or member. A read gets the member and a write sets
  it, under the same name.
- **The report**: the exit code and what was observed.

A feature or a fix is not the CLI's when any of these is true:

1. Whether it is right depends on what the document contains. Returning what was read does not.
2. It speaks of the future or of intent: what will happen, what should, what to try next.
3. It mends, hides or changes what the engine does.
4. The agent can do it with two or three calls the CLI already has.

So the CLI does not verify or review a result, predict one, suggest a next step or a corrected
name, infer a type, a target or a default, combine several engine calls into one operation, work
around the engine, serve a user interface, update itself, or teach how to design a document.

Only two things are added. A **row** of the table is one name for one engine member, which is
read, or read and set. An **action** is one name for one engine call whose parameters the caller
states. Whatever needs an "if" about the document to work is not added. A row returns what the
engine returns, untrimmed, sets only its own member, and writes its value in one fixed form.

## Defects

Sort every report before changing code.

| Kind | What to do |
|---|---|
| The engine's behavior | One entry in [KNOWN-ISSUES.md](KNOWN-ISSUES.md) with a minimal SDK-only reproduction, reported to the SDK team. No refusal, workaround or warning in the CLI |
| The agent's mistake | No code; at most a sentence of documentation |
| A promise the CLI broke: a read that differs from the engine's, a write to a member other than the named one, a change nobody named, an error that was not reported | Fix it by correcting or deleting code, never by adding a warning, a hint or a check |

Before changing code around an engine, verify the official API and reproduce the behavior with a
minimal SDK-only case.

## Version 2.0 is in progress

`master` still holds what this direction removes, each part until the change that deletes it:
`--verify` and `--dry-run`; per-operation snapshots,
verifiers and warnings; operations that choose for the caller; workarounds for engine defects.
Do not extend or repair these parts, except for a wrong result. 2.0 keeps no compatibility with
1.x: delete cleanly, with no alias, compatibility layer or transitional path.

## Structure

- Launcher -> Host -> SDK; launcher -> active Products; Products -> SDK, never another Product.
  The types of a document engine stay inside its Product's Engine adapter.
- Host carries no product-specific branch: product ids come from the catalog.
- A product on the object table declares its rows, locators, adds and actions in its
  `Engine.Mapping`; the SDK's `Tables` owns the batch, the read-back from the saved file, the read,
  the search and the schema written from the table.
- What Products share lives once in the SDK, and what their Skills share once in the Host's
  `aspose-cli-platform` Skill. A decision has one owner; the architecture tests refuse a second.
- Extend at compile time through the catalog and explicit registration: no runtime plugin
  loading, reflection scanning, dependency-injection container or command source generator.
- Schemas, help and capabilities are generated from one source; nothing hand-writes a copy.
- Source, project references, build imports, tests and publishing tools stay inside this
  project. Never reference a parent workspace or another CLI project.
- Do not adopt raw XML or XPath write layers, a whole-document export replayed as a batch,
  in-memory sessions with deferred writes, partially successful batches, out-of-process plugins,
  or a home-grown renderer or formula engine. The table is explicit typed rows, not a string
  property bag, and a read's output is a view, never the input of a replay.
- The process's culture is pinned to en-US at startup, so a result does not change with the
  machine's regional settings; a command that needs another culture takes it as an option.

## Contract

The command line follows the habits an agent already has: POSIX and GNU argument conventions,
results on stdout and diagnostics on stderr, the vocabulary of git, kubectl and gh. Commands are
product first, `aspose-cli <product> <verb>`. The mechanical rules are numbered `C-Rnn` and
checked by `tests/Aspose.Cli.Invariants.Tests/Rules`, which is their authority. What a test
cannot judge:

- A verb does the same kind of work in every product.
- A name, a value and a unit follow the engine's object model; a field measured in anything but
  points names its unit.
- A field name has one type and one meaning within and across products.
- Two error codes never name the same fact, and one code never covers two.
- A Skill describes the tool, and says of a result only what a check establishes.

## Gates

- Never weaken a check or add a known violation to make a change pass. Adding or widening an
  entry of `known-violations.json`, deleting a generated case id, or adding a rule exemption
  needs the owner's `quality-exception` label.
- Production code builds with the analyzers as errors; fix a violation rather than suppress it.
- A reproduction or an example is a `*.scenario.json` in
  `tests/Aspose.Cli.Invariants.Tests/Scenarios`. Tests call handlers directly with real requests;
  production code carries no test shims.
- A test that changes process-wide state joins its serial collection in
  `tests/TestAssemblyFixture.cs`. Keep license contents out of logs and fixtures.

## Must not break

- Every boundary in [SECURITY.md](SECURITY.md): network, files and secrets.
- Existing user files and uncommitted changes you did not make. Never clean unknown files.
- Files stay primary: never round-trip an entire document through JSON.
- A run without a license says so in its result.

## Conventions

Use only the corresponding commercial Aspose SDK packages; no FOSS source, gitlinks or
Git LFS. License-free products live in a separate repository; every product here is licensed.
Code comments, diagnostics, documentation and Skills are English and describe present behavior.
