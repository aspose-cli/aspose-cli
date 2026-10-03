# Words error codes

The error envelope, exit codes, not-found details and shared codes such as
`PASSWORD_REQUIRED` or `OUTPUT_EXISTS` are in `aspose-cli docs troubleshooting`.

## Errors

- `BLOCK_NOT_FOUND`, `SECTION_NOT_FOUND`: block and section numbers are
  1-based; `details.availableCount` says how many exist. Read the numbers again
  with `words query blocks --scope outline` or `inspect --detail sections`.
- `ANCHOR_NOT_FOUND`: no heading contains the `heading` text (the document's
  headings are in `details.available`), a `find` text or an `nth` goes past the
  matches (`details.availableCount` is the match count), or
  `words split --by heading1` found no Heading 1 paragraph.
- `BOOKMARK_NOT_FOUND`, `STYLE_NOT_FOUND`: the document's bookmarks or styles
  are in `details.available`; `define_style` creates a missing style.
- `MERGE_DATA_INVALID`: the `mail_merge` or `repeat_table_row` data is not a
  JSON array of flat objects or a CSV file with a header row, or `mail_merge`
  got no rows ([mail merge](mail-merge.md)).
- `DOCUMENT_PROTECTED`: the `unprotect` password was wrong.
- `DOCUMENT_HAS_REVISIONS`: `words compare` inputs must be revision-free; list
  the revisions with `words inspect --detail revisions` ([revisions](revisions.md)).
- `OPTION_INVALID` from a tracked batch: the batch contains an operation that
  cannot be recorded as a revision ([editing](editing.md)).

## Warnings

- `PROTECTION_NOT_ENFORCED`: the document has editing restrictions; the edit
  succeeded and the restrictions remain in a Word format output.
- `TRACKED_CHANGES_PRESENT`: the edited or converted document already had
  revisions and the output, in a Word format, `rtf`, `odt` or `ott`, still
  contains them; disclose them ([revisions](revisions.md)).
- `MERGE_VALUE_MISSING`: `mail_merge` data has no value for a template field
  in the listed records ([mail merge](mail-merge.md)).
- `DOCUMENT_ENCRYPTION_REMOVED`: the output format cannot be encrypted.
- `MACROS_DROPPED`: the source has macros and the output of `convert`, `edit`,
  `create`, `split` or `compare` was written without them. Only `doc`, `dot`,
  `docm`, `dotm` and `wordml` keep macros; save to `docm` or `dotm` to keep them.
- `LAYOUT_MAY_DIFFER`: `words split` can reflow complex layouts slightly;
  review the split pages.
- `LINKED_IMAGES_SKIPPED`: linked images store no bytes in the document, so
  `extract --what images` cannot write them.
