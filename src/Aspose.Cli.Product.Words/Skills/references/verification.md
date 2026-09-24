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
`coverage.complete`; when coverage is incomplete, say so. Review still writes
its evidence but exits 8 when coverage is incomplete or a finding has `error`
severity, such as `FONTS_MISSING_OR_SUBSTITUTED`. Fix and review again into a
new directory, for at most three rounds, then report any remaining defects.

```powershell
aspose-cli fonts check output.docx --output json
```

`fonts check` lists the fonts the document's text uses, marks those unavailable
here, and names in `substitutedBy` the font the layout draws instead. For fonts
delivered beside the document, such as brand fonts on a machine without them,
pass the same `--font-dir` to every command whose result depends on layout:
`fonts check`, `review`, and `words render`, `convert`, `create`, `edit`,
`compare`, `split` and `inspect` (its page count). It adds to the system fonts,
so the output uses the fonts the check saw.

```powershell
aspose-cli fonts check output.docx --font-dir fonts --output json
aspose-cli words convert output.docx --to pdf --font-dir fonts --output json
```
