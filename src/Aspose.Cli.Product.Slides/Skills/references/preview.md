# Slides preview

Start the managed product-routed preview:

```powershell
aspose-cli preview deck.pptx --open --output json
```

Inspect or stop the shared session:

```powershell
aspose-cli preview status --output json
aspose-cli preview stop <id> --output json
```

The slide browser shows thumbnails, current-slide navigation, live editing
feedback, evaluation status and last-good recovery. It is a human review aid;
agents must use deterministic static commands and rendered snapshots for final
evidence.

Run `preview` again after changing a license. A matching session is reused only
when the applied license identity also matches. A valid change of source, path
or license contents restarts it with a new `id` and `pid` and `reused: false`,
keeping the URL unless a different port is requested. An invalid selected
license is rejected before the existing session is stopped. Status and stop
use the same current-user CLI configuration as startup.
