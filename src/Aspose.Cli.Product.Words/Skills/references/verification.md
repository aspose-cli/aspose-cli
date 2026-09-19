# Verification

## Content

`words edit --verify` reopens the exact staged file before publication and
reports:

- `ok` and `issues`;
- `semanticChangesDetected` from a comparison of private copies with revisions
  accepted, so the saved document keeps its revisions;
- `fieldCount`, `revisionCount` and `protection`, each checked against the
  in-memory result.

`--verify` cannot be combined with `--dry-run`. Failed content checks are a
partial-success report (exit 8) and still publish the output for repair;
execution failures prevent publication.

Then read back what changed:

```powershell
aspose-cli words inspect output.docx --detail outline fields comments --output json
aspose-cli words query blocks output.docx --blocks 1-30 --scope full --output json
aspose-cli words compare baseline.docx output.docx --output json
```

`words compare` requires revision-free inputs.

## Appearance

```powershell
aspose-cli review output.docx --out output.review-1 --output json
```

Open every page image under the review directory, one by one; JSON success
alone does not prove layout quality. `review.json` lists findings and
`coverage.complete`; when coverage is incomplete, say so. Fix and review again
into a new directory, for at most three rounds, then report any remaining
defects.
