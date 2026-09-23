# Report from Markdown

Copy this Skill to a fresh writable directory, then change into
examples/report-from-markdown. The bundled
[report.md](report.md) contains synthetic quarterly figures.

```powershell
aspose-cli words create report.docx --markdown report.md --title "Quarterly Report" --output json
aspose-cli words inspect report.docx --detail outline properties fonts --output json
aspose-cli review report.docx --out report.review --output json
```

The document contains a Summary heading and a two-row metrics table, using
the built-in design's styles and A4 page setup. Open every image listed by
the review before delivery and disclose evaluation output when applicable.
