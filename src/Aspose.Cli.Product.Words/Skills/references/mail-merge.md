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
- In evaluation mode, Aspose.Words keeps only about the first 200 paragraphs
  of the result, so a merge of many records loses the later ones and cuts one
  short; the result then warns `EVAL_INPUT_TRUNCATED` and `--verify` reports
  `OUTPUT_TRUNCATED`, while `itemsAffected` still counts every record merged.

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

A null value (JSON `null`, an empty CSV cell, or a CSV row shorter than the
header) and a key a row lacks both merge as blank text, with or without
`regions`; no `«Name»` placeholder is left. With `regions`, merge fields outside the region stay.

A template field that some rows give no value reports `MERGE_VALUE_MISSING`,
listing each field with its 1-based row numbers, such as
`Salary: record 2; Bonus: records 1, 3`. A row number is also the merged copy,
or with `regions` the region repetition, that has the gap. Each merged copy
appends the template's sections, so with a one-section template record N is
section N of the output: read it with `words query blocks <file> --section N`.
A template of S sections puts record N in sections `(N-1)*S+1` to `N*S`.

For one file per record, such as one contract per employee, either run one
`words edit` per row with an `inline` row and its own `--out`, which names
each file as you choose, or merge once and run
`aspose-cli words split merged.docx --by section --out-dir parts`: with a
one-section template, part N (`part-001.docx`, `part-002.docx`, ...) is record
N, so rename each part from row N of the data. With `regions` only
the fields inside the region are checked. A JSON empty string is a value and is
not reported, and `--verify` still reports `ok`. A data field the template does
not use is named only when it is close to a blank template field, as a likely
misspelling: `Salary: records 1, 2 (did you mean the unused data field 'Salery'?)`.
Supply the values or confirm with the user that the result is
acceptable. Check representative
outputs for long values, CJK or right-to-left text and pagination. The
[mail-merge example](../examples/mail-merge-letters/README.md) merges a CSV
file into a letter.
