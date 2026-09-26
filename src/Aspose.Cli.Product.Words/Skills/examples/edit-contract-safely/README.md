# Edit a contract safely

Copy this Skill to a fresh writable directory, then change into
examples/edit-contract-safely. [contract.md](contract.md) is a synthetic contract fragment;
[update-ops.json](update-ops.json) changes its notice period and comments on the
Termination heading.

```powershell
aspose-cli words create contract.docx --markdown contract.md --output json
aspose-cli words inspect contract.docx --detail outline bookmarks comments --output json
aspose-cli words edit contract.docx --ops update-ops.json --out contract.review.docx --track-changes --author "Legal Ops" --verify --output json
aspose-cli words query search contract.review.docx --pattern "forty-five" --output json
aspose-cli words inspect contract.review.docx --detail comments --output json
aspose-cli review contract.review.docx --out contract.review --output json
```

The source keeps the thirty-day notice period. The output holds the
forty-five-day period as tracked revisions by Legal Ops and their comment on
the Termination heading, and `verification`
reports `semanticChangesDetected` with a nonzero `revisionCount`. Review reports
`WORDS_REVISIONS_PRESENT`: leave the revisions for the user to accept or reject.

`words compare` needs revision-free inputs, so compare copies only after the
user has decided on the revisions.
