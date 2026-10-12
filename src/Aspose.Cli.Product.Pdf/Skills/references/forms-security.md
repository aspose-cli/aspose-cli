# Forms and security

## Forms

Read the fields before filling, then read the result back:

```powershell
aspose-cli pdf inspect cover.pdf --detail forms --output-format json
aspose-cli pdf edit cover.pdf --ops form-ops.json --out cover.filled.pdf --output-format json
aspose-cli pdf extract cover.filled.pdf --what forms --form-format json --out-dir form-data --output-format json
```

`set_form_field` addresses an AcroForm field by its exact full name. Each field's
`kind` is one of `text` (including date, number, password and rich-text boxes),
`checkbox`, `radio`, `combobox`, `listbox`, `button`, `signature` or `other`.
Its `page` and `rect` (points from the page's top-left corner) place it on the
page: when names do not say which label a field belongs to, match the `rect` to
the label's position (`pdf search` gives each text's `rect`). The list holds the
first 500 fields by name; a `LIST_TRUNCATED` warning says when there are more.

A field with `options` accepts only those values. A check box lists its states,
`Off` first: set another state to check it and `Off` to clear it. A radio group
is one field with the group's selection as its `value` and its buttons' values
as its `options`; it has no `page` of its own. Any other value is refused. A `value` of `null` clears any field: a radio group selects no
button, a check box is unchecked and any other field is emptied.
Fields are counted by name: `fieldCount` of `pdf inspect --detail forms` and the
count `review` reports take a radio group as one field.

```json
{ "ops": [
  { "op": "set_form_field", "name": "agree", "value": "Checked" },
  { "op": "set_form_field", "name": "color", "value": "Blue" },
  { "op": "set_form_field", "name": "notes", "value": null }
] }
```

`pdf inspect --detail forms` reports XFA as `kind: "xfa"` and `readOnly: true`;
filling, flattening and form export reject it with `FORM_XFA_UNSUPPORTED`.
Flattening is the `flatten_forms` operation, for named fields or every field; use
it only when those fields should no longer be editable. Naming a radio group
flattens every button of that group.

## Encryption

`encrypt` and `decrypt` are edit operations whose ops name environment
variables, never password values (`aspose-cli docs editing`). For a password to
open the output and no restrictions, `create`, `edit`, `merge` and `split` take
`--encrypt-env` (or `--encrypt-stdin`); on `edit` it applies after the
operations and replaces any encryption they leave:

```bash
aspose-cli pdf merge a.pdf b.pdf --out merged.pdf --encrypt-env PDF_PASSWORD --output-format json
```

`output.encrypted` states whether the written file is encrypted, a file with
only an owner password included. Without `--encrypt-env`, an edit or signature
keeps the input's encryption; a merge or split keeps the password the inputs
were opened with as the output's open password and warns
`PROTECTION_NOT_ENFORCED` with what became of the rest: opened with the user
password, the output keeps the input's permissions under a new owner password
no one knows; opened with the owner password, that password opens the output
and grants every permission. An input whose open password is also its owner
password, as `--encrypt-env` writes it, keeps that one password without a
warning. An input with only an owner password has no open
password to keep, and a convert writes formats that cannot carry one: any
document or text output that does not keep the encryption of an encrypted input
carries `ENCRYPTION_REMOVED`; images and what `extract` writes never do. Inspect protected
output with `pdf inspect --detail permissions`: `print`, `copy` and the other
permission fields state what the file grants readers without the owner
password, also when `--password-env` opened it with the owner password, and
`ownerAccess` whether this run may change the file regardless of them.
`set_metadata` with text beyond Latin-1, such as Chinese, is refused on an
encrypted input unless an `encrypt` operation follows it in the same batch.
`hasOpenPassword` and `hasOwnerPassword` state which passwords the file has, and
`passwordType` which one this run opened it with: a file with only an owner
password opens with none, so `passwordType` is absent, and anyone can open it within its permissions; a
password that both opens the file and is its owner password reports `owner`, with
`hasOwnerPassword` and `ownerAccess` true. The CLI
reports these flags and adds no permission preflight beyond the engine's; opening
a PDF or completing an edit is not proof of owner authorization, so perform only
authorized changes.

## Signatures

Edits can invalidate existing signatures. Inspect signatures before changing
signed material and again afterward; a `SIGNATURE_INVALIDATED` warning is not a
preservation guarantee. Sign only after final content and standards
verification, with the PKCS#12 password in an environment variable that is
already set (or on stdin with `--certificate-password-stdin`):

```powershell
aspose-cli pdf sign report.pdf --certificate signer.pfx --certificate-password-env PDF_SIGNING_PASSWORD --visible --page 1 --rect 36,36,180,60 --reason "Approved" --out report.signed.pdf --output-format json
aspose-cli pdf inspect report.signed.pdf --detail signatures --output-format json
```

Omit `--visible` for an invisible signature; `--rect` requires `--visible` and
measures from the page's top-left corner, as `redact_area` and `pdf render --grid` do.
Signature rectangle values are rounded to whole points, so review the placement.
The command reopens the saved file and reports `signature.valid`, which may be
true, false or unavailable. Require `valid: true` and inspect every signature
field, especially when the input was already signed. The CLI does not validate
operating-system trust chains or revocation.
