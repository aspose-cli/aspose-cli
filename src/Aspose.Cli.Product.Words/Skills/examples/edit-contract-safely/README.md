# Edit a contract safely

```powershell
aspose-cli words inspect contract.docx --detail outline bookmarks comments --output json
aspose-cli words edit contract.docx --ops update-ops.json --out contract.review.docx --track-changes --author "Legal Ops" --verify --output json
aspose-cli words compare contract.docx contract.review.docx --out contract.redline.docx --output json
```
