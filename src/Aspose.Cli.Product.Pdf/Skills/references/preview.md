# PDF live preview

Open the document in the local viewer and keep it live:

```powershell
aspose-cli preview report.pdf --open --output json
aspose-cli preview status --output json
aspose-cli preview stop <id> --output json
```

Valid PDF content resolves to the PDF product and its `pages` view; the file
extension constrains candidate products but is not content proof. The page
reads like a PDF reader: pages, thumbnails, the size of the page in view,
zoom, and a mark on what a change touched. It does not show Words headings or
Cells worksheets. Passwords use `--password-env` or `--password-stdin` and
never enter result envelopes.

One viewer service per user serves every open document; `stop --all` ends it,
and it ends itself once nothing has been rendered or looked at for a while.
Opening the same file the same way returns the document already open
(`reused: true`).

Each document carries the license it was opened with, and the result's
`license` says which mode rendered it. Installing or removing a license
recycles the renderer behind the service, so the next render applies it
without restarting anything. A license the engine refuses fails that document
alone. Check the `pdf` entry in `license status --output json` when a mode is
not what you expect. Licensing a preview does not remove evaluation
watermarks already saved into a PDF; regenerate that file from the original
inputs with the valid license.

The page supports human inspection. Agents collect `pdf query pages`,
`pdf query search`, `pdf validate` and `review` evidence, and open the review
images before claiming a visual pass.

The page's own toolbar switches between light and dark and turns the demo
pointer on or off while watching; neither needs the document reopened.
