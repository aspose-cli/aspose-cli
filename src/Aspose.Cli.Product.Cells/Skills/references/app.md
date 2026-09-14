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

Settings shows each product's independent license state, effective source and
priority, preview defaults, fonts/runtime diagnostics, and local-data controls.
A Total license is detected once and installed for every compatible product;
product-only licenses can be installed and removed independently. Installing or
removing a saved license performs a controlled App restart because SDK license
state belongs to a process. Uploaded preview copies are transferred before the
old process exits. The App uses the same source precedence as new CLI commands;
a higher-priority explicit or environment source remains effective.

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
