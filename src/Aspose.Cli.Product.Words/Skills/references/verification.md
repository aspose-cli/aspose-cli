# Verification

The delivery checklist, the review protocol and font checks are in
`aspose-cli docs verification`. This page covers what Words adds.

## Semantic verification

`words edit --verify` reopens the staged file before publication and reports
`verification`; its fields are described in
`aspose-cli schema v2/words/edit-result`. It checks the field count, the
revision count and the protection of the reopened file against the edited
document, and compares the content with the input's on private copies with
revisions accepted, so the saved document keeps its revisions. The operations
it reads back are those that add, update or remove fields, set protection or
accept or reject revisions, and with `--track-changes` every applied
operation; one left out of `checkedOps` was not checked. A count or
protection mismatch is an issue, usually because the output format (such as
`txt` or `html`) does not keep that state; save to a Word format and verify
again.

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
before and after it, and `targets` names them in document order, where the
output of the whole batch holds them: a body block as `block/<n>`, numbered as
`words read` numbers the output even when a later operation moved it, and a
block the batch removed by its number in the input; a section whose page
setup, headers or footers changed as `section/<n>`, a style as
`style/<name>`, a document property as `property/<name>`, the watermark as
`watermark/1` and the editing restriction as `protection/document`. More than
100 of one kind shorten to one range, such as `block/1-150`. An operation
that changed nothing warns `OP_NO_EFFECT`; act on it as Reading the outcome
in `aspose-cli docs editing` says. `pagesTouched` lists the output pages that
hold what the batch changed: the pages of each changed block, and for a
removed block the page of the content that took its place; every page of a
section whose page setup, headers or footers changed; every page for the
watermark; and the pages of the text in a changed style. A batch that changed
nothing on a page, such as only properties, has no `pagesTouched`.

## Visual review

`aspose-cli review output.docx --out <new-dir>` renders every page. What to do
about the Words findings that most often need action:

When pages break badly, with a heading left at the foot of a page, a page
nearly or wholly empty, or many manual breaks (the review names them, such as
`WORDS_HEADING_ORPHANED` or `WORDS_PAGE_BLANK`), look for manual breaks and
empty paragraphs and remove them rather than adding more breaks.

A table split across pages, such as a last row pushed onto the next page, is
not a check: look for it on the rendered pages. Fix it with
`set_table_pagination` and `keepTogether: true` (or `headerRowCount` for a long
table) rather than a page break, then run `review` again
([editing](editing.md)).

No operation moves a floating image or shape, so when one crosses the page
edge (`WORDS_OBJECT_OUTSIDE_PAGE`), open the rendered page and tell the user
what is cut off instead of delivering the page as right.

When the document keeps tracked revisions or comments, the review reports
them as information (`WORDS_REVISIONS_PRESENT`, `WORDS_COMMENTS_PRESENT`).
Never accept revisions as a visual repair: disclose them, and accept or reject
them only as the user decides. Disclose comments (`document.commentCount` in
`words inspect`) too, and remove them with `delete_comments` only when the
user asks.

When an unlicensed run saved its banner, footer sentence and watermark into
the file (`WORDS_EVALUATION_MARKS`), a license does not remove them:
regenerate the document from its original inputs with the license before
delivery. When the message says evaluation mode cut the document short, the
content after its truncation notice, such as later merge records, is
missing, so removing the marks does not repair it. Only a licensed review
checks this, because opening a document without a license adds the marks
itself.

`words render` exports page images for delivery. It takes the format from
`--to`, or from the `--out` extension when `--to` is omitted.
