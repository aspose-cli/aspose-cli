# Edit a contract safely

```powershell
aspose-cli words inspect contract.docx --detail outline bookmarks comments --output json
aspose-cli words edit contract.docx --ops update-ops.json --out contract.review.docx --track-changes --author "Legal Ops" --verify --output json
aspose-cli words inspect contract.review.docx --detail comments --output json
```

The input must contain the `Termination` heading targeted by `update-ops.json`.
The output retains tracked revisions. `words compare` requires revision-free
inputs, so compare reviewed copies only after an authorized accept/reject step.
