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
heading1` needs a Heading 1 paragraph. `define_style` creates a missing
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

## What a warning asks you to disclose

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
