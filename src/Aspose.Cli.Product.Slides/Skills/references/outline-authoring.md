# Outline authoring

Markdown is the deterministic path from notes to a first presentation draft.
`#` starts a title slide and `##` starts a content slide. Following paragraphs
and bullet lists become body content. A `---` separator starts a section at the
next slide.

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --template brand.pptx --output json
```

`--template` retains the source masters, layouts, theme and default slide size,
but Markdown authoring replaces the slides and applies its own background and
explicit Arial/Consolas fonts. Use an edit batch for brand typography and colors.
Without a template, choose `--size 16x9` or `--size 4x3`. After creation, inspect
the slide inventory before adding charts, tables, images, notes or transitions.
