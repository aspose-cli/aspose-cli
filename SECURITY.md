# Security Policy

Report vulnerabilities privately through
[GitHub private vulnerability reporting](https://github.com/aspose-cli/aspose-cli/security/advisories/new),
never in a public issue. Name the affected revision and platform, and include a minimal synthetic
reproduction. Do not attach customer documents, passwords, tokens or license contents.

## Boundaries

Each boundary names the tests that check it, and `SecurityClaimsTests` keeps those names current.

- **Network.** Documents never cause network requests unless the caller opts in for trusted
  input. Every command passes one network gate before it runs, in every process that runs it,
  which blocks the http, https, ftp and file requests of the document engines; an opt-in opens
  http and https for that command alone. External resources are read only from ordinary local
  files beneath the input directory, and each omitted or blocked resource is reported; see
  [KNOWN-ISSUES.md](KNOWN-ISSUES.md).
  Checked by: `PdfResourceLoadingTests`, `CellsResourceLoadingTests`, `CellsSvgPictureTests`,
  `SlidesResourceLoadingTests`, `WordsResourceLoadingTests`, `SvgReferencesTests`,
  `NetworkGateTests`, `LocalDocumentResourceLoaderTests`, `VerifiedResourceReadTests`,
  `IsolationMatrixTests`, `OutputScanTests`.
- **Files.** File routing is content-driven. Publication is atomic, extraction and inputs are
  bounded by resource budgets, and the per-user install and PATH state are guarded by
  interprocess locks. No command writes over its own input unless asked to edit in place.
  Checked by: `ProductFileRouterTests`, `AtomicOutputSetWriterTests`, `AtomicNewDirectoryWriterTests`,
  `SafeFileWriterTests`, `ResourceBudgetLedgerTests`,
  `PdfArtifactWorkflowTests.ExtractAttachments_LaterBudgetFailureRollsBackTheWholeSet`,
  `CustomerInstallerPowerShellTests.Install_WhileAnotherProcessHoldsTheDirectoryLock_ChangesNothingUntilItIsReleased`,
  `OutputIsInputContractTests`, `PublicationOwnershipTests`, `OwnershipMatrixTests`.
- **Secrets.** License contents and passwords never enter logs or results, and an installed
  license file is readable only by the current user. SDK evaluation effects are always disclosed.
  Checked by: `HostSecretTests`,
  `DecisionOwnershipTests.D5_SecretType_RedactsItsTextAndJson`,
  `LicenseManagementTests.LicenseContents_NeverEnterResultsOrDiagnostics`,
  `LicenseInstallerTests.InstalledLicense_InASharedFolder_IsReadableOnlyByTheCurrentUser`,
  `LicenseManagementTests.InstalledLicense_InASharedConfigurationDirectory_IsReadableOnlyByTheCurrentUser`,
  `EvaluationMarkDisclosureTests`, `EvaluationOwnershipTests`.

## Releases

Releases are published on GitHub over HTTPS. The installer checks the downloaded archive against
the size and SHA-256 in the release's `RELEASE-MANIFEST.json`, and every installed file against
the package's `SHA256SUMS`; a mismatch installs nothing. This proves
integrity, not the publisher's identity, which rests on the GitHub repository. A release for
another product or runtime is rejected, as is a downgrade or a different build with the same
version. Only the installer's explicit `-DevelopmentPackage` switch, which
`scripts/install-local.ps1` passes, may replace any build.
