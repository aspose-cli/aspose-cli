# License ownership

`LicenseManager` is the management entry point for CLI, App, startup reporting,
preview and doctor. It selects catalog products, reports each product independently,
and coordinates validated installation/removal through the SDK storage module.

The SDK `Licensing` directory owns source precedence, bounded input, native gate
lifetime, identity and atomic storage. Each product Engine retains only its own
`SetLicense(Stream)` adapter. Adding a product does not add a Host branch.

## Invariants

- An explicit invalid source is an error; it never falls through to evaluation.
- A gate reads at most one MiB, validates those exact bytes and exposes their opaque
  source/content identity. Status and process reuse come from the same snapshot.
- Installation admits one private snapshot before validation, then publishes the
  validated bytes for compatible products in one transaction. Removal uses the same
  publication locks and rollback, including shared and product-specific locations.
- Installation/removal results come from explicit validated configuration changes. Only
  the supervisor publishes worker outputs; ordinary file reads always address physical files.
  Existing license targets must already be private.
- A long-lived App pins its validated gates for its entire SDK process. Opening a
  new document cannot silently read a newer license than the App's status/identity.
- App-side validation uses `LicenseValidationProcess`, a bounded short-lived CLI
  process. Management and replacement planning cannot mutate the running SDK state.
- A new launch compares the validated identity before reusing an App or preview.
  A changed valid preview license replaces the owned process; an invalid selected
  license rejects the launch and leaves the existing preview untouched. App Settings
  remain available with invalid configuration, and such App snapshots are not reused.
  Failed App restart restores the old control endpoint and reports saved configuration.
  Uploaded preview files are copied through the existing bounded upload path before
  the old App exits.
- Human startup messages are emitted once at the outer CLI boundary. JSON, MCP,
  verbose JSONL and internal protocols remain parseable.

App upload/HTTP response and process-control adapters remain with App lifecycle
code; they do not select a license or invent their own precedence. The public
status contract is `LicenseStatusResult.Products`; `Identity` is an opaque cache
key for a fully validated snapshot, not a substitute for SDK validation.
