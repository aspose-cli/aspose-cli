# Document standards

## Template

The template carries the design: Normal and Heading styles, page size and
margins, headers, footers and page numbers. Without `--template`,
`words create` uses the built-in A4 design: A4 pages, Calibri body text with
Microsoft YaHei for CJK text, navy and teal headings with a rule under
Heading 1, a teal-barred Quote style and a centered page number.
`words create base.docx`, without a content option or template, writes that design as a starting point for a
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
- `--from-markdown`, `insert_markdown` and Markdown headers or footers import
  into the document's styles: headings map to Heading 1-6, quotes to Quote,
  lists to list paragraphs, and footnote references (`text[^1]` with a
  `[^1]: note` line) to real footnotes. Put every footnote definition at the
  end of the Markdown: a heading right after one joins the note (known SDK
  issue `WORDS-MD-FOOTNOTE-HEADING`). As in CommonMark, a closing `**` or `~~`
  right after punctuation, such as `》` or `）`, needs a space or punctuation
  after it, so `~~原《守则》~~同时` stays literal; write `<del>`, `<strong>` or
  `<em>` tags in Chinese text instead, and read the result back for leftover
  markers.
- A Markdown image is read only from an ordinary file beneath a directory, by
  relative path: the Markdown file's directory for `words create
  --from-markdown`, the input document's directory for `insert_markdown`. A
  missing image, one outside that directory and a network image are left out,
  and `REMOTE_RESOURCES_BLOCKED` names each one as the Markdown wrote it, with
  the reason. To use an image from elsewhere, copy it into that directory or a
  folder below it and reference it by a relative path.
- Markdown has no Title syntax, and its `#` becomes Heading 1, which the table
  of contents lists. For a document title, write it as a plain first line,
  write the chapters as `#`, and restyle that line in the batch that inserts
  the table of contents; check that `inspect --detail outline` no longer lists
  it.

  ```json
  {"ops":[
    {"op":"set_style","target":{"block":1},"style":"Title"},
    {"op":"insert_toc","at":{"block":1},"position":"after"}
  ]}
  ```
- A nested Markdown list numbers its sub-items 1., 2. again, not as contract
  clauses 1.1, 1.2. Renumber them after the import with one `set_list` that
  covers all the sub-clauses of an article, at `"level": 2` with `numberStyle`
  `decimal` and `numberFormat` `"%1.%2"`. For this Markdown, whose blocks 4 and
  5 are the sub-clauses (find them with `words read`):

  ```markdown
  Agreement

  # General

  1. Article one
     1. Clause one
     2. Clause two
  2. Article two
  ```

  ```json
  {"ops":[
    {"op":"set_list","target":{"blocks":"4-5"},"kind":"number","level":2,"numberStyle":"decimal","numberFormat":"%1.%2"}
  ]}
  ```

  `listLabel` in `words read` then shows 1., 1.1, 1.2, 2. Each `set_list`
  makes a new list that counts from 1.1, so one op per sub-clause numbers each
  of them 1.1. A new list does not count the articles either: give a later
  article's sub-clauses its number as text, such as `"numberFormat": "2.%2"`
  for 2.1, 2.2.
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
layout (known SDK issue `WORDS-CJK-LINE-BREAK`). Documents created with the
built-in design avoid it; one created from another template or converted
from another format keeps its own settings, which no operation changes, so
disclose such lines rather than adding breaks.
