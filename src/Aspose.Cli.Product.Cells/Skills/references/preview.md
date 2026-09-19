# Managed workbook preview

Start or reuse the product-routed background session:

```powershell
aspose-cli preview book.xlsx --view workbook --output json
```

The result contains `id`, `url`, `pid`, `file`, `view`, and `reused`. The
session binds only to loopback, refreshes after safe saves, preserves the last
good snapshot after a render failure, and never modifies the workbook.
Reuse requires matching product, file, view, selector, font profile,
presentation effect and validated license identity, with the same requested
port or `--port 0`. For a matching session, a changed license identity,
presentation effect or explicit port causes replacement; a rejected requested
license leaves it untouched.

After each `cells edit --in-place`, the workbook view spotlights the edited
ranges. For a live demonstration, add `--fx demo` so an animated cursor flies
to each change before it is highlighted:

```powershell
aspose-cli preview book.xlsx --fx demo --open --output json
```

Inspect or stop sessions through the same root lifecycle:

```powershell
aspose-cli preview status --output json
aspose-cli preview status <id> --output json
aspose-cli preview stop <id> --output json
aspose-cli preview stop --all --output json
```

Use `--view workbook` for workbook navigation or `--view sheet` for the active
sheet. Use `--port 0` to request a system-assigned port. `--open` launches the
default browser unless `ASPOSE_CLI_NO_OPEN=1` is set.

Preview is a human review aid. Agent verification still uses deterministic
`cells query range`, `cells compare` and `review` output. Evaluation-mode
previews disclose the evaluation state; report any resulting output watermark
when delivering files.
