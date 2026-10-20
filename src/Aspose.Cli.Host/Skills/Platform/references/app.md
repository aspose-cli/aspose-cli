# The local browser App

`aspose-cli app open` opens a browser workspace for people: license setup, files
and live preview. Agents and scripts use the CLI commands, `preview` and
`--license` instead.

```text
aspose-cli app open
aspose-cli app open <file>
aspose-cli app open --welcome
aspose-cli app status
aspose-cli app stop
```

Without a file, `app open` opens the App's home page; with one, it opens that
file. The App listens only on `127.0.0.1`, and every document stays on the
local machine.

- **Welcome** (`app open --welcome`) installs a `.lic` file or continues in
  evaluation mode.
- **Files** opens an original file through the operating system's picker.
  Browser uploads are labelled as temporary preview copies.
- **Preview** shows each document through the same per-user viewer service as
  `preview` (`aspose-cli docs preview`). Several documents stay open, one tab
  each; switching tabs returns to the sheet, page or slide and scroll position
  the document was showing. The view selector shows the same file another
  way, and the theme control applies to the App and every document it frames.
- **Settings** shows each product's license state, effective source and
  priority, preview defaults, font and runtime diagnostics, and local-data
  controls.

Browser uploads stay within these limits for each App session; an
environment variable set before the App starts changes a limit within its
maximum:

| Variable | Default | Maximum | Limits |
|---|---|---|---|
| `ASPOSE_CLI_APP_MAX_UPLOAD_FILES` | `16` | `128` | uploaded files kept at once |
| `ASPOSE_CLI_APP_MAX_UPLOAD_SESSION_BYTES` | `536870912` | `2147483648` | bytes of all uploads together (512 MiB, at most 2 GiB) |

## Licenses in the App

A license file is validated before it is installed for this user. A Total
license is installed for every compatible product; product licenses install
and remove independently. The App follows the CLI's source precedence, so an
explicit or environment source keeps priority over a file installed here
(`aspose-cli docs licensing`). The viewer service outlives the shell that
started it, so the App reports and renders under the shell of the last
`app open`: its license environment, `--license` and working directory. The
App holds no engine: after a license change
the viewer service recycles its renderer and the next render applies it.

## Lifecycle

`app status` reports whether the App is `running`. `app stop` stops the App,
its documents and the viewer service. `app open --no-open` starts or activates
the App without a browser, and `app open --foreground` keeps it in the current
process for debugging, containers and browser automation; `--help` lists the
other options.
