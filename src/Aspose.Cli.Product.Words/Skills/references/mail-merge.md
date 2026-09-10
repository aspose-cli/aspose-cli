# Mail merge

Merge data is either a JSON array of flat objects or a CSV file with a header row.

```json
[
  { "FirstName": "Ava", "Balance": "125.00" },
  { "FirstName": "Noah", "Balance": "80.00" }
]
```

Use a `mail_merge` op with exactly one of `path` or `inline`. Keep templates and merge data under version control when appropriate, never embed secrets, and verify representative outputs for long values, missing fields, CJK/RTL text and pagination.
