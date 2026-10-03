# Live preview for a person

`preview` shows a document in the local viewer and follows every change to
the file. It is a review aid for people; agents verify with reads and
`review` evidence (`aspose-cli docs verification`).

```text
aspose-cli preview <file> --open --output json
```

The result carries `id`, `product`, `url`, `pid`, `file`, `view`, `reused`
and the `license` mode the document rendered under. Content detection selects
the product; `--product` overrides it and `--view` picks another of the
product's views (`capabilities` lists them under `products[].preview.views`).
`--open` launches the default browser unless `ASPOSE_CLI_NO_OPEN=1` is set.

## Lifecycle

```powershell
aspose-cli preview status --output json
aspose-cli preview stop --all --output json
```

`preview status <id>` and `preview stop <id>` address one document. `status`
lists each open document in `sessions[]`, with the same `id`, `product`,
`url`, `file` and `view` as the start result and its current `revision`;
`stop` names the closed documents in `stopped[]` and those still open in
`sessions[]`; `stop --all` closes every document and ends the service.

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

## Licensing

Each document keeps the license it was opened with. Installing or removing a
license recycles the renderer behind the service, so the next render applies
it while open documents stay on screen. A license the engine refuses fails
that document alone. Licensing a preview does not remove evaluation marks
already saved in a file; regenerate the file with the license
(`aspose-cli docs licensing`).

## The App

`aspose-cli app` opens the same viewer inside a browser workspace for people
who prefer a guided setup; see `aspose-cli docs app`.
