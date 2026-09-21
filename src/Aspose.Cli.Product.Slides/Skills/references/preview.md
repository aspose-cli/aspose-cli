# Slides preview

Open the deck in the local viewer and keep it live:

```powershell
aspose-cli preview deck.pptx --open --output json
```

Inspect or close documents:

```powershell
aspose-cli preview status --output json
aspose-cli preview stop <id> --output json
aspose-cli preview stop --all --output json
```

The deck reads like a slide editor: a numbered slide rail, one slide on the
stage with its speaker notes, a full-screen slideshow, and a mark on the
shapes an edit changed. It is a human review aid; agents use deterministic
static commands and `review` evidence for final proof.

One viewer service per user serves every open document, so `status` and
`stop` work across products in the same current-user CLI configuration, and
`stop --all` ends the service. Opening the same file the same way returns
the document already open (`reused: true`); the result reports the `license`
mode the render ran under. A license the engine refuses fails that document
alone and leaves what is already open rendering.

The page's own toolbar switches between light and dark and turns the demo
pointer on or off while watching; neither needs the document reopened.
