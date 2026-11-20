# Fill and stamp a report

Copy this Skill to a fresh writable directory, then change into examples/fill-and-stamp.
[cover.html](cover.html) is a cover page with the text inputs `ReportTitle` and `PreparedFor`;
[body.html](body.html) is a page of figures. [ops.json](ops.json) fills the two fields and stamps
a page number at the bottom and DRAFT across each page.

```powershell
aspose-cli pdf convert cover.html --out cover.pdf --output-format json
aspose-cli pdf convert body.html --out body.pdf --output-format json
aspose-cli pdf merge cover.pdf body.pdf --out report.pdf --output-format json
aspose-cli pdf read report.pdf --kind field --output-format json
aspose-cli pdf edit report.pdf --ops ops.json --out report.filled.pdf --output-format json
aspose-cli pdf render report.filled.pdf --out report.png --output-format json
```

The engine imports each HTML page as one PDF page and names a text input's field after its
`name`; `read --kind field` shows the names to set. The merged document has two pages, so the
stamps name `page/1` and `page/2`, and each stamp's entry reads back that page's text. Look at the
rendered pages before handing the document over.
