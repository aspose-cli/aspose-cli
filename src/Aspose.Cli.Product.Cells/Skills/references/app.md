# Local browser App

For a human-first setup and preview flow, open the local App:

```
aspose-cli app
aspose-cli app report.xlsx
```

The App listens only on `127.0.0.1`; spreadsheet and document processing stays
on the local machine. Its welcome page installs a `.lic` file or continues in
evaluation mode, the Files page opens an original file with the operating system's picker,
and the Preview page follows external saves through the same product-routed
`PreviewRuntime` as the global `preview` command. Browser uploads are explicitly
labelled as temporary preview copies.

Settings shows independent Cells and Words license state, effective source and
priority, preview defaults, fonts/runtime diagnostics, and local-data controls.
A Total license is detected once and installed for every compatible product;
product-only licenses can be installed and removed independently. Installing
refreshes the current preview. Removing a saved license performs a controlled
App restart because an Aspose engine applies a license for the lifetime of its
process.

Useful lifecycle commands:

```
aspose-cli app --welcome
aspose-cli app status --output json
aspose-cli app stop
aspose-cli app --foreground --port 4680
```

`--foreground` is intended for debugging, containers, and browser automation.
Set `ASPOSE_CLI_NO_OPEN=1` when a script must not launch the default browser.
CLI commands, global `preview`, and `--license` remain the preferred interfaces
for agents and CI.
