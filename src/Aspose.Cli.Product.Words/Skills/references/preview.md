# Words live preview

Open the document in the local viewer and keep it live:

```
aspose-cli preview contract.docx --open --output json
aspose-cli preview status --output json
aspose-cli preview stop <id> --output json
```

Content detection routes supported Word-processing input to Words, which
renders its `pages` view. The result identifies `product: "words"`, the `url`
of that document and the `license` mode it rendered under. Use
`--product words` to choose Words explicitly for a supported input. Passwords
use the standard `--password-env` or `--password-stdin` sources and never
enter the result.

One viewer service per user serves every open document, so `status` and `stop`
work across products in the same current-user CLI configuration; `stop --all`
closes everything and ends the service, which also ends itself once nothing
has been rendered or looked at for a while.

Opening the same file the same way returns the document already open
(`reused: true`); a different view, license, password or font profile opens
its own. A license the engine refuses fails that document alone and leaves
what is already open rendering.

The page is for people: pages, an outline of the document's headings, zoom,
and a mark on the paragraphs an edit changed. It is not a data source.
Agents verify with `words query blocks`, `--verify`, comparison and `review`.
