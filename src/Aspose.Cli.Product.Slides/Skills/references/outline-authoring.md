# Outline authoring

Markdown is the deterministic path from notes to a first draft. The outline is
mapped onto the template's own layouts; the template decides fonts, colors,
backgrounds and placement. Only emphasis (bold, italic) and code (a monospace
font) are applied to the text itself.

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --template brand.pptx --output json
```

| Markdown | Result |
|----------|--------|
| `# Title` | Title Slide layout; following paragraphs fill the subtitle |
| `## Title` | Title and Content layout; the body holds the content below it |
| `## Title` with no content | Title Only layout |
| `### Heading` (and deeper) | Bold body paragraph without a bullet |
| `- item`, `* item`, `+ item`; indented `  - item` | Bullets from the layout; each two spaces of indent is one level |
| `1. item` or `1) item` | Numbered paragraphs (1., 2., ...) at the indented level |
| Plain paragraph, `> quote` | Body paragraphs without bullets; a quote is wrapped in quotation marks |
| `**strong**`, `*emphasis*`, `` `code` ``, a link | Bold, italic and monospace runs; a link keeps its text only |
| Fenced code block | Body paragraphs in a monospace font |
| Markdown image of a local file, with optional `"title"` after the path | Picture with the image's alternative text and title; with body text the slide uses Two Content |
| `---` | Starts a new section at the next slide |

The template's existing slides are replaced; its masters, layouts, theme and
slide size are kept. A layout type the template lacks falls back to Title and
Content. Unused placeholders are removed. Body placeholders are set to shrink
text on overflow; still check review for overflow and split a crowded slide
rather than rely on shrinking. Wrap an image path that contains spaces in angle
brackets; remote images are refused.

Without `--template`, the built-in 16:9 design is used. `--size 16x9` or
`--size 4x3` resizes the canvas and scales the masters, layouts and slides with
it. Without `--from-markdown`, the template's own slides are kept; a template
that has none yields one empty Title Slide.

After creation, inspect the slide inventory before adding charts, tables,
images, notes or transitions with `slides edit`.
