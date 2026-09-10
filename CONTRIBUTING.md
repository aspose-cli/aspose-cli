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

Use real engines and CLI child processes. Preserve atomic publication, budgets, rollback and service controls.
Do not weaken checks or remove a supported operation to obtain passing tests.
Code comments, diagnostics and public documentation are English.
Do not introduce old command aliases, historical trust allowlists or migration frameworks for unpublished builds.
Report sensitive defects through SECURITY.md.

Commercial SDK tests must remain valid in licensed and evaluation modes. Preserve evaluation disclosures.

Assess relevant shared defect reports independently; entire platform trees do not require synchronization.
