# Mail-merge letters

```powershell
aspose-cli words edit letter-template.docx --ops merge-ops.json --out letters.docx --verify --output json
aspose-cli words query blocks letters.docx --blocks 1-20 --scope text --output json
aspose-cli review letters.docx --out letters.review --output json
```

Check representative letters in the review for long values, missing fields and
pagination.
