# Mail merge

The `mail_merge` op fills a template's MERGEFIELD fields from rows given in a
file (`path`) or in the batch (`inline`). A data file is a JSON array of flat
objects or an RFC 4180 CSV file with a header row, whose quoted fields may
contain commas, doubled quotes and line breaks. The data needs at least one
row: a header-only CSV or `[]` is refused with `MERGE_DATA_INVALID` and
nothing is written.

- Without `regions`, each row after the first appends one merged copy of the
  whole document.
- With `regions: true`, the rows fill the template's single
  `TableStart:Name`/`TableEnd:Name` region; a template with several region
  names is rejected.
- The expected result size is checked against the document node budget before
  any copy is made.

```json
{
  "ops": [
    { "op": "mail_merge", "inline": [
      { "FirstName": "Ava", "Balance": "125.00" },
      { "FirstName": "Noah", "Balance": "80.00" }
    ] },
    { "op": "update_fields", "what": "all" }
  ]
}
```

A field the rows do not name stays in the output and reads as `«Name»`; find
leftovers with `words query search <file> --pattern "«"`. Check representative
outputs for long values, CJK or right-to-left text and pagination. The
[mail-merge example](../examples/mail-merge-letters/README.md) merges a CSV
file into a letter.
