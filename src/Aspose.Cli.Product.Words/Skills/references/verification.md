# Verification

The delivery checklist, the review protocol and font checks are in
`aspose-cli docs verification`. This page covers what Words adds.

## Semantic verification

`words edit --verify` reopens the staged file before publication and reports
`verification`:

- `ok` and `issues`;
- `checkedOps`, the ids of the operations whose effect these checks read back:
  those that add, update or remove fields, set protection or accept or reject
  revisions, and with `--track-changes` every applied operation;
- `semanticChangesDetected`, from a comparison of private copies with
  revisions accepted, so the saved document keeps its revisions;
- `fieldCount`, `revisionCount` and `protection`, each checked against the
  in-memory result. A mismatch is reported as `FIELD_COUNT_CHANGED`,
  `REVISION_COUNT_CHANGED` or `PROTECTION_CHANGED`, usually because the
  output format (such as `txt` or `html`) does not keep that state; save to a
  Word format and verify again.

`--verify` cannot be combined with `--dry-run`.

## Read-back

```powershell
aspose-cli words inspect output.docx --detail outline fields comments --output-format json
aspose-cli words read output.docx --blocks 1- --max-blocks 30 --content full --output-format json
aspose-cli words compare baseline.docx output.docx --output-format json
```

Read the blocks the batch reported in `applied[].targets`, and blocks that
follow insertions. In `--content full`, compare an inserted paragraph's `runs`
fonts and `paragraphFormat` (indents, spacing, alignment) with its neighbours'.
`applied[].itemsAffected` counts the objects the operation changed, compared
before and after it, and `targets` names them where the output holds them: a
body block as `block/<n>`, numbered as `words read` numbers it, a section
whose page setup, headers or footers changed as `section/<n>`, a style as
`style/<name>`, a document property as `property/<name>`, the watermark as
`watermark/1` and the editing restriction as `protection/document`. More than
100 of one kind shorten to one range, such as `block/1-150`. An operation that
changed nothing reports 0 and `OP_NO_EFFECT`. `pagesTouched` lists the output
pages the batch changed: those of the blocks and sections it addressed or
inserted, of replaced text, and of the content that took the place of removed
blocks; a header or footer change touches every page of its section.

## Visual review

`aspose-cli review output.docx --out <new-dir>` renders every page. The Words
checks that most often need action:

- `WORDS_HEADING_ORPHANED`, `WORDS_PAGE_UTILIZATION_LOW`, `WORDS_PAGE_BLANK`
  and `WORDS_PAGE_BREAKS_EXCESSIVE`: pagination problems, usually from manual
  breaks or empty paragraphs; remove them rather than adding more breaks.
- A table split across pages, such as a last row pushed onto the next page,
  is not a check: look for it on the rendered pages. Fix it with
  `set_table_pagination` and `keepTogether: true` (or `headerRowCount` for a long
  table) rather than a page break, then run `review` again
  ([editing](editing.md)).
- `WORDS_OBJECT_OUTSIDE_PAGE`: a floating image or shape crosses the page edge.
- `WORDS_REVISIONS_PRESENT` (info): tracked revisions are shown; never accept
  them as a visual repair. Disclose them, and accept or reject them only as
  the user decides.
- `WORDS_EVALUATION_MARKS`: a run without a license saved its banner, footer
  sentence and watermark into the file, and a license does not remove them;
  regenerate the document from its original inputs with the license before
  delivery. When the message says evaluation mode cut the document short, the
  content after its truncation notice, such as later merge records, is
  missing, so removing the marks does not repair it. Only a licensed review checks it, because opening a document
  without a license adds the marks itself.
- `WORDS_COMMENTS_PRESENT` (info): the document keeps comments
  (`document.commentCount` in `words inspect`); disclose them, and remove them
  with `delete_comments` only when the user asks.

`words render` exports page images for delivery. It takes the format from
`--to`, or from the `--out` extension when `--to` is omitted.
