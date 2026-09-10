# Verification

For an existing user document, preserve one baseline before the first edit. Apply related changes in one batch with `--verify`.

Verification reopens the produced file, reads back bounded blocks, checks document state and renders pages. Documents up to 20 pages render every page; longer documents render the first, last, touched and adjacent pages up to a bounded set and set `visualReviewRequired`.

After delivery:

```powershell
aspose-cli words inspect output.docx --detail outline fields comments --output json
aspose-cli words query blocks output.docx --blocks 1-30 --scope full --output json
aspose-cli words compare baseline.docx output.docx --output json
```

Actually inspect rendered images. JSON success alone does not prove layout quality.
