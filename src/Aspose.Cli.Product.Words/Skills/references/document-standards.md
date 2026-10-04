# Document standards

## Template

The template carries the design: Normal and Heading styles, page size and
margins, headers, footers and page numbers. Without `--template`,
`words create` uses the built-in A4 design: A4 pages, Calibri body text with
Microsoft YaHei for CJK text, navy and teal headings with a rule under
Heading 1, a teal-barred Quote style and a centered page number.
`words create base.docx --blank` writes that design as a starting point for a
brand template. With `--template` and no content option, `words create` keeps
the template's own body.

To change the look, edit the template in Word once and reuse it. Do not
correct a design paragraph by paragraph.

## Structure

- Use built-in Heading styles for every heading; never fake one with bold text.
  The outline and the table of contents list paragraphs with an outline level,
  which Heading styles have; the built-in design's Title and Subtitle have
  none, so a cover title or a "Contents" caption in them stays out of the
  table of contents. A template's own Title may differ: check `inspect
  --detail outline`.
- Use real tables for tabular data; never align columns with spaces or tabs.
- Keep one style per role; apply `set_style`, or create one with
  `define_style`, instead of `format_text` on individual runs.
- Use section-specific page setup only when layout requirements differ.
- Keep headers, footers and page numbers consistent across linked sections.
- Set the document title with `--title` or `set_properties`.

## What to look for in review

Heading hierarchy, paragraph flow, widows and orphans, table widths and header
rows, list numbering, image placement and captions, headers, footers and page
numbers, section breaks, clipping, tracked-change and comment visibility, and
missing CJK glyphs.

Chinese, Japanese or Korean lines that start with "，", "。" or "：", or that
end early before East Asian text following a space, come from the engine's
layout (known SDK issue `WORDS-CJK-LINE-BREAK`): it applies the East Asian
line-breaking rules only to text whose East Asian language is Chinese,
Japanese or Korean, and, in a document older than Word 2013's compatibility
mode, breaks East Asian text drawn in one font for all scripts, as a PDF
input loads, only at spaces. The built-in design declares Chinese; a
document created from another template or converted from another format
keeps its own settings, so disclose such lines rather than adding breaks.
