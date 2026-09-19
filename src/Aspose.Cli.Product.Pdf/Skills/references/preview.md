# PDF live preview

For normal agent work, start the shared managed lifecycle:

```powershell
aspose-cli preview report.pdf --open --output json
aspose-cli preview status --output json
aspose-cli preview stop <id> --output json
```

Valid PDF content resolves to the PDF product and its default `pages` view;
the file extension constrains candidate products but is not content proof. The product
shell exposes PDF pages, dimensions, navigation, zoom, render progress and
last-good failure recovery. It does not show Words headings/revisions or Cells
worksheets/formulas. Passwords use `--password-env` or `--password-stdin` and
never enter result envelopes or session markers.

Preview startup validates the selected PDF license with the real SDK before
reuse. An unchanged effective license can reuse a matching session. After
installing, replacing or removing a license, run the preview command again:
a changed license identity replaces that matching session with a new process.
A configured invalid license fails before an existing session is stopped.
A normal document refresh alone does not switch the running process's license.

Use `--license <path>` or the PDF/shared license environment variables when
an isolated configuration needs an explicit source. Check the `pdf` product
entry in `license status --output json` and the preview start result's license
state. Licensing a preview does not remove evaluation watermarks or restore
content already altered in a previously saved evaluation PDF; regenerate that
artifact from the original inputs with the valid license.

The browser supports human inspection. Agents collect `pdf query pages`,
`pdf query search`, `pdf validate` and `review` evidence, and open the review
images before claiming a visual pass.
