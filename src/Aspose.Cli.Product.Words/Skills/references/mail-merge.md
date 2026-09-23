# Mail merge

Merge data is either a JSON array of flat objects or an RFC 4180 CSV file with a
header row: quoted fields may contain commas, doubled quotes and line breaks.

```json
[
  { "FirstName": "Ava", "Balance": "125.00" },
  { "FirstName": "Noah", "Balance": "80.00" }
]
```

Use a `mail_merge` op with exactly one of `path` or `inline`. Without `regions`,
each row after the first appends one merged copy of the whole document. With
`regions`, the rows fill the template's single `TableStart:Name`/`TableEnd:Name`
region; a template with several region names is rejected. The expected result
size is checked against the document node budget before any copy is made. Keep templates and merge data under version control when appropriate, never embed secrets, and verify representative outputs for long values, missing fields, CJK/RTL text and pagination.
