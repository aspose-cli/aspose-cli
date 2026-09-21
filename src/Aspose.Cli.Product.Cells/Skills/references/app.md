# Local browser App

For a human-first setup and preview flow, open the local App:

```
aspose-cli app
aspose-cli app report.xlsx
```

The App listens only on `127.0.0.1`; spreadsheet and document processing stays
on the local machine. Its welcome page installs a `.lic` file or continues in
evaluation mode, the Files page opens an original file with the operating
system's picker, and the Preview page shows the document through the same
per-user viewer service as the global `preview` command, following external
saves as they happen. Browser uploads are explicitly labelled as temporary
preview copies.

Several documents stay open at once. Each has a tab above the preview;
switching tabs frames the document that was already rendering, so it comes
back to the sheet, page and scroll position it was showing. Closing the last
tab returns to the workspace. The view selector beside the tabs shows the same
file another way — for a workbook, the selectable grid or one image per sheet —
and the document keeps its tab. The theme control applies to the App and to
every document it frames.

Settings shows each product's independent license state, effective source and
priority, preview defaults, fonts/runtime diagnostics, and local-data controls.
A Total license is detected once and installed for every compatible product;
product-only licenses can be installed and removed independently. The App holds
no engine of its own, so installing or removing a saved license changes nothing
about the App process: the viewer service recycles its renderer and the next
render applies the license, with open documents left on screen meanwhile. The
App uses the same source precedence as new CLI commands; a higher-priority
explicit or environment source remains effective. A license the engine refuses
fails the document that asked for it and leaves everything else rendering.

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
