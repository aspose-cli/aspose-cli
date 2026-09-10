# Outline authoring

Markdown is the deterministic path from notes to a first presentation draft.
Each level-one heading starts a slide. The heading becomes the title; following
paragraphs and bullet lists become body content.

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --template brand.pptx --output json
```

Use a template when brand masters, layouts or theme fonts matter. Without a
template, explicitly choose `--size 16x9` or `--size 4x3`. After creation,
inspect the slide inventory and use an edit batch for charts, tables, images,
notes, transitions and precise styling.
