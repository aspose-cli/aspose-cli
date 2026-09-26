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
  in-memory result.

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
follow insertions. `words compare` needs revision-free inputs
([revisions](revisions.md)).

## Visual review

`aspose-cli review output.docx --out <new-dir>` renders every page. The Words
checks are listed with `aspose-cli capabilities words --output json` under
`review.checks`; the ones that most often need action:

- `WORDS_HEADING_ORPHANED`, `WORDS_PAGE_UTILIZATION_LOW`, `WORDS_PAGE_BLANK`
  and `WORDS_PAGE_BREAKS_EXCESSIVE`: pagination problems, usually from manual
  breaks or empty paragraphs; remove them rather than adding more breaks.
- `WORDS_OBJECT_OUTSIDE_PAGE`: a floating image or shape crosses the page edge.
- `WORDS_REVISIONS_PRESENT` (info): tracked revisions are shown; never accept
  them as a visual repair.

```powershell
aspose-cli review output.docx --out output.review-2 --code WORDS_HEADING_ORPHANED --code WORDS_PAGE_BLANK --output json
```

`words render` exports page images for delivery. It takes the format from
`--to`, or from the `--out` extension when `--to` is omitted, and refuses a
`--to` that disagrees with the `--out` extension.
