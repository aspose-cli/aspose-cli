# Reading documents

## Output

Results are JSON on stdout, indented by default; `--output-format compact`
writes the same JSON on one line. An error goes to stderr and leaves stdout
empty. A read is deterministic: the same command on the same file returns the
same output.

## Read and search

| Command | Returns |
|---------|---------|
| `<product> read <file>` | The objects with every row of their kind and their addresses (`at`), and the inventory |
| `<product> read <file> --at <address>` | One object and what it holds |
| `<product> read <file> --kind <kind>` | The objects of the kinds named; repeat `--kind` for more |
| `<product> search <file> --pattern <text>` | Each match: the address (`at`) and the `row` it is in, and the matched `text` |
| `<product> render <file>` | Images of pages, slides or sheets |

The inventory counts the objects of each kind the file holds. A kind in
`window.notListedUnlessNamed` is counted but listed only when `--kind` names
it. A search hit places the match in the row's text with `offset` and
`length`, or, where the engine's own search found it, as in a PDF page's text,
with the engine's `rect`.

## Windows

A read or a search returns one window and reports it in `window`
(`aspose-cli schema v3/common/result-window`):

```json
"window": {"unit": "object", "returned": 2, "total": 6, "truncated": true, "next": {"from": "sheet/Sheet1/cell/B2"}}
```

- `truncated` is true when units remain or the last one was cut short.
- `next` holds options of the same command, without the leading `--`, and the
  values that select the next window; it is absent when nothing remains. Run
  the command again with them.
- `total` is absent when the command stopped before counting every unit, as a
  search usually does.

`--help` gives each command's budget options with their defaults. A list
inside a result that reaches its cap is reported with a `LIST_TRUNCATED`
warning.

## Rendering

`render` writes every page, slide or sheet unless you select fewer, up to a
per-run cap that `--help` states; a larger selection is refused. When a command
writes several files, the result's `outputs` lists their paths; a `convert`
result lists its files in `outputs` too.

`--dpi` sets the resolution of a raster image such as png or jpeg. An svg has
no pixel size, so `render --to svg` refuses `--dpi`.

Missing fonts are substituted by the engine. `--font-dir <dir>` adds a font
directory to the system fonts for commands whose output depends on layout,
and can repeat; `aspose-cli fonts list` shows each engine's font sources and
its default fallback font.

## Budgets

`--max-input-bytes` bounds the size of every input; exceeding it fails with
`FILE_TOO_LARGE`. `aspose-cli schema v3/common/budgets` lists every resource
budget with its default and maximum. A budget bounds a file as it sits on
disk, not the memory an engine uses while it loads it.

Encrypted inputs take `--password-env <VAR>` or `--password-stdin`.
