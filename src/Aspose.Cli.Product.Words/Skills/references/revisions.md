# Revisions and comparison

Before editing, inspect `document.hasRevisions`, `document.revisionAuthors`,
`document.commentCount`,
`document.protection` and `document.signed` in the `words inspect` result.
Editing or converting a document that already has revisions reports
`TRACKED_CHANGES_PRESENT` while the output still contains them; disclose them.
A batch that accepts or rejects every revision does not report it. Which
outputs keep revisions is in [editing](editing.md#save-and-verify).

## Listing revisions

`--detail revisions` lists every tracked change in document order, so "what did
the reviewer change?" needs no accepted or rejected copies:

```powershell
aspose-cli words inspect contract.docx --detail revisions --output-format json
```

```json
"revisions": [
  { "revision": 1, "kind": "deletion", "author": "Alice Legal", "date": "2026-09-01T10:30:00+00:00", "scope": "body", "block": 1, "text": "thirty" },
  { "revision": 2, "kind": "insertion", "author": "Alice Legal", "date": "2026-09-01T10:30:00+00:00", "scope": "body", "block": 1, "text": "sixty" }
]
```

Each entry's fields are described in
`aspose-cli schema v2/words/inspect-result`. Reading them:

One entry is one change: adjacent runs and paragraph marks that one author
inserted, deleted or moved are listed together, except inside comment text,
where each run and paragraph mark is its own entry. So the document's
`revisionCount`, one per run, paragraph mark or other changed node, is usually
larger than the number of entries: a two-run insertion is one entry and two
revisions. A replacement is a deletion followed by an insertion by the same
author in the same block: read the pair above as "thirty" replaced by "sixty".
A move is listed twice, once at its source and once at its destination, each
with the moved text.

A change inside a comment's text belongs to the comment's `scope`, not to the
body around it, even though its `block` is the block that anchors the
comment. Format and style definition changes have no `text`; read the
`block` instead. A paragraph mark inserted or deleted on its own, as when a
paragraph is split or joined, has no `text` either. The `date` is reported as
UTC; Word stores the author's local time in this field, so a revision date
Word wrote can be off from the true UTC by the author's time zone offset,
while the revisions a `--track-changes` edit records carry the
UTC time of the run. Long texts are cut at 300 characters; read the block for the
full text. More than 1000 changes carry a `LIST_TRUNCATED` warning.

## Accepting and rejecting

`accept_revisions` and `reject_revisions` decide every revision, one
`author`'s, or the changes whose `revision` numbers they list. Numbers refer
to the document as it was before the batch, as block addresses do, so one
batch can accept some changes and reject others; list both halves of a
replacement.
`itemsAffected` counts the blocks and sections whose content the decision
changed, and its `targets` name them. Authors match exactly, as
`document.revisionAuthors` spells them.

```json
{ "ops": [
  { "op": "accept_revisions", "revisions": [1, 2, 5, 6] },
  { "op": "reject_revisions", "revisions": [3, 4] }
] }
```

A decision by number fails with `OPS_INVALID`, changing nothing, when:

- it comes after an operation that applied and is not a revision decision,
  which could split the runs of a change and leave part of it undecided. Put
  the decisions first, or in a batch of their own;
- an earlier decision in the batch already took one of its revisions;
- it lists only some of the changes that share one node and type, such as a
  paragraph's format change and its mark's character format change, which the
  engine can only decide together. The message names the numbers to list.

## Comparing

`words compare` refuses inputs that already contain revisions (`REVISIONS_UNRESOLVED`); list them with `--detail revisions` above. To compare anyway, make reviewed copies first, explicitly accept or reject revisions there, then compare:

```powershell
aspose-cli words compare original.docx changed.docx --out redline.docx --author "Legal Review" --output-format json
```

The redline's revisions are attributed to `--author`, or to `Aspose CLI`
without it.

`--granularity word` (the default) marks whole changed words. Chinese and
Japanese text has no spaces between words, so a one-character edit marks the
whole run of text; pass `--granularity char` to mark only the changed
characters.

The result counts the revisions by kind and lists up to 50 `samples`, one per
revision run or paragraph mark, with the `kind` names `--detail revisions`
uses; a paragraph mark's sample has no `text`.
