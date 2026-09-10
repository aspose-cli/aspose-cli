# Troubleshooting

- `PASSWORD_REQUIRED` / `PASSWORD_INVALID`: use `--password-env` or `--password-stdin`.
- `DOCUMENT_PROTECTED`: inspect protection and explicitly unprotect using an environment-backed password.
- `DOCUMENT_HAS_REVISIONS`: comparison inputs must be revision-free.
- `BLOCK_NOT_FOUND`: run `words query blocks` again; block numbers are 1-based.
- `ANCHOR_NOT_FOUND`: inspect headings/bookmarks or use a current block.
- `PAGE_RANGE_INVALID`: use ranges such as `1-3,7,9-`.
- `RENDER_TOO_LARGE`: lower DPI or render fewer pages.
- `REMOTE_RESOURCES_BLOCKED`: external access is denied; use guarded local resources beside the document.
- `EVAL_MODE`: disclose watermark and evaluation limits.

Use `aspose-cli doctor`, `aspose-cli license status`, and `aspose-cli capabilities --output json` for environment diagnosis.
License status is per product. For Words-only setup, use
`aspose-cli license install Aspose.Words.lic --product words` or
`ASPOSE_WORDS_LICENSE_PATH`; a Cells-only license does not license Words.
