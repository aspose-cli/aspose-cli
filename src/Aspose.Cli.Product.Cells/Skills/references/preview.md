# Workbook preview

The viewer service, its lifecycle and the App are shared by every product:
`aspose-cli docs preview`. This page covers what a workbook shows.

```
aspose-cli preview book.xlsx --open --output json
```

- The default `workbook` view is the product's own grid: cell text stays
  selectable, and each `cells edit --in-place` is patched into the grid cell by
  cell. The edited cells light up where they are; the sheet, scroll position
  and untouched cells stay put.
- `--view sheets` shows one rendered image per sheet instead.
- The preview opens on the workbook's saved active sheet (`set_active_sheet`).

To build in front of the user, start the preview first, hand over its `url`,
then edit in three to five batches (data, structure, formats, charts, polish)
so each save narrates the build (`aspose-cli docs cells/design-system`,
section 11). The preview is the user's view; your own verification still uses
`query range`, `review` and `render` (`aspose-cli docs cells/verification`).
