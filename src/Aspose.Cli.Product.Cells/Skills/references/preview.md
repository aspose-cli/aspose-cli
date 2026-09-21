# Managed workbook preview

Open the workbook in the local viewer and keep it live:

```powershell
aspose-cli preview book.xlsx --output json
```

The result contains `id`, `url`, `pid`, `file`, `view`, `license` and
`reused`. One viewer service per user serves every open document: it binds
loopback only, re-renders after a safe save, keeps the last good revision on
screen when a render fails, and never modifies the workbook.

The default `workbook` view is the product's own grid, so cell text stays
selectable and an edit is patched into the grid cell by cell: after each
`cells edit --in-place` the edited cells light up where they are, and the
sheet, scroll position and everything the edit did not touch stay put. For a
live demonstration, add `--fx demo` so a pointer travels to what changed:

```powershell
aspose-cli preview book.xlsx --fx demo --open --output json
```

Inspect or close documents through the same lifecycle:

```powershell
aspose-cli preview status --output json
aspose-cli preview status <id> --output json
aspose-cli preview stop <id> --output json
aspose-cli preview stop --all --output json
```

Use `--view sheets` for one rendered image per sheet instead of the grid; the
page's own toolbar switches the theme and turns the demo pointer on or off
while watching, so neither needs the document reopened.
`--port` chooses the service's loopback port when it starts. `--open`
launches the default browser unless `ASPOSE_CLI_NO_OPEN=1` is set. Opening
the same file the same way returns the document already open
(`reused: true`).

Preview is a human review aid. Agent verification still uses deterministic
`cells query range`, `cells compare` and `review` output. Evaluation-mode
previews disclose the evaluation state; report any resulting output watermark
when delivering files.
