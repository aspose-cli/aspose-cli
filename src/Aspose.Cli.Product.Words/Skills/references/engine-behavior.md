# Engine behavior

What Aspose.Words does with what an edit or a conversion gives it. The CLI passes each of these on
unchanged; the ones that are engine defects are entries of `KNOWN-ISSUES.md`.

## Text and paragraphs

- A paragraph's text is its runs'. A new paragraph has no run and the style `Normal`; a new run
  takes its paragraph's formatting.
- `replace` matches across runs and may split them. While changes are tracked, it leaves a deletion and an insertion, and
  the paragraph's text holds both.
- A paragraph's `text` and a range's `text` hold the deleted text of tracked changes; the
  engine's `RevisionsView` does not change them.
- `insert_break` puts the break at the end of the paragraph; a section break starts the new
  section with an empty paragraph.
- Setting a paragraph's `paragraphFormat.styleName` to a built-in style the document lacks, such
  as `Heading 2`, adds that style to the document; a name that is neither is refused.

## Tables

- A new table, row or cell is empty. The engine does not keep a table without rows, or a row
  without cells, when it saves, and leaves an empty paragraph where such a table was. A cell
  without a paragraph is given one when it saves.
- Two tables with nothing between them are one table once the file is opened again.
- Setting a table's `styleName` clears the formatting the table sets itself.
- A horizontal merge is saved as one wider cell: `cellFormat.horizontalMerge` reads `None` after
  the save. A vertical merge is kept as `First` and `Previous`.

## Fields, headers and footers

- `add field` without `fieldValue` updates the new field at once; with it, the field shows
  `fieldValue` until it is updated. Opening and saving a document update no field.
- `update_page_layout` lays the document out; `update_fields` after it gives page numbers and
  tables of contents their pages.
- A header or footer that holds no paragraph reads `isLinkedToPrevious` true.
- A watermark is shapes in the headers, which the engine adds when there are none.

## Layout and counts

- Reading `layout`'s `pageCount` lays the document out. A layout recomputes the widths of table
  columns, which a later save writes; read, search and edit lay nothing out unless asked.
- `builtInDocumentProperties.words` and `listLabel.labelString` are as last updated:
  `update_word_count` and `update_list_labels` update them.

## Conversions

- Saving to ODT drops the results of `PAGE` and `NUMPAGES` fields and drops `=` formula fields.
- A plain text output writes each footnote's text where its reference mark is; Markdown writes
  footnotes at the end.
- A PDF is read by laying its pages out as paragraphs; an encrypted PDF needs its password, which
  the engine asks for when it opens it, though its format detection calls no PDF encrypted.
- Without a license, the engine adds a paragraph to the start of every document it opens, a line
  to its headers and footers, and cuts long documents short.
