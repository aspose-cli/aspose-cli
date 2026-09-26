# Forms and security

## Forms

Read the fields before filling, then read the result back:

```powershell
aspose-cli pdf query forms cover.pdf --output json
aspose-cli pdf edit cover.pdf --ops form-ops.json --out cover.filled.pdf --output json
aspose-cli pdf extract cover.filled.pdf --what forms --to json --out form-result.json --output json
```

`set_form_field` addresses an AcroForm field by its exact full name. Each field's
`type` is one of `text` (including date, number, password and rich-text boxes),
`checkbox`, `radio`, `radio-option`, `combobox`, `listbox`, `button`, `signature`
or `other`. `query forms` reports XFA as `type: "xfa"` and `readOnly: true`;
filling, flattening and form export reject it with `FORM_XFA_UNSUPPORTED`.
Flattening is the `flatten_forms` operation, for named fields or every field; use
it only when those fields should no longer be editable.

## Encryption

`encrypt` and `decrypt` are edit operations whose ops name environment
variables, never password values (`aspose-cli docs editing`). Reopen protected
output with `--password-env` and inspect `ownerAccess` and `permissions`. The CLI
reports these flags and adds no permission preflight beyond the engine's; opening
a PDF or completing an edit is not proof of owner authorization, so perform only
authorized changes.

## Signatures

Edits can invalidate existing signatures. Inspect signatures before changing
signed material and again afterward; a `SIGNATURE_INVALIDATED` warning is not a
preservation guarantee. Sign only after final content and standards
verification, with the PKCS#12 password in an environment variable that is
already set:

```powershell
aspose-cli pdf sign report.pdf --certificate signer.pfx --certificate-password-env PDF_SIGNING_PASSWORD --visible --page 1 --rect 36,36,180,60 --reason "Approved" --out report.signed.pdf --output json
aspose-cli pdf inspect report.signed.pdf --detail signatures --output json
```

Omit `--visible` for an invisible signature; `--rect` requires `--visible`.
Signature rectangle values are rounded to whole points, so review the placement.
The command reopens the saved file and reports `signature.valid`, which may be
true, false or unavailable. Require `valid: true` and inspect every signature
field, especially when the input was already signed. The CLI does not validate
operating-system trust chains or revocation.
