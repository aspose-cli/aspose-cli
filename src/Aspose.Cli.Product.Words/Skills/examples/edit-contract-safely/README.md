# Edit a contract safely

Copy this Skill to a fresh writable directory, then change into
examples/edit-contract-safely. [contract.md](contract.md) is a synthetic contract fragment;
[update-ops.json](update-ops.json) changes its notice period and adds a comment.

```powershell
aspose-cli words create contract.docx --markdown contract.md --output json
aspose-cli words inspect contract.docx --detail outline bookmarks comments --output json
aspose-cli words edit contract.docx --ops update-ops.json --out contract.review.docx --track-changes --author "Legal Ops" --verify --output json
aspose-cli words inspect contract.review.docx --detail comments --output json
aspose-cli review contract.review.docx --out contract.review --output json
```

The source keeps the original thirty-day notice period. The output retains
tracked revisions for the proposed forty-five-day period and a comment on
the Termination heading. Open every review image and disclose evaluation output.

`words compare` requires revision-free inputs, so compare reviewed copies only
after an authorized accept/reject step.
