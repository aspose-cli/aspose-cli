# Forms and security

Inspect fields before filling:

```powershell
aspose-cli pdf query forms cover.pdf --output json
aspose-cli pdf edit cover.pdf --ops form-ops.json --out cover.filled.pdf --output json
aspose-cli pdf extract cover.filled.pdf --what forms --to json --out form-result.json --output json
```

AcroForm fields are addressed by exact field name using `set_form_field`
operations. `query forms` reports XFA as `type: "xfa"` and `readOnly: true`;
filling, flattening and form export reject it with `FORM_XFA_UNSUPPORTED`.
Flattening is an edit operation, not a `--flatten` option: use
`{"op":"flatten_forms","all":true}` or
`{"op":"flatten_forms","all":false,"fields":["ReportTitle"]}` only when
the requested fields should no longer be editable.

Encryption is an edit operation. Its ops document names environment variables,
never secret values:

```powershell
aspose-cli pdf edit report.pdf --ops encrypt-ops.json --out report.protected.pdf --output json
```

`encrypt` uses AES-256, requires `ownerPasswordEnv` and a `permissions` object,
and accepts optional `userPasswordEnv`; omitted permission flags are false.
Reopen protected output with `--password-env` and inspect `ownerAccess` and
permissions. The CLI reports these flags but does not add an operation-specific
permission preflight beyond the engine. Perform only authorized changes;
opening a PDF or completing an edit is not proof of owner authorization.

Existing signatures can be invalidated by edits. Inspect signatures before
changing signed material, then inspect them again afterward; a
`SIGNATURE_INVALIDATED` warning is not a signature-preservation guarantee.
Sign only after final content and standards verification. Supply the PKCS#12
password through an environment variable already set securely:

```powershell
aspose-cli pdf sign report.pdf --certificate signer.pfx --certificate-password-env PDF_SIGNING_PASSWORD --visible --page 1 --rect 36,36,180,60 --reason "Approved" --out report.signed.pdf --output json
aspose-cli pdf inspect report.signed.pdf --detail signatures --output json
```

Omit `--visible` for an invisible signature; `--rect` requires `--visible`.
Signature rectangle values are rounded to integer PDF points before the SDK
call, so render the result to check placement. The command reopens the saved
file and reports `signature.valid`, which may be true, false or unavailable. Require
`valid: true` and inspect all signature fields, especially when the input was
already signed. Command success alone does not prove signature validity, and
the CLI does not validate operating-system trust chains or revocation. Never
log or serialize the certificate password or pass its value on the command line.
