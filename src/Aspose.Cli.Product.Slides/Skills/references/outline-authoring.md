# Outline authoring

Markdown is the deterministic path from notes to a first draft. The outline is
mapped onto the template's own layouts; the template decides fonts, colors,
backgrounds and placement, and tables take the template's default table style.
Only emphasis (bold, italic), code (a monospace font) and table column alignment
are applied to the text itself.

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --template brand.pptx --output json
```

| Markdown | Result |
|----------|--------|
| `# Title` | Title Slide layout; following paragraphs fill the subtitle, and a picture or table starts a continuation slide |
| `## Title` | Title and Content layout; the body holds the content below it |
| `## Title` with no content | Title Only layout |
| `### Heading` (and deeper) | Bold body paragraph without a bullet |
| `- item`, `* item`, `+ item`; indented `  - item` | Bullets from the layout; each two spaces of indent is one level |
| `1. item` or `1) item` | Numbered paragraphs (1., 2., ...) at the indented level |
| Plain paragraph, `> quote` | Body paragraphs without bullets; a quote is wrapped in quotation marks |
| `**strong**`, `*emphasis*`, `` `code` ``, a link | Bold, italic and monospace runs; a link keeps its text only |
| Fenced code block | Body paragraphs in a monospace font |
| Markdown image of a local file, with optional `"title"` after the path | Picture with the image's alternative text and title; with body text the slide uses Two Content |
| Pipe table: header row, delimiter row (`\|---\|:-:\|--:\|`), body rows | Table with a header row; see [Tables](#tables) |
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

## Tables

```markdown
## Pipeline
Deals by stage
| Stage | Owner | Value |
|:------|:-----:|------:|
| **Won** | A\|B | 12 |
```

- The table fills the width of the free content placeholder, top-aligned. Text
  before or after it on the slide goes to the body, and the slide uses Two
  Content (text first, table second) as it does for a picture.
- A slide holds one picture or table. A further table, or a picture after a
  table, starts a continuation slide with the same title, which also takes the
  content that follows. A title slide's subtitle holds text only, so a table or
  picture under `# Title` starts such a continuation slide too.
- The first row is the table's header row, styled by the template. Colons in
  the delimiter row align a column left, center or right; without them the
  table style decides.
- Cells accept emphasis, code spans and links like paragraphs. A backslash
  escapes only the next character: `\|` is a literal pipe, while in `\\|` the
  pipe still ends the cell. A shorter row gets empty cells, a longer one loses
  its extra cells.
- A pipe line not followed by a delimiter row with as many cells stays a
  paragraph. The table ends at a blank line, a heading or a line without a pipe.
- A table holds at most 100 rows and 50 columns; a larger one is
  `FEATURE_UNSUPPORTED`. Rows grow with their text, and a table that ends below
  its placeholder is reported as a `TABLE_OVERFLOW` warning located at its
  slide: split it across slides under the same heading or shorten its cells.

After creation, read the slides with `slides query slides --scope shapes` before adding
charts, images, notes or transitions with `slides edit`.
