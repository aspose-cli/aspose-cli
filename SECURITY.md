# Security Policy

Report vulnerabilities privately through
[GitHub private vulnerability reporting](https://github.com/aspose-cli/aspose-cli/security/advisories/new),
never in a public issue. Name the affected revision and platform, and include a minimal synthetic
reproduction. Do not attach customer documents, passwords, tokens or license contents.

## Boundaries

Each boundary names the tests that check it, and `SecurityClaimsTests` keeps those names current.

- **Local services.** App and Preview listen only on `127.0.0.1` and require the exact Host
  header and bounded bodies; only the current user can control them. Every page, document and
  request needs the service's session cookie (HttpOnly, SameSite=Strict), which a browser gets
  only by opening an address the CLI returns: each carries a one-time entry ticket, valid for
  five minutes, that the service issues over its current-user control channel. State changes
  also need the service's exact Origin. Browsers do not keep cookies apart by port, so this does
  not protect against another user of the same machine who serves another port of `127.0.0.1`
  and gets your browser to open a page there once: the browser sends that user the session
  cookie. Their pages send `object-src 'none'` and `Cross-Origin-Resource-Policy: same-origin`;
  the App shell cannot be framed (`frame-ancestors 'none'`, `X-Frame-Options: DENY`) and the viewer
  only by its own origin (`frame-ancestors 'self'`, `SAMEORIGIN`). Rendered document parts run no
  scripts. The App control endpoint is one per configuration directory: a user who runs two
  configuration directories gets two services, each with its own endpoint.
  Checked by: `AppHttpSecurityTests`, `LocalHttpRequestSecurityTests`,
  `ViewerServiceHttpTests.Requests_AreRefusedUnlessTheyAreExactLoopbackReads`,
  `ViewerServiceHttpTests.DocumentPageAndParts_AreConfinedToTheirOrigin`,
  `AppCommandTests.App_WithADocument_MountsPreviewOnItsLoopbackOrigin`,
  `LocalServiceRuntimeTests.ControlProtocol_RejectsWrongTokenAndNonce`,
  `LocalServiceRuntimeTests.ViewerService_IsOnePerConfigurationDirectory`,
  `AppHttpSecurityTests.ServiceSecret_InASharedConfigurationDirectory_IsReadableOnlyByTheCurrentUser`,
  `LicenseInstallerTests.InstalledLicense_InASharedFolder_IsReadableOnlyByTheCurrentUser`,
  `LicenseManagementTests.InstalledLicense_InASharedConfigurationDirectory_IsReadableOnlyByTheCurrentUser`.
- **MCP.** `capabilities` is read-only. `execute` runs product commands and only the host
  commands that read or publish new outputs; it cannot install, update, change licenses or start
  and stop services.
  Checked by: `McpCommandTests.Server_ReportsReadOnlyDiscoveryAndMutatingExecution`,
  `McpCommandTests.Execute_AllowsExactlyTheseHostCommandsAndNamesThemWhenRejecting`,
  `McpCommandTests.Execute_RejectsPrivilegedAndLifecycleCommands`,
  `McpProtocolTests.Execute_UsesActualSyntaxPreservesHostDefaultsAndRejectsUnauthorizedCommands`.
- **Network.** Documents never cause network requests unless the caller opts in for trusted
  input. Every command passes one network gate before it runs, in every process that runs it,
  which blocks the http, https, ftp and file requests of the document engines; an opt-in opens
  http and https for that command alone. External resources are read only from ordinary local
  files beneath the input directory, and each omitted or blocked resource is reported; see
  [KNOWN-ISSUES.md](KNOWN-ISSUES.md).
  Checked by: `PdfResourceLoadingTests`, `CellsResourceLoadingTests`, `CellsSvgPictureTests`,
  `SlidesResourceLoadingTests`, `WordsResourceLoadingTests`, `SvgReferencesTests`,
  `NetworkGateTests`, `LocalDocumentResourceLoaderTests`, `VerifiedResourceReadTests`.
- **Files.** File routing is content-driven. Publication is atomic, extraction and inputs are
  bounded by resource budgets, and file publication and the per-user install and PATH state are
  guarded by interprocess locks. No command writes over its own input unless asked to edit in
  place.
  Checked by: `ProductFileRouterTests`, `AtomicOutputSetWriterTests`, `AtomicNewDirectoryWriterTests`,
  `SafeFileWriterTests`, `ResourceBudgetLedgerTests`,
  `PdfArtifactWorkflowTests.ExtractAttachments_LaterBudgetFailureRollsBackTheWholeSet`,
  `CrossProcessLockTests`,
  `CustomerInstallerPowerShellTests.Install_WhileAnotherProcessHoldsTheDirectoryLock_ChangesNothingUntilItIsReleased`,
  `OutputIsInputContractTests`, `PublicationOwnershipTests`.
- **Secrets.** License contents and passwords never enter logs or results. SDK evaluation
  effects are always disclosed.
  Checked by: `HostSecretTests`, `McpSecretExecutionTests`,
  `DecisionOwnershipTests.D5_SecretType_RedactsItsTextAndJson`,
  `LicenseManagementTests.LicenseContents_NeverEnterResultsOrDiagnostics`, `LicenseFingerprintTests`,
  `EvaluationMarkDisclosureTests`, `ReviewEvaluationDisclosureTests`, `EvaluationOwnershipTests`.

## Releases

Releases are published on GitHub over HTTPS. The installer and `aspose-cli update` check the
downloaded archive against the size and SHA-256 in the release's `RELEASE-MANIFEST.json`, and
every installed file against the package's `SHA256SUMS`; a mismatch installs nothing. This proves
integrity, not the publisher's identity, which rests on the GitHub repository. A release for
another product or runtime is rejected, as is a downgrade or a different build with the same
version. Only the installer's explicit `-DevelopmentPackage` switch, which
`scripts/install-local.ps1` passes, may replace any build.
