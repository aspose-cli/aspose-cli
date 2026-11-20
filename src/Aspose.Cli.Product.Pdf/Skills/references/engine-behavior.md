# Engine behavior

What Aspose.PDF does with what an edit or a conversion gives it. The CLI passes each of these on
unchanged; the ones that are engine defects are entries of `KNOWN-ISSUES.md`.

## Pages

- `add page` with a `page` copies it within the document (`Pages.Insert`); the copy has the
  page's content and annotations. Deleting the original leaves links that led to it leading
  nowhere; bookmarks and other links are renumbered.
- A new empty page has the engine's default size, A4, and no font.
- `resize_page` scales the content; setting `mediaBox` or `cropBox` does not.

## Forms

- A text, check box, combo or list field takes any text as `value`, also a state a check box
  does not list. A radio group's `value` is one of its options' names.
- `flatten_form` and `flatten_field` make the values page content; the fields are gone.
- Merging two documents with fields of one name renames the later ones (`name1`).
- Copying a page that holds form controls within its own document leaves the field count as it
  was and gives the copy more widgets than the original.

## Annotations and attachments

- An annotation's position among its page's is the engine's; adding or deleting one changes the
  positions after it. A link added with `add link` goes last.
- An attachment's position follows the keys of the document's name tree, so adding one can move
  others. A description set on an attachment the engine read is not saved.

## Text, search and redaction

- `TextAbsorber` reads a page's text as laid out; runs set apart by a small gap read as words
  with a space between them, which a search without the space does not find.
- `redact_area` removes what lies in the rectangle and draws its fill there. Text after the
  removed text on the same line can move left, so a second redaction on that line from a
  rectangle read before the first may cover the wrong text: redact from right to left, or search
  again between them. On a rotated page the engine can leave the text under the fill.
- Searching a page with `TextFragmentAbsorber` adds a saved graphics state to a page that holds a
  stamp, in memory; `search` runs on a copy no edit saves.

## Saving and converting

- Reading the structure tree (`--at taggedContent`) makes the engine retitle the document and
  mark it PDF/UA in memory; no other read does, and nothing saves after it.
- An HTML import writes the folder of a relative link into the link, and sets its own title,
  author and subject; set `info.title` and the others to change them.
- A PDF/A conversion with `Delete` removes what the profile forbids, such as attachments of
  PDF/A-1; the log names what it changed.
- Encrypting a document whose information holds text beyond Latin-1 writes that text garbled.
