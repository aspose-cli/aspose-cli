# Verification

For an existing user document, preserve one baseline before the first edit.
Apply related changes in one batch with `--verify`; verification is off by default
and cannot be combined with `--dry-run`.

Verification reopens the produced file, reports up to 20 read-back block ids,
checks field/revision/protection state and renders pages at 150 DPI. Documents
up to 20 pages render every page. For longer documents, touched and adjacent
pages plus the first and last pages form a candidate set; the first 12 in page
order are rendered, so the last page is not guaranteed. Check
`verification.renders` for actual coverage and `verification.visualReviewRequired`
for a required wider review.

Inspect `verification.ok` and `verification.issues`. Verification runs after
save; an unsuccessful verification can leave a saved output requiring repair.
Its semantic comparison accepts revisions only in private clones, preserving
revisions in the saved document.

After saving, compare only when both inputs are revision-free:

```powershell
aspose-cli words inspect output.docx --detail outline fields comments --output json
aspose-cli words query blocks output.docx --blocks 1-30 --scope full --output json
aspose-cli words compare baseline.docx output.docx --output json
```

Actually inspect rendered images. JSON success alone does not prove layout quality.
