# Troubleshooting

- `FILE_CORRUPT`: the input could not be parsed as a supported document. Verify its format and obtain an intact copy; truncated document containers require repair or replacement.
- `FILE_LOCKED`: another application holds an exclusive file lock. Close that application and retry; this code does not mean the document is corrupt.
- `FEATURE_UNSUPPORTED`: the document engine failed. Retry with a simplified copy or another output format; when other documents fail the same way, the local environment (for example its fonts) is the cause.
- `PASSWORD_REQUIRED` / `PASSWORD_INVALID`: use `--password-env` or `--password-stdin`.
- `DOCUMENT_PROTECTED`: the `unprotect` password was wrong. Editing restrictions alone never block an edit; they report `PROTECTION_NOT_ENFORCED`.
- `DOCUMENT_HAS_REVISIONS`: comparison inputs must be revision-free.
- `BLOCK_NOT_FOUND`, `SECTION_NOT_FOUND`: numbers are 1-based; `details.availableCount` says how many exist.
- `BOOKMARK_NOT_FOUND`, `STYLE_NOT_FOUND`, `ANCHOR_NOT_FOUND`: named not-found errors list the available names in `details.available` and the closest ones in `details.suggestions`; a `find` or `nth` past the matches reports the match count in `details.availableCount`.
- `PAGE_RANGE_INVALID`: use ranges such as `1-3,7,9-`.
- `RENDER_TOO_LARGE`: lower DPI or render fewer pages.
- `REMOTE_RESOURCES_BLOCKED`: external access is denied; use guarded local resources beside the document.
- `OUTPUT_EXISTS`: choose another path or pass `--overwrite`. For `split` and `extract`, one existing file in `--out-dir` refuses the whole run and nothing is published; `--overwrite` replaces the files the command writes, and other files in the directory stay.
- `EVAL_MODE`: disclose watermark and evaluation limits.

Use `aspose-cli doctor`, `aspose-cli license status`, and `aspose-cli capabilities --output json` for environment diagnosis.
License status is per product. For Words-only setup, use
`aspose-cli license install Aspose.Words.lic --product words` or
`ASPOSE_WORDS_LICENSE_PATH`; a Cells-only license does not license Words.
