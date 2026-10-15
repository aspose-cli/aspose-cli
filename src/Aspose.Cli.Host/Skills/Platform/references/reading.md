# Reading documents within budgets

Every read is bounded. Climb the projection ladder and stop at the first rung
that answers the question; never dump a whole document.

## Output

Parse `--output-format json` or `compact` (the same JSON on one line, fewer
tokens). Without the option, a terminal gets human text and a redirected
stdout gets JSON; `--help` lists the other formats. Results go to stdout; an
error envelope goes to stderr and leaves stdout empty. Reads are
deterministic: repeating one returns the same output, so two reads can be
diffed.

## The projection ladder

| Rung | Command | Answers |
|------|---------|---------|
| 1. Structure | `<product> inspect` | Counts, names, ids, properties; `--detail` adds the sections you name and, where offered, `--sample` a bounded sample |
| 2. Window | `cells read`, `pdf read`, `slides read`, `words read` | The content of a selected range, pages, slides or blocks |
| 3. Search | `cells search`, `pdf search`, `slides search`, `words search` | Where a pattern occurs |
| 4. Look | `review`, `<product> render` | What data cannot show: layout, clipping, overlap, charts |

Take addresses (sheet names, page, slide and block numbers) from rung 1
before reading rung 2; a read reports the finer ones, such as shape ids.
Every `read` takes `--content` for how much of each unit it returns, and
`cells search` takes it for what to search. Each product overview names its
inspect details and read depths; `--help` lists each command's selection and
budget options with their defaults.

Fields a read returns use the edit vocabulary: a value read under a name is
written back by the operation field of the same name
(`aspose-cli docs editing`).

Every result that reads a file reports its `input`, with
`input.fingerprint.sha256`. Pass that fingerprint to the next edit as
`--if-match` so the edit fails with `INPUT_CHANGED` if the file changed in
between.

## Windows

Every bounded read and search reports `window` after its payload
(`aspose-cli schema v2/common/result-window`):

```json
"window": {
  "unit": "hit",
  "returned": 2,
  "truncated": true,
  "next": "aspose-cli cells search \"C:\\work\\book.xlsx\" --pattern x --content values --max-hits 2 --skip 2 --output-format json"
}
```

- `truncated` true means units remain or the last one was cut short; treat
  the window as partial evidence.
- `next` is the ready-to-run command for the following window; it is absent
  when nothing remains. Run it verbatim instead of composing the next page
  yourself, adding again what it does not repeat: a password given with
  `--password` or `--password-stdin`, and global options such as `--timeout`,
  `--workdir` or `--license`. A password given with `--password-env` is
  repeated.
- `total` is absent when the read stopped before counting every unit, as a
  search usually does.

Raise a budget option only when you really need more in one result; paging
with `next` keeps each result small.

## Search paging

A search (`cells search`, `pdf search`, `slides search`, `words search`) returns at most its `--max-hits` budget of hits in document
order. A truncated search's `window.next` repeats the search with `--skip`
past the hits already returned. Each hit is one match: its `text` is the
matched text itself, and its `context` is the text around it, at most 40
characters on each side, on one line, with an ellipsis where it is cut. In
Cells a hit is one cell, holding the first match in that cell.

## Capped lists

A list inside a result, such as an `inspect --detail` list or a comparison's
samples, can be capped. A `LIST_TRUNCATED` warning in `warnings` then says so,
and its hint names the command that reads the rest. Treat a capped list as
incomplete evidence.

## Rendering

`render` writes every page, slide or sheet unless you select fewer, up to a
per-run cap that `--help` states; a larger selection is refused with a hint
that names a smaller one, so render a long document in several runs. When a
command writes several files, `--help` for its `--out` or `--out-dir` names
them, such as `<stem>.<page>.<ext>` beside `--out`; take the written paths
from the result's `outputs` instead of composing them. A `convert` result
lists its files in `outputs[]` too, even when it writes one: read that file
from `outputs[0]`; a convert result has no `output`.

## Limits

`--max-input-bytes` bounds the input size for every command; `capabilities`
lists every resource budget with its default and maximum under
`resourceBudgets`. Exceeding a budget fails with a stable code instead of
returning a partial answer (`aspose-cli docs troubleshooting`).

Encrypted inputs take `--password-env <VAR>` or `--password-stdin`.
