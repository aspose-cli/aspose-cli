# Slides editing

All addresses resolve against the original presentation before the first
operation is applied. Content inserted earlier in a batch cannot be addressed
by later operations in that same batch. Prefer stable `slideId` and `shapeId`
values obtained from `slides inspect` or `slides query slides` when slide order may change.

Apply one atomic batch:

```powershell
aspose-cli slides edit deck.pptx --ops deck-ops.json --out deck.revised.pptx --verify --output json
```

Use `--in-place --backup` only for an intentional in-place edit. Normal failure
writes no output. `--best-effort` commits successful operations and exits
8, so reserve it for workflows that explicitly accept partial delivery.

After `set_footer`, reopen the presentation and render every affected slide.
Confirm that footer text, dates, and slide numbers are visible inside the slide
canvas; template placeholders can retain geometry from an earlier slide size.

The frozen operation vocabulary is available offline:

```powershell
aspose-cli schema v2/slides/ops
aspose-cli docs slides/ops
```

Video and audio insertion or MP4 rendering are outside v1. Existing embedded
media can be inventoried and extracted, but must not be silently synthesized.
