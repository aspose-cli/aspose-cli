# Security Policy

Report vulnerabilities privately through
[GitHub private vulnerability reporting](https://github.com/aspose-cli/aspose-cli/security/advisories/new),
never in a public issue. Name the affected revision and platform, and include a minimal synthetic
reproduction. Do not attach customer documents, passwords, tokens or license contents.

## Boundaries

- **Local services.** App and Preview listen only on `127.0.0.1` and require the exact Host
  header, same-origin requests, CSRF tokens and bounded bodies; only the current user can control
  them. Their pages send `object-src 'none'` and `Cross-Origin-Resource-Policy: same-origin`; the
  App shell cannot be framed (`frame-ancestors 'none'`, `X-Frame-Options: DENY`) and the viewer
  only by its own origin (`frame-ancestors 'self'`, `SAMEORIGIN`). Rendered document parts run no
  scripts. The App control endpoint is one per user, whatever configuration directory is chosen.
- **MCP.** `capabilities` is read-only. `execute` runs product commands and only the host
  commands that read or publish new outputs; it cannot install, update, change licenses or start
  and stop services.
- **Network.** Documents never cause network requests unless the caller opts in for trusted
  input. External resources are read only from ordinary local files beneath the input
  directory, and each omitted resource is reported. Input that an SDK importer would fetch
  before any policy applies is refused; see [KNOWN-ISSUES.md](KNOWN-ISSUES.md).
- **Files.** File routing is content-driven. Publication is atomic, extraction and inputs are
  bounded by resource budgets, and file publication and the per-user install and PATH state are
  guarded by interprocess locks. No command writes over its own input unless asked to edit in
  place.
- **Secrets.** License contents and passwords never enter logs or results. SDK evaluation
  effects are always disclosed.

## Releases

Releases are published on GitHub over HTTPS. The installer and `aspose-cli update` check the
downloaded archive against the size and SHA-256 in the release's `RELEASE-MANIFEST.json`, and
every installed file against the package's `SHA256SUMS`; a mismatch installs nothing. This proves
integrity, not the publisher's identity, which rests on the GitHub repository. A release for
another product or runtime is rejected, as is a downgrade or a different build with the same
version. Only the installer's explicit `-DevelopmentPackage` switch, which
`scripts/install-local.ps1` passes, may replace any build.
