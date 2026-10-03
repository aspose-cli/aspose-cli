# Mail merge

The `mail_merge` op fills a template's MERGEFIELD fields from rows given in a
file (`path`) or in the batch (`inline`). A data file is a JSON array of flat
objects or an RFC 4180 CSV file with a header row, whose quoted fields may
contain commas, doubled quotes and line breaks. The data needs at least one
row: a data file holding only a CSV header or `[]` fails the op with
`MERGE_DATA_INVALID`.

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

A null value (JSON `null`, or a CSV row shorter than the header) merges as
blank text. A key a row lacks leaves that field as `«Name»` in the row's copy;
with `regions`, it merges as blank text unless no row has the key, in which
case the field stays `«Name»` in every repetition. Find leftovers with
`words query search <file> --pattern "«"`.

A template field that some rows give no value reports `MERGE_VALUE_MISSING`,
listing each field with its 1-based row numbers, such as
`Salary: record 2; Bonus: records 1, 3`. A row number is also the merged copy,
or with `regions` the region repetition, that has the gap. With `regions` only
the fields inside the region are checked. An empty string is a value and is not
reported, nor is a data field the template does not use, and `--verify` still
reports `ok`. Supply the values or confirm with the user that the result is
acceptable. Check representative
outputs for long values, CJK or right-to-left text and pagination. The
[mail-merge example](../examples/mail-merge-letters/README.md) merges a CSV
file into a letter.
