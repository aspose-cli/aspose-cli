# Security Policy

Report vulnerabilities privately through the repository host or the private contact published by Aspose.
Identify Aspose CLI, the affected revision and platform, and use a minimal synthetic reproduction.
Do not attach customer documents, passwords, tokens or license contents.

## Boundaries

App and Preview listen only on `127.0.0.1`. They enforce the exact Host header, same-origin
requests, CSRF tokens, bounded bodies and authenticated current-user lifecycle control. Their
pages send `object-src 'none'` and `Cross-Origin-Resource-Policy: same-origin`; the App shell
cannot be framed (`frame-ancestors 'none'`, `X-Frame-Options: DENY`) and the viewer only by its
own origin (`frame-ancestors 'self'`, `SAMEORIGIN`). Rendered document parts run no scripts.
The App control endpoint is a per-user
singleton; selecting another configuration directory does not create an independent endpoint.

MCP `execute` runs product commands and only the host commands that change no user or service
state beyond publishing new outputs (`review` publishes a new evidence directory); it cannot
install, update, change licenses or start and stop services.

File routing is content-driven. Publication, extraction, ownership and resource budgets are
checked. File publication and shared per-user install and PATH state are guarded by
interprocess locks. Product license files are separate, and configuration files use the
selected CLI configuration directory. License contents and passwords must not enter logs or
results. SDK evaluation behavior is disclosed honestly.

## Releases

Customer installation and updates require the installer's Authenticode signature and a
detached package signature from a key in the configured `ASPOSE_CLI_RELEASE_TRUSTED_KEYS` ring.
A release signed for another distribution is rejected, and a downgrade or a different build
with the same version is refused. The explicit development-package path does not establish
customer release trust.
