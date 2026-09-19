# Report from Markdown

Pour Markdown content into the bundled template (or the user's), check the
structure, then review every page:

```powershell
aspose-cli words create report.docx --markdown report.md --template default-a4.docx --title "Quarterly Report" --output json
aspose-cli words inspect report.docx --detail outline properties fonts --output json
aspose-cli review report.docx --out report.review --output json
```

`default-a4.docx` is `assets/templates/default-a4.docx` in this Skill.
