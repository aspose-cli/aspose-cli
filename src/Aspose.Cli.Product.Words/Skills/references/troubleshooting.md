# Words troubleshooting

`aspose-cli capabilities words --output-format json` lists every error and
warning code Words reports, with its severity and category and an error's exit code; the error
envelope, the `details` keys and general diagnosis are in
`aspose-cli docs troubleshooting`, and not-found details in
`aspose-cli docs editing`. This page covers the Words situations whose fix
the message does not give.

## An address finds nothing

Block and section numbers are 1-based and count the document as it was
before the batch: read them again with `words read --content outline` or
`inspect --detail sections` right before the edit. A `heading` or `find`
address matches visible text, ignoring case, and an `nth` past the matches
fails the same way ([editing](editing.md#addresses)). `words split --by
heading1` needs a Heading 1 paragraph, and warns `SPLIT_SINGLE_HEADING` when
there is only one, which divides the document at that heading alone: apply
Heading 1 to each chapter's title with `set_style`, or split `--by sections`
or `--by pages`. `define_style` creates a missing
style. A `revisions` number of `accept_revisions` or `reject_revisions`
refers to the list `words inspect --detail revisions` gives of the input.

## An operation changed nothing

Act on each `OP_NO_EFFECT` warning: `details.id` and `details.op` name the
operation and `details.reason` says why. When `accept_revisions`,
`reject_revisions` or `delete_comments` found no revision or comment by its
`author`, names match exactly and the hint lists the document's: do not
report the decision as made; correct the name and run the batch again. When
`replace_text` matched nothing in its `scope`, search with `words search`
and the same pattern; text in headers, footers, footnotes or comments needs
its `scope`.

## The batch or its data is refused

A tracked batch (`--track-changes`) holds only operations Word can record as
revisions, and comments ([editing](editing.md#batch-semantics)); run the
others in a separate batch. The data of `mail_merge` and `repeat_table_row`
must be usable as their pages describe ([mail merge](mail-merge.md),
[editing](editing.md#text)). `words compare` needs revision-free inputs: list
the revisions with `words inspect --detail revisions` and decide them first
([revisions](revisions.md)). `unprotect_document` with a wrong `passwordEnv`
password fails ([editing](editing.md#protection-and-encryption)).

## A PDF input

A PDF input is rebuilt as flowing text, and `convert`, `split` and `edit`
report `LOSSY_CONVERSION` for what may have changed. Its headers and footers
are guessed (known SDK issue `WORDS-PDF-HEADER-FOOTER`): they may become body
text, or real headers and footers in which a number, such as the "1" of a
version "第 1 版", becomes a page-number field that counts up on later pages;
the layout may also add pages. Check the output with `inspect --detail
sections fields` and correct it with `set_header`, `set_footer`,
`set_page_numbers` or `delete_blocks`. Chinese, Japanese or Korean text also
gets a space wherever a line ended on the PDF page, and the lines of a heading
and the clause after it may share one paragraph (`WORDS-PDF-CJK-LINE-END`):
read the blocks before editing, and find phrases with a regex that allows that
space, such as `甲 ?乙双方`.

## What a warning asks you to disclose

Besides the shared disclosures (`aspose-cli docs verification`), tell the
user about tracked changes and comments left in the output, invalidated
signatures (`SIGNATURE_INVALIDATED`), dropped macros (`MACROS_DROPPED`;
`inspect` reports `document.hasMacros` before any conversion) and fonts
embedded because the input's settings ask every save to embed them
(`FONTS_EMBEDDED`: the output can be megabytes larger, as with a docx written
by `pdf convert`).

Editing a restricted document succeeds and keeps the restriction
([editing](editing.md#protection-and-encryption)); tracked changes the input
had stay in the output and must be disclosed ([revisions](revisions.md)).
Macros survive only in `doc`, `dot`, `docm`, `dotm` and `wordml` outputs of
`convert`, `edit`, `create`, `split` and `compare`; save to `docm` or `dotm`
to keep them. With a license, an input that holds the evaluation banner,
footer sentence or truncation notice of an earlier unlicensed save keeps them
in the output, and the warning lists them; regenerate the document from its
original inputs with the license. `words split --by pages` can reflow complex
layouts slightly, so review the split pages. Linked images store no bytes in
the document, so `extract --what images` cannot write them.

## Evaluation mode

What every product discloses is in `aspose-cli docs licensing`. In Words, an
`extract --what images` copy also leaves out the watermark picture evaluation
mode draws into the header. When the edited document grows past what
evaluation mode lays out and saves (about 200 paragraphs, as with a mail merge
of many records), the output keeps only its first sections and ends with the
engine's truncation notice, which the `EVALUATION_MODE` message names;
`--verify` then reports `OUTPUT_TRUNCATED`, and the result is incomplete
whatever `itemsAffected` says. A licensed `review` reports marks an earlier
unlicensed save left in a file as `WORDS_EVALUATION_MARKS`
([verification](verification.md#visual-review)).
