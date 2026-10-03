# Mail-merge letters

Run from a fresh writable copy of this example directory.
[letter-template.rtf](letter-template.rtf) is a small synthetic letter with
FirstName, LastName and Balance merge fields. [recipients.csv](recipients.csv)
contains two fictional recipients; [merge-ops.json](merge-ops.json) fills them.

```powershell
aspose-cli words convert letter-template.rtf --to docx --out letter-template.docx --output json
aspose-cli words edit letter-template.docx --ops merge-ops.json --out letters.docx --verify --output json
aspose-cli words query blocks letters.docx --blocks 1- --scope text --output json
aspose-cli review letters.docx --out letters.review --output json
```

The result holds one letter per recipient, Ava Stone and Noah Chen, each in
its own section with its balance, and the edit reports no `MERGE_VALUE_MISSING`.
Check every page image for long values and pagination.
