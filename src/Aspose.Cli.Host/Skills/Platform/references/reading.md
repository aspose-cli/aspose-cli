# Reading documents within budgets

Every read is bounded. Climb the projection ladder and stop at the first rung
that answers the question; never dump a whole document.

## Output

Results are JSON, indented by default; `--output-format compact` writes the
same JSON on one line, with fewer tokens. Results go to stdout; an
error envelope goes to stderr and leaves stdout empty. Reads are
deterministic: repeating one returns the same output, so two reads can be
diffed.

## The projection ladder

| Rung | Command | Answers |
|------|---------|---------|
| 1. Objects | `<product> read` | The objects of the document with every row of each and the inventory's counts; `--kind` names the kinds to list, `--at` one object and what it holds |
| 2. Search | `<product> search` | Where a pattern occurs, by the address of the object and the row |
| 3. Look | `<product> render` | What data cannot show: layout, clipping, overlap, charts |

Each object comes with its `at` address, which an edit names it by. A kind a
read lists only when `--kind` names it is listed in
`window.notListedUnlessNamed`. Each product overview names its kinds;
`--help` lists each command's selection and budget options with their
defaults.

A row a read returns is the name an edit sets it by: a value read under a
name is written back by a `set` of the same name (`aspose-cli docs editing`).

## Windows

Every bounded read and search reports `window` after its payload
(`aspose-cli schema v2/common/result-window`):

```json
"window": {"unit": "object", "returned": 2, "total": 6, "truncated": true, "next": {"from": "sheet/Sheet1/cell/B2"}}
```

- `truncated` true means units remain or the last one was cut short; treat
  the window as partial evidence.
- `next` names options of the same command, without the leading `--`, and
  the values that select the following window; it is absent when nothing
  remains. Run the command again with those options replaced or added.
- `total` is absent when the read stopped before counting every unit, as a
  search usually does.

Raise a budget option only when you really need more in one result; paging
with `next` keeps each result small.

## Search paging

`<product> search` returns at most its `--max-hits` budget of hits in
document order. A truncated search's `window.next` holds only a `skip` past
the hits already returned. Each hit is one match: `at` and `row` name the
object and the row it was found in, `text` is the matched text itself, and
`offset` and `length` place it in the row's text, or, where the engine's own
search found it, as in a PDF page's text, `rect` is the rectangle the engine
gives.

## Capped lists

A list inside a result can be capped. A `LIST_TRUNCATED` warning in `warnings` then says so.
Treat a capped list as incomplete evidence.

## Rendering

`render` writes every page, slide or sheet unless you select fewer, up to a
per-run cap that `--help` states; a larger selection is refused, so render a
long document in several runs. When a command writes several files, `--help`
for its `--out` or `--out-dir` names them, such as `<stem>.<page>.<ext>`
beside `--out`; take the written paths from the result's `outputs` instead of
composing them. A `convert` result lists its files in `outputs[]` too, even
when it writes one: read that file from `outputs[0]`; a convert result has no
`output`.

`--dpi` sets the resolution of a raster image such as png or jpeg. An svg is
vector and has no pixel size, so `render --to svg` refuses `--dpi`, and any
other pixel-size option, with `OPTION_INVALID` and writes nothing; drop the
option, or render png at that size.

## Limits

`--max-input-bytes` bounds the input size for every command; `capabilities`
lists every resource budget with its default and maximum under
`resourceBudgets`. Exceeding a budget fails with a stable code instead of
returning a partial answer (`aspose-cli docs troubleshooting`).

Encrypted inputs take `--password-env <VAR>` or `--password-stdin`.
