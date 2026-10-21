# Forms and security

## Forms

Read the fields before filling, then read the result back:

```powershell
aspose-cli pdf inspect cover.pdf --detail forms --output-format json
aspose-cli pdf edit cover.pdf --ops form-ops.json --out cover.filled.pdf --output-format json
aspose-cli pdf extract cover.filled.pdf --what forms --form-format json --out-dir form-data --output-format json
```

`pdf inspect --detail forms` is the structured read of the fields: their `name`,
`kind` and `value` as its schema names them. `extract --what forms` writes a
file for other form software in the format `--form-format` names: `fdf`,
`xfdf`, or with `json` the engine's own JSON, whose field names (`Name`,
`PageIndex`, `Flags`, `Value`) differ from the inspect result's and that no
schema describes. To hand field values to another program, read them with
`inspect --detail forms`.

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
as its `options`; it has no `page` of its own. Any other value is refused. A `value` of `null` clears a field: a radio group selects no
button, a check box is unchecked and any other field is emptied.
A `button` or `signature` field holds no value, so `set_form_field` refuses it
with `OPS_INVALID`. To put a picture where a form has a photo or image button,
use `add_stamp_image` with that field's `page` and `rect` from
`pdf inspect --detail forms`; a signature field is filled by `pdf sign`.
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

### Forms made from HTML

`pdf create --from-html` turns HTML form controls into AcroForm fields. A
single-line text `<input>` keeps its `name`; other controls get generated names
and lose their `value`s, and inputs of type `email`, `tel`, `url`, `time`,
`datetime-local`, `month`, `week`, `color`, `range`, `file`, `image` or `hidden`
are dropped; `LOSSY_CONVERSION` lists both. Use `type="text"` for an input that
must stay a field, place the picture of an image input with `add_stamp_image`,
and match each field's `rect` to its label before filling.

## Encryption

`encrypt_document` and `decrypt_document` are edit operations whose ops name environment
variables, never password values (`aspose-cli docs editing`). For a password to
open the output and no restrictions, `create`, `edit`, `merge` and `split` take
`--encrypt-env` (or `--encrypt-stdin`); on `edit` it applies after the
operations and replaces any encryption they leave:

```bash
aspose-cli pdf merge a.pdf b.pdf --out merged.pdf --encrypt-env PDF_PASSWORD --output-format json
```

`output.encrypted` counts a file with only an owner password as encrypted.
Without `--encrypt-env`, an edit or signature
keeps the input's encryption; a merge or split keeps the password the inputs
were opened with as the output's open password and warns
`PROTECTION_NOT_ENFORCED` with what became of the rest: opened with the user
password, the output keeps the input's permissions under a new owner password
no one knows; opened with the owner password, that password opens the output
and grants every permission. An input whose open password is also its owner
password, as `--encrypt-env` writes it, keeps that one password without a
warning. An input with only an owner password has no open
password to keep, and a convert writes formats that cannot carry one: a convert
to a document or text format, a `decrypt_document`, and a merge or split of an
input with only an owner password warn `ENCRYPTION_REMOVED`
(`aspose-cli docs editing`). Pass `--encrypt-env` where the output must stay
protected. Inspect protected
output with `pdf inspect --detail permissions`: `print`, `copy` and the other
permission fields state what the file grants readers without the owner
password, also when `--password-env` opened it with the owner password, and
`ownerAccess` whether this run may change the file regardless of them.
`set_properties` with text beyond Latin-1, such as Chinese, is refused on an
encrypted input unless an `encrypt_document` operation follows it in the same batch.
`hasOpenPassword` and `hasOwnerPassword` state which passwords the file has, and
`passwordType` which one this run opened it with: a file with only an owner
password opens with none, so `passwordType` is absent, and anyone can open it within its permissions; a
password that both opens the file and is its owner password reports `owner`, with
`hasOwnerPassword` and `ownerAccess` true. The CLI
reports these flags and adds no permission preflight beyond the engine's; opening
a PDF or completing an edit is not proof of owner authorization, so perform only
authorized changes.

In evaluation mode each save prints the evaluation notice once, and an encrypted
output is saved once, with two exceptions that encrypt a saved copy to keep text
beyond Latin-1, such as a Chinese title, readable, and so carry the notice twice:
`--encrypt-env` when the output's document information holds such text, whether
set now or taken from an input; and a `pdf edit` batch with `set_properties` and
`encrypt_document` when the information holds such text. `encrypt_document`
without `set_properties` saves once, whatever the title.

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
A visible signature names the signer by the common name (CN) of the certificate's
subject, or by the whole subject when it has none, then the date and one line
each for the `--reason`, `--location` and `--contact` given; an option left out
draws no line. Lines are not wrapped: the type shrinks from 10 to 6 points so the
longest line fits the rectangle's width, and each line takes 12 points of its
height. Text that still does not fit is cut off and the result warns
`SIGNATURE_TEXT_CLIPPED`; sign again with a larger `--rect` or shorter text.
A signer name with East Asian characters is drawn in SimSun or Microsoft YaHei
when `--reason`, `--location` and `--contact` have none; with neither font
installed the name is left out and the result warns `SIGNATURE_TEXT_CLIPPED`.
When one of them has East Asian text, the engine picks the font from it, and a
Chinese name can still be left out without a warning: Japanese kanji such as
`批准` give MS Gothic. A Korean signer name is left out in SimSun, also without
a warning. Give `--reason` in simplified Chinese, or sign with a certificate
whose subject name is in Latin letters, and review the signature.
Keep `--reason`, `--location`, `--contact` and the certificate's subject name in
ASCII: other text makes the engine embed a whole font into the file, even for an
invisible signature, which for Chinese text adds megabytes; the result then warns
`SIGNATURE_FONT_EMBEDDED` with the options that hold such text and the growth.
The command reopens the saved file and reports `signature.intact`, which may be
true, false or unavailable. `intact: true` means the signature verifies against
the bytes it signed and those bytes are the whole file; a revision appended
after signing, which can change what every page shows, makes it false. A file
signed more than once reports `intact: false` for every signature but the last,
because each later signature is appended after the earlier ones: check the last
one. It does not mean the signer is trusted: the CLI checks no trust chain or
revocation, and a self-signed certificate is intact too. Require `intact: true`
of the last signature and inspect every signature field, especially when the
input was already signed, then confirm the signer by other means.
