# Security Policy

Report vulnerabilities privately through the repository host or the private contact published by Aspose.
Identify Aspose CLI, the affected revision and platform, and use a minimal synthetic reproduction.
Do not attach customer documents, passwords, tokens or license contents.

## Boundaries

App and Preview bind only to loopback. Exact Host, same-origin, CSRF, bounded bodies and authenticated
current-user lifecycle controls remain enforced. File routing is content-driven.
Publication, extraction, ownership and resource budgets are checked.
Product license files are separate, and configuration files use the selected CLI configuration directory.
The App control endpoint is a per-user singleton; selecting another configuration directory does not
create an independently runnable App endpoint. Shared OS publication/PATH resources use interprocess locks.

Customer installation and updates require applicable Authenticode and detached package signatures.
The explicit development-package path does not establish customer release trust.
Different products cannot substitute each other's signed manifests.

License contents and encryption secrets must not enter logs or results. SDK evaluation behavior is disclosed honestly.

This project is pre-release and maintains its current development contract.
