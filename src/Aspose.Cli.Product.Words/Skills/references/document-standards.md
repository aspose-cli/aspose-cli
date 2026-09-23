# Document standards

## Template

The template carries the design: Normal and Heading styles, page size and
margins, headers, footers and page numbers. Without `--template`,
`words create` uses the built-in A4 design: A4 pages, Calibri body text with
Microsoft YaHei for CJK text, navy and teal headings with a rule under
Heading 1, a teal-barred Quote style and a centered page number.
`words create base.docx` writes that design as a starting point for a brand
template.

To change the look, edit the template in Word once and reuse it. Do not
correct a design paragraph by paragraph.

## Structure

- Use built-in Heading styles for every heading; never fake one with bold text.
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
