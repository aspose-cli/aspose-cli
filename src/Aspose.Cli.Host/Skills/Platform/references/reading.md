# Reading documents within budgets

Every read is bounded. Climb the projection ladder and stop at the first rung
that answers the question; never dump a whole document.

## Output formats

`--output` selects `json` (indented contract envelopes), `compact` (the same
JSON on one line, fewer tokens), `table` (human text) or `markdown`. The
default is `table` on a terminal and `json` when stdout is redirected. Parse
`json` or `compact`. Results go to stdout; an error envelope goes to stderr.
Reads are deterministic: repeating one returns the same output, so two reads
can be diffed.

## The projection ladder

| Rung | Command | Answers |
|------|---------|---------|
| 1. Structure | `<product> inspect` | Counts, names, ids, properties; `--detail <kinds...>` adds lists, `--preview` adds a sample |
| 2. Window | `cells query range`, `pdf query pages`, `slides query slides`, `words query blocks` | The content of a selected range, pages, slides or blocks |
| 3. Search | `<product> query search` | Where a pattern occurs, with `--regex` and `--case-sensitive` |
| 4. Look | `review`, `<product> render` | What data cannot show: layout, clipping, overlap, charts |

Take addresses (sheet names, page and slide numbers, block numbers, shape ids)
from rung 1 before reading rung 2. Each product overview names its inspect
details and read scopes.

Fields a read returns use the edit vocabulary: a value read under a name is
written back by the operation field of the same name
(`aspose-cli docs editing`).

Every `inspect`, read and search result reports `source.fingerprint.sha256`.
Pass it to the next edit as `--if-match` so the edit fails with
`INPUT_CHANGED` if the file changed in between.

## Windows

Every bounded read and search reports `window` after its payload:

```json
"window": {
  "unit": "hit",
  "returned": 2,
  "truncated": true,
  "next": "aspose-cli cells query search \"C:\\work\\book.xlsx\" --pattern x --scope values --max-hits 2 --skip 2 --output json"
}
```

- `unit` names what is counted (`cell`, `page`, `slide`, `block`, `hit`).
- `returned` is how many units this result holds; `total` is how many the
  selection holds, omitted when the read stopped before counting them all.
- `truncated` is true when units remain or the last one was cut short.
- `next` is the ready-to-run command for the following window; it is absent
  when nothing remains. It repeats the command, the input path, the command's
  selection and budget options and `--password-env` when the password came from
  a variable. It does not repeat a password given with `--password` or
  `--password-stdin`, or global options such as `--timeout`, `--workdir` or
  `--license`; add those again. Otherwise run it verbatim instead of composing
  the next page yourself.

Reads that bound text (`--max-chars`) can also mark an individual item as cut
short; the product overview names that field. Raise a budget option
(`--max-cells`, `--max-chars`, `--max-blocks`, `--max-hits`) only when you
really need more in one result.

## Search paging

`query search` returns at most `--max-hits` hits (default 100) in document
order. A truncated search's `window.next` repeats the search with `--skip`
past the hits already returned. `total` is usually absent for a search,
because it stops at the budget instead of counting every match.

## Capped lists

A list inside a result, such as an `inspect --detail` list or a comparison's
samples, can be capped. A `LIST_TRUNCATED` warning in `warnings` then says so,
and its hint names the command that reads the rest. Treat a capped list as
incomplete evidence.

## Limits

`--max-input-bytes` bounds the input size for every command;
`capabilities` lists every resource budget with its default and maximum under
`resourceBudgets`. Exceeding a budget fails with a stable code
(`aspose-cli docs troubleshooting`) instead of returning a partial answer.

Encrypted inputs take `--password-env <VAR>` or `--password-stdin`.
