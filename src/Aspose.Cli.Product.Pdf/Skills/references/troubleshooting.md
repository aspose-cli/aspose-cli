# PDF troubleshooting

The error envelope and the shared codes are in `aspose-cli docs troubleshooting`;
`aspose-cli capabilities pdf` lists every PDF code with its exit code. This page
adds what to do about the PDF cases that need more than the error's hint.

## Sources that can reach the network

HTML, Markdown and SVG image inputs, and the stylesheets and SVG files they load,
may not name any network address, including hyperlinks and addresses in text, or
contain script, event-handler attributes or `javascript:` URLs: the PDF engine
requests them, or runs the script, before the CLI can refuse them, so the input is
refused with `FEATURE_UNSUPPORTED`. Remove the address, or save the resource beside
the input and reference it by relative path. For trusted HTML,
`--allow-network-resources` lets the import fetch them, and the result lists the
requested addresses as `NETWORK_RESOURCES_REQUESTED`; review what they returned.
Combine the option with `--timeout`, since each unanswered request can hold the
import for up to 100 seconds. `aspose-cli words convert page.html --to pdf`
converts without any request.

Every image, stylesheet and SVG file Markdown could load, and those they reference,
must be an existing ordinary file beneath the Markdown file's directory: absolute
paths, `file:` URIs, `..` escapes (also percent- or entity-encoded), links and
junctions, missing files and non-raster `data:` URIs are refused, including in code
examples. Run the command from the Markdown file's directory. A local HTML image or
stylesheet that is missing or outside the HTML directory is left out with
`REMOTE_RESOURCES_BLOCKED`; keep resources beneath that directory rather than
widening access, and review the incomplete output.

## Forms and archives

XFA forms can be inspected but not filled, flattened or exported as AcroForm data
(`FORM_XFA_UNSUPPORTED`). A PDF/A conversion that cannot make the document conform
fails with `PDFA_CONVERSION_FAILED` and lists what it could not fix in
`error.details.problems`; a font the document uses that is missing here also fails
it ([PDF/A](pdf-standards.md)).

## Permissions and encryption

An input opened without its owner password (with its user password, or with none
when it has no open password) whose permissions (`pdf inspect --detail permissions`)
forbid a change is still changed, because the engine does not enforce them, and the
result warns `PROTECTION_NOT_ENFORCED`. Filling fields needs `fillForms`, `annotate`
or `modify`; inserting, moving, rotating or deleting pages and adding bookmarks need
`assemble` or `modify`; other changes need `modify`; `encrypt_document` and
`decrypt_document` always need the owner password. Confirm the owner authorized the
change; the hint says which encryption the output carries. A merge or split that
keeps an input's open password warns too.

An output that loses its input's encryption, as after a convert to a document or
text format, a `decrypt_document`, or a merge or split of an input with only an
owner password, warns `ENCRYPTION_REMOVED`; images and extracted files never warn.
Pass `--encrypt-env` where the output must stay protected
([encryption](forms-security.md#encryption)). `pdf convert` does not encrypt and
refuses `--encrypt-env`: to encrypt a PDF, run `pdf edit` with an
`encrypt_document` operation, or with `--encrypt-env` beside its operations. PDF/A
does not allow encryption, so an encrypted PDF/A file is no longer conformant:
keep the PDF/A file and encrypt a copy.
