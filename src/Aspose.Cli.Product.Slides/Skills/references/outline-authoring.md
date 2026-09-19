# Outline authoring

Markdown is the deterministic path from notes to a first draft. The outline is
mapped onto the template's own layouts; the template alone decides fonts,
colors, backgrounds and placement.

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --template brand.pptx --output json
```

| Markdown | Result |
|----------|--------|
| `# Title` | Title Slide layout; following paragraphs fill the subtitle |
| `## Title` | Title and Content layout; the body holds the content below it |
| `## Title` with no content | Title Only layout |
| `- item`, indented `  - item` | Bullets; each two spaces of indent is one level |
| Plain paragraph, `> quote` | Body paragraphs without bullets |
| Fenced code block | Body paragraphs in a monospace font |
| Markdown image of a local file | Picture; with body text the slide uses Two Content |
| `---` | Starts a new section at the next slide |

The template's existing slides are replaced; its masters, layouts, theme and
slide size are kept. A layout type the template lacks falls back to Title and
Content. Unused placeholders are removed, and body text uses the layout's
autofit so PowerPoint shrinks overflowing text.

Without `--template`, the SDK's plain default theme is used. Prefer the bundled
`assets/templates/default-16x9.pptx` or the user's brand template, and choose
`--size 16x9` or `--size 4x3` only when no template sets the size.

After creation, inspect the slide inventory before adding charts, tables,
images, notes or transitions with `slides edit`.
