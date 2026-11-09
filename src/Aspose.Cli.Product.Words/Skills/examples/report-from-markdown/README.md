# Report from Markdown

Copy this Skill to a fresh writable directory, then change into
examples/report-from-markdown. The bundled
[report.md](report.md) contains synthetic quarterly figures. Its first line is
plain text, because Markdown has no Title syntax and a `#` line goes into the
table of contents; [title-ops.json](title-ops.json) gives that line the Title
style.

```powershell
aspose-cli words create report.docx --from-markdown report.md --title "Quarterly Report" --output-format json
aspose-cli words edit report.docx --ops title-ops.json --in-place --output-format json
aspose-cli words inspect report.docx --detail outline --detail properties --detail sections --output-format json
aspose-cli review report.docx --out report.review --output-format json
```

The document contains the title, a Summary heading, the only entry of the
outline, and a three-row metrics table, a row of labels and two data rows, using
the built-in design's styles and A4 page setup.
Open every image listed by the review before delivery and disclose evaluation
output when applicable.
