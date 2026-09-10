# Forms and security

Inspect fields before filling:

```powershell
aspose-cli pdf query forms cover.pdf --output json
aspose-cli pdf edit cover.pdf --ops form-ops.json --out cover.filled.pdf --output json
aspose-cli pdf extract cover.filled.pdf --what forms --to json --out form-result.json --output json
```

AcroForm fields are addressable by exact field name. XFA is intentionally
reported as `FORM_XFA_UNSUPPORTED`; do not silently flatten or guess mappings.
Use `--flatten` only when the requested deliverable should no longer be
editable.

Encryption is an edit operation. Its ops document names environment variables,
never secret values:

```powershell
aspose-cli pdf edit report.pdf --ops encrypt-ops.json --out report.protected.pdf --output json
```

Reopen protected output with `--password-env` and inspect permissions. A wrong
PDF-specific license or password configuration must not affect Cells or Words.
Existing signatures can be invalidated by edits; inspect signatures before
changing signed material and disclose the consequence.

Apply a PKCS#7 signature only after the final content verification. The
certificate password must come from an environment variable:

```powershell
$env:PDF_SIGNING_PASSWORD = "<secret>"
aspose-cli pdf sign report.pdf --certificate signer.pfx --certificate-password-env PDF_SIGNING_PASSWORD --visible --page 1 --rect 36,36,180,60 --reason "Approved" --out report.signed.pdf --output json
aspose-cli pdf inspect report.signed.pdf --detail signatures --output json
```

Omit `--visible` for an invisible signature. A successful `sign` result proves
that the saved signature field verifies through the PDF engine; it does not
perform operating-system trust-chain or revocation validation. Never log,
serialize, or place the certificate password directly on the command line.
