# Verification

The delivery checklist, the review protocol and font checks are in
`aspose-cli docs verification`. This page covers what Words adds.

## Semantic verification

`words edit --verify` reopens the staged file before publication and reports
`verification`:

- `ok` and `issues`;
- `semanticChangesDetected`, from a comparison of private copies with
  revisions accepted, so the saved document keeps its revisions;
- `fieldCount`, `revisionCount` and `protection`, each checked against the
  in-memory result. A mismatch is reported as `FIELD_COUNT_CHANGED`,
  `REVISION_COUNT_CHANGED` or `PROTECTION_CHANGED`, usually because the
  output format (such as `txt` or `html`) does not keep that state; save to a
  Word format and verify again.
- `OUTPUT_TRUNCATED` when evaluation mode cut the edited document short: the
  output keeps only its first sections and ends with the engine's truncation
  notice. Apply a license and run the edit again.

`--verify` cannot be combined with `--dry-run`. Failed checks are a partial
success (exit 8) that still publishes the output for repair; execution
failures publish nothing.

## Read-back

```powershell
aspose-cli words inspect output.docx --detail outline fields comments --output json
aspose-cli words query blocks output.docx --blocks 1-30 --scope full --output json
aspose-cli words compare baseline.docx output.docx --output json
```

Read the blocks the batch reported in `applied[].targets`, and blocks that
follow insertions. `replace_text` names the original body blocks whose text,
comments or footnotes it changed, and `document` when it changed none of them.
`pagesTouched` lists the output pages the batch changed: those of the blocks
and sections it addressed or inserted, of replaced text, and of the content
that took the place of removed blocks; a header or footer change touches every
page of its section. An operation whose only target is `document`, such as
`add_watermark` or `mail_merge`, can change any page. `words compare` needs
revision-free inputs ([revisions](revisions.md)).

## Visual review

`aspose-cli review output.docx --out <new-dir>` renders every page. The Words
checks are listed with `aspose-cli capabilities words --output json` under
`review.checks`; the ones that most often need action:

- `WORDS_HEADING_ORPHANED`, `WORDS_PAGE_UTILIZATION_LOW`, `WORDS_PAGE_BLANK`
  and `WORDS_PAGE_BREAKS_EXCESSIVE`: pagination problems, usually from manual
  breaks or empty paragraphs; remove them rather than adding more breaks.
- A table split across pages, such as a last row pushed onto the next page,
  is not a check: look for it on the rendered pages. Fix it with
  `format_table` and `keepTogether: true` (or `headerRowCount` for a long
  table) rather than a page break, then run `review` again
  ([editing](editing.md)).
- `WORDS_OBJECT_OUTSIDE_PAGE`: a floating image or shape crosses the page edge.
- `WORDS_REVISIONS_PRESENT` (info): tracked revisions are shown; never accept
  them as a visual repair.
- `WORDS_COMMENTS_PRESENT` (info): the document keeps comments
  (`document.commentCount` in `words inspect`); disclose them, and remove them
  with `remove_comments` only when the user asks.

```powershell
aspose-cli review output.docx --out output.review-2 --code WORDS_HEADING_ORPHANED --code WORDS_PAGE_BLANK --output json
```

`words render` exports page images for delivery. It takes the format from
`--to`, or from the `--out` extension when `--to` is omitted, and refuses a
`--to` that disagrees with the `--out` extension.
