# PDF troubleshooting

The error envelope and the shared codes are in `aspose-cli docs troubleshooting`;
`aspose-cli capabilities pdf` lists every PDF code with its exit code. This page
adds what to do about the PDF cases that need more than the error's hint.

## Sources that can reach the network

The CLI blocks every network and `file:` request the PDF engine makes for HTML and
SVG image inputs and the stylesheets and SVG files they load, script included: the
command still succeeds, without those resources, and `REMOTE_RESOURCES_BLOCKED`
lists each blocked address. An SVG image keeps only the images it embeds as `data:`
URIs: every other reference is removed and listed the same way. Copy the resource
into the input's folder or a folder below it and reference it by relative path. For trusted HTML,
`--allow-network-resources` lets the import fetch http and https addresses, and the
result lists them as `NETWORK_RESOURCES_REQUESTED`; review what they returned.
Combine the option with `--timeout`, since each unanswered request can hold the
import for up to 100 seconds. `aspose-cli words convert page.html --to pdf`
converts without any request.

Every image, stylesheet and SVG file Markdown could load, and those they reference,
must be an existing ordinary file beneath the Markdown file's directory: network
addresses, absolute paths, `file:` URIs, `..` escapes (also percent- or
entity-encoded), links and junctions, missing files and non-raster `data:` URIs are refused, including in code
examples. Run the command from the Markdown file's directory. A local HTML image or
stylesheet that is missing or outside the HTML directory is left out with
`REMOTE_RESOURCES_BLOCKED`; keep resources beneath that directory rather than
widening access, and review the incomplete output.

The HTML and Markdown importers run the input's page script (without
`XMLHttpRequest`); script inside an SVG file the page loads does not run. What a
script adds to the page, such as a frame or a stylesheet, is loaded like any other
reference: no network address and no file outside the input's directory, so a
script can put into the output only what the input's directory already holds.

## Forms and archives

`FORM_XFA_UNSUPPORTED` is in [forms](forms-security.md), and
`PDFA_CONVERSION_FAILED` in [PDF/A](pdf-standards.md).

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

[Encryption](forms-security.md#encryption) says which outputs warn
`ENCRYPTION_REMOVED` and how to keep them protected. `pdf convert` does not encrypt and
refuses `--encrypt-env`: to encrypt a PDF, run `pdf edit` with an
`encrypt_document` operation, or with `--encrypt-env` beside its operations. PDF/A
does not allow encryption, so an encrypted PDF/A file is no longer conformant:
keep the PDF/A file and encrypt a copy.
