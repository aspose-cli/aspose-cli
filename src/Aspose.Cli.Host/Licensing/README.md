# License ownership

`LicenseManager` is the management entry point for the `license` and `doctor`
commands and the startup notice. It selects catalog products, reports each product
independently, and coordinates validated installation/removal through the SDK
storage module.

The SDK `Licensing` directory owns source precedence, bounded input, native gate
lifetime, identity and atomic storage. Each product Engine retains only its own
`SetLicense(Stream)` adapter. Adding a product does not add a Host branch.

## Invariants

- An explicit invalid source is an error; it never falls through to evaluation.
- A gate reads at most one MiB, validates those exact bytes and exposes their opaque
  source/content identity.
- Installation admits one bounded snapshot before validation, then publishes the
  validated bytes for compatible products in one transaction. Removal uses the same
  publication locks and rollback, including shared and product-specific locations.
- Installation/removal results come from explicit validated configuration changes. Only
  the supervisor publishes worker outputs; ordinary file reads always address physical files.
  Existing license targets must be regular files reached without links or reparse points.
- The long-lived viewer service, which hosts the App, never applies a license. The App
  runs `license status`, `license install` and `license remove` as bounded CLI child
  processes (`AppCliGateway`), keeps the status until `LicenseFingerprint` reports a
  change, and re-raises a child's error with the child's own code and exit code.
- The render worker applies licenses for the viewer. It asks to be recycled when the
  `LicenseFingerprint` of a product changes, because an engine cannot swap a license.
- Human startup messages are emitted once at the outer CLI boundary. JSON, MCP,
  verbose JSONL and internal protocols remain parseable.

The public status contract is `LicenseStatusResult.Products`; `Identity` is an opaque
key for a fully validated snapshot, not a substitute for SDK validation.
