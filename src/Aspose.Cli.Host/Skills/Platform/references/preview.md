# Live preview for a person

`preview open` shows a document in the local viewer and follows every change to
the file. It is a review aid for people; agents verify with reads and
`review` evidence (`aspose-cli docs verification`).

```text
aspose-cli preview open <file> --output-format json
```

It opens the viewer in the default browser; pass `--no-open` when no browser
should appear, such as in automation, and hand the result's `url` to the
person. Content detection selects the product; `--product` overrides it and
`--view` picks another of the product's views (`capabilities` lists them
under `products[].preview.views`). `aspose-cli schema v2/common/preview-session`
describes the result.

## Lifecycle

```powershell
aspose-cli preview status --output-format json
aspose-cli preview stop --output-format json
```

`preview status <id>` and `preview stop <id>` address one document, by the
`id` that `preview open` returned; `status` without an id lists every open
document, and `stop` without an id closes every document and ends the service.

## The viewer service

- One viewer service per user serves every open document across products. It
  binds to `127.0.0.1` only; `--port` chooses its port when it starts.
- It re-renders after each save of the file, including saves by other
  applications, and keeps the last good revision on screen when a render
  fails. It never modifies the document.
- Opening the same file the same way returns the document already open
  (`reused: true`); a different view, effect, license, password or font
  directory opens its own.
- It ends itself once nothing has been rendered or looked at for a while.
- `--fx demo` adds a pointer that travels to what each edit changed, for live
  demonstrations. The page's toolbar switches between light and dark and turns
  the pointer on or off without reopening the document.
- Passwords come from `--password-env` or `--password-stdin` and never appear
  in results; `--font-dir` applies as for every render.

## Limits

Each render, for the viewer and for `review` evidence alike, is a snapshot of
files kept within these limits. A render beyond one fails with
`PREVIEW_BUDGET_EXCEEDED`; the viewer keeps the last good revision on screen.
An environment variable changes a limit within its maximum; the viewer service
reads it when it starts:

| Variable | Default | Maximum | Limits |
|---|---|---|---|
| `ASPOSE_CLI_PREVIEW_MAX_FILES` | `2048` | `8192` | files in one snapshot |
| `ASPOSE_CLI_PREVIEW_MAX_FILE_BYTES` | `67108864` | `268435456` | bytes of one snapshot file (64 MiB, at most 256 MiB) |
| `ASPOSE_CLI_PREVIEW_MAX_SNAPSHOT_BYTES` | `268435456` | `1073741824` | bytes of one snapshot (256 MiB, at most 1 GiB) |

## Licensing

Each document keeps the license it was opened with; the result of a document
opened without a license carries its `license` mode, and a licensed one
omits `license`. Installing or
removing a license recycles the renderer behind the service, so the next
render applies it while open documents stay on screen. A license the engine
refuses fails that document alone. Licensing a preview does not remove
evaluation marks already saved in a file; regenerate the file with the license
(`aspose-cli docs licensing`).

## The App

`aspose-cli app open` opens the same viewer inside a browser workspace for
people who prefer a guided setup; see `aspose-cli docs app`.
