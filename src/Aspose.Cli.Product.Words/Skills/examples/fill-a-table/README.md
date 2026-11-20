# Fill a table

Copy this Skill to a fresh writable directory, then change into examples/fill-a-table.
[table-ops.json](table-ops.json) sets the new document's title and builds a table of two rows and
two columns, node by node: a table, its rows, each row's cells, each cell's paragraph, each
paragraph's run; [row-ops.json](row-ops.json) clones the second row, with its formatting and text,
and changes the clone's text.

```powershell
aspose-cli words create milestones.docx --output-format json
aspose-cli words edit milestones.docx --ops table-ops.json --out milestones.1.docx --output-format json
aspose-cli words edit milestones.1.docx --ops row-ops.json --out milestones.2.docx --output-format json
aspose-cli words read milestones.2.docx --at section/0/body/table/0 --kind cell --kind paragraph --output-format json
aspose-cli words convert milestones.2.docx --out milestones.pdf --output-format json
```

The engine's new document holds one empty paragraph; the table goes after it, as
`section/0/body/table/0`. Each add's entry gives the address of what it added. The clone goes last
in the table, `row/2`, and its runs are the copies of `row/1`'s, so the second edit sets their
text. Look at the PDF before handing the document over.
